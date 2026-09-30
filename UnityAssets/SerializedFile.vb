Option Strict On
Option Explicit On

Imports System.IO

''' <summary>
''' Unity SerializedFile（格式 17 / Unity 5.6.4p3，Release 剥离 TypeTree）解析。
''' 关键点：
'''  - 对象表条目的 byteStart 相对 dataOffset 存储（绝对偏移 = dataOffset + 存储值）。
'''  - 条目不含 classID，classID 存于类型表（type-list），条目中的 typeID 为类型表索引。
'''  - 类型表：int32 数量 + N 条 23 字节记录[classID(int32)+isStripped(1)+scriptTypeIndex(2)+hash(16)]。
''' </summary>
Public Class SerializedFile

    Public RawBytes() As Byte = {}
    Public SourcePath As String = ""
    Public Version As Integer = 0
    Public DataOffset As Integer = 0
    Public Objects As New List(Of ObjectInfo)()

    ''' <summary>类型表：typeID 索引 -> classID。通过类型表映射得到对象的 classID。</summary>
    Public ClassIDByTypeIndex As New List(Of Integer)()

    ''' <summary>外部引用文件名称（用于解析外置 .resource）。</summary>
    Public Externals As New List(Of String)()

    ''' <summary>外置资源解析回调，由调用方（扫描器）注入。</summary>
    Public ExternalResolver As Func(Of String, Byte()) = Nothing

    Public Class ObjectInfo
        Public Index As Integer = 0
        Public PathID As Long = 0
        Public Offset As Integer = 0
        Public Length As Integer = 0
        Public TypeID As Integer = 0
        Public ClassID As Integer = 0
        Public ScriptTypeIndex As Integer = 0
        Public Stripped As Boolean = False
    End Class

    Public Shared Function LoadFromFile(path As String) As SerializedFile
        Dim bytes = File.ReadAllBytes(path)
        Dim sf = LoadFromBytes(bytes, path)
        ' 设置外部资源解析：默认在源文件目录内查找 .resource / 同名外置流文件
        sf.ExternalResolver = Function(name As String) ResolveDiskResource(Path.GetDirectoryName(Path.GetFullPath(path)), name)
        Return sf
    End Function

    Public Shared Function LoadFromBytes(bytes() As Byte, sourcePath As String) As SerializedFile
        Dim sf As New SerializedFile() With {.RawBytes = bytes, .SourcePath = sourcePath}
        Dim r = New EndianBinaryReader(New MemoryStream(bytes))
        ' 头部字段为大端
        Dim metadataSize = r.ReadInt32BE()
        Dim fileSize = r.ReadInt32BE()
        Dim format = r.ReadInt32BE()
        Dim dataOffset = r.ReadInt32BE()
        Dim endianness = r.ReadByte()
        r.ReadBytes(3) ' reserved
        sf.Version = format
        sf.DataOffset = dataOffset

        ' 头部之后（含版本字符串）为小端
        r.IsLittleEndian = True
        Dim unityVersion = r.ReadStringToNull()

        FindTypeList(bytes, sf)
        FindExternals(bytes, sf, dataOffset)
        FindObjectTable(bytes, sf, dataOffset)
        Return sf
    End Function

    ''' <summary>扫描定位类型表，得到 typeID -> classID 映射。</summary>
    Private Shared Sub FindTypeList(bytes() As Byte, sf As SerializedFile)
        For T = 20 To Math.Min(bytes.Length - 200, 2000)
            Dim C = BitConverter.ToInt32(bytes, T)
            If C < 1 OrElse C > 200 Then Continue For
            Dim ok = True
            Dim list As New List(Of Integer)()
            Dim entrySize = 23
            If T + 4 + C * entrySize > bytes.Length Then Continue For
            For i = 0 To C - 1
                Dim eo = T + 4 + i * entrySize
                Dim cid = BitConverter.ToInt32(bytes, eo)
                If cid < 1 OrElse cid > 1000 Then ok = False : Exit For
                list.Add(cid)
            Next
            If ok AndAlso list.Count > 0 Then
                sf.ClassIDByTypeIndex = list
                Return
            End If
        Next
    End Sub

    ''' <summary>对象表扫描：条目 = pathID(pw)+byteStart(4)+byteSize(4)+typeID(4)，byteStart 相对 dataOffset。</summary>
    Private Shared Sub FindObjectTable(bytes() As Byte, sf As SerializedFile, dataOffset As Integer)
        For Each pw In New Integer() {8, 4}
            Dim entrySize = pw + 12
            For p = 20 To dataOffset - 20
                Dim oc = BitConverter.ToInt32(bytes, p)
                If oc < 1 OrElse oc > 300000 Then Continue For
                If p + 4 + oc * entrySize > bytes.Length Then Continue For
                Dim ok = True
                Dim hasAnchor = False
                For i = 0 To oc - 1
                    Dim eo = p + 4 + i * entrySize
                    Dim os = BitConverter.ToInt32(bytes, eo + pw)
                    Dim len = BitConverter.ToInt32(bytes, eo + pw + 4)
                    Dim tid = BitConverter.ToInt32(bytes, eo + pw + 8)
                    Dim abs = dataOffset + os
                    If os < 0 OrElse len <= 0 OrElse abs + len > bytes.Length Then ok = False : Exit For
                    If tid < 0 OrElse tid >= sf.ClassIDByTypeIndex.Count Then ok = False : Exit For
                    If abs = dataOffset Then hasAnchor = True
                Next
                If ok Then
                    ' 优选首个对象恰好落在 dataOffset 的表
                    If hasAnchor OrElse sf.Objects.Count = 0 Then
                        For i = 0 To oc - 1
                            Dim eo = p + 4 + i * entrySize
                            Dim oi As New ObjectInfo()
                            If pw = 8 Then oi.PathID = BitConverter.ToInt64(bytes, eo) Else oi.PathID = BitConverter.ToInt32(bytes, eo)
                            oi.Offset = dataOffset + BitConverter.ToInt32(bytes, eo + pw)
                            oi.Length = BitConverter.ToInt32(bytes, eo + pw + 4)
                            oi.TypeID = BitConverter.ToInt32(bytes, eo + pw + 8)
                            If oi.TypeID >= 0 AndAlso oi.TypeID < sf.ClassIDByTypeIndex.Count Then
                                oi.ClassID = sf.ClassIDByTypeIndex(oi.TypeID)
                            End If
                            oi.Index = i
                            sf.Objects.Add(oi)
                        Next
                        If hasAnchor Then Return
                    End If
                End If
            Next
        Next
        Throw New Exception("无法定位对象表")
    End Sub

    ''' <summary>对象表之后为外部引用表（数量 + 各引用[guid/type/pathName/fileName]）。仅记录名称用于外置解析。</summary>
    Private Shared Sub FindExternals(bytes() As Byte, sf As SerializedFile, dataOffset As Integer)
        ' 尝试在对象表之后查找 externals 列表（宽松扫描）
        Try
            For p = dataOffset - 200 To dataOffset + 4
                If p < 0 OrElse p + 4 > bytes.Length Then Continue For
                Dim ec = BitConverter.ToInt32(bytes, p)
                If ec < 0 OrElse ec > 64 Then Continue For
                Dim scan = p + 4
                If scan + ec * 30 > bytes.Length Then Continue For
                ' 尝试用 EndianBinaryReader 读取 ec 个引用字符串
                Dim tmp = New EndianBinaryReader(New MemoryStream(bytes, False))
                tmp.IsLittleEndian = True
                tmp.BaseStream.Seek(scan, SeekOrigin.Begin)
                Dim ok = True
                Dim names As New List(Of String)()
                For k = 0 To ec - 1
                    Dim guid = tmp.ReadAlignedString()
                    If guid Is Nothing OrElse guid.Length > 200 Then ok = False : Exit For
                    Dim typ = tmp.ReadInt32()
                    Dim pathName = tmp.ReadAlignedString()
                    Dim fileName = tmp.ReadAlignedString()
                    If pathName Is Nothing OrElse fileName Is Nothing OrElse pathName.Length > 300 OrElse fileName.Length > 300 Then ok = False : Exit For
                    names.Add(pathName)
                    names.Add(fileName)
                Next
                If ok AndAlso names.Count > 0 Then
                    sf.Externals = names
                    Return
                End If
            Next
        Catch
        End Try
    End Sub

    Public Function GetObjectBytes(obj As ObjectInfo) As Byte()
        If obj.Offset + obj.Length > RawBytes.Length Then Return {}
        Dim buf(obj.Length - 1) As Byte
        Array.Copy(RawBytes, obj.Offset, buf, 0, obj.Length)
        Return buf
    End Function

    Public Function CreateReader() As EndianBinaryReader
        Return New EndianBinaryReader(New MemoryStream(RawBytes))
    End Function

    ''' <summary>读取对象的 m_Name（跳过基类字段：hideFlags + PPtr(m_PrefabInstance) + Align + 对齐字符串）。</summary>
    Public Function ReadObjectName(obj As ObjectInfo) As String
        Dim r = New EndianBinaryReader(New MemoryStream(RawBytes))
        r.IsLittleEndian = True
        r.BaseStream.Seek(obj.Offset, SeekOrigin.Begin)
        r.ReadInt32() ' m_ObjectHideFlags
        r.ReadBytes(12) ' PPtr m_PrefabInstance
        r.Align(4)
        Dim nm = r.ReadAlignedString()
        Return If(nm Is Nothing, "", nm)
    End Function

    ''' <summary>跳过对象基类字段（hideFlags + PPtr + 对齐名称字符串），返回定位到派生类字段的读取器。</summary>
    Public Function SkipObjectBase(obj As ObjectInfo) As EndianBinaryReader
        Dim r = New EndianBinaryReader(New MemoryStream(RawBytes))
        r.IsLittleEndian = True
        r.BaseStream.Seek(obj.Offset, SeekOrigin.Begin)
        r.ReadInt32() ' hideFlags
        r.ReadBytes(12) ' PPtr
        r.Align(4)
        r.ReadAlignedString() ' m_Name
        Return r
    End Function

    ''' <summary>外置资源解析：先走注入的 ExternalResolver，否则在磁盘上按名称查找 .resource 等外置流文件。</summary>
    Public Function GetExternalResource(name As String) As Byte()
        If ExternalResolver IsNot Nothing Then
            Dim b = ExternalResolver(name)
            If b IsNot Nothing Then Return b
        End If
        ' 回退：在源目录内查找文件名包含 name 的外置文件
        Return ResolveDiskResource(Path.GetDirectoryName(Path.GetFullPath(SourcePath)), name)
    End Function

    Private Shared Function ResolveDiskResource(baseDir As String, name As String) As Byte()
        If baseDir = "" OrElse Not Directory.Exists(baseDir) Then Return Nothing
        Dim baseName = Path.GetFileName(name).Replace("\", "/")
        If baseName.Contains("/"c) Then baseName = baseName.Split("/"c).Last()
        For Each f In Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories)
            Dim fn = Path.GetFileName(f)
            If fn.Equals(baseName, StringComparison.OrdinalIgnoreCase) OrElse
               fn.IndexOf(baseName, StringComparison.OrdinalIgnoreCase) >= 0 Then
                Try
                    Return File.ReadAllBytes(f)
                Catch
                    Return Nothing
                End Try
            End If
        Next
        Return Nothing
    End Function

End Class
