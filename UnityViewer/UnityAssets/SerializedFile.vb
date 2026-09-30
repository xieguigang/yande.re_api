Option Strict On
Option Explicit On

Imports System.IO

''' <summary>
''' Unity SerializedFile 解析（format 17 / Unity 5.6）。
''' Release 构建剥离了 TypeTree，故跳过脆弱的类型表，改为在元数据区扫描定位对象表：
''' 找到 objectCount，其首个对象的 offset 落在数据区（>= DataOffset）且 classID 合法。
''' </summary>
Public Class SerializedFile

    Public Property MetadataSize As Integer = 0
    Public Property FileSize As Integer = 0
    Public Property FormatVersion As Integer = 0
    Public Property DataOffset As Integer = 0
    Public Property Endianness As Byte = 0
    Public Property UnityVersion As String = ""
    Public Property TargetPlatform As Integer = 0

    Public Property Objects As List(Of ObjectInfo)
    Public Property SourcePath As String = ""
    Public Property RawBytes As Byte()

    ''' <summary>外部资源（.resource / 包内 entry）解析回调：根据资源文件名返回其字节。</summary>
    Public Property ExternalResolver As Func(Of String, Byte())

    Public Sub New()
        Objects = New List(Of ObjectInfo)()
    End Sub

    Public Shared Function LoadFromFile(path As String) As SerializedFile
        Dim bytes = File.ReadAllBytes(path)
        Dim sf = LoadFromBytes(bytes, path)
        ' 设置基于磁盘目录的外部资源解析（查找同目录及子目录下的 .resource 文件）
        Dim dir = Path.GetDirectoryName(Path.GetFullPath(path))
        sf.ExternalResolver = Function(name As String) ResolveDiskResource(dir, name)
        Return sf
    End Function

    Public Shared Function LoadFromBundleEntry(entry As BundleEntry, bundle As UnityFSBundle, sourcePath As String) As SerializedFile
        Dim sf = LoadFromBytes(entry.Data, sourcePath)
        sf.ExternalResolver = Function(name As String) ResolveBundleResource(bundle, name)
        Return sf
    End Function

    Private Shared Function ResolveDiskResource(baseDir As String, name As String) As Byte()
        ' name 可能是 "archive:/.../xxx.resource" 或纯文件名
        Dim leaf = name
        Dim idx = leaf.LastIndexOf("/"c)
        If idx >= 0 Then leaf = leaf.Substring(idx + 1)
        idx = leaf.LastIndexOf("\"c)
        If idx >= 0 Then leaf = leaf.Substring(idx + 1)

        Dim candidates As New List(Of String) From {Path.Combine(baseDir, leaf)}
        ' 在子目录中搜索
        Try
            For Each f In Directory.EnumerateFiles(baseDir, leaf, SearchOption.AllDirectories)
                candidates.Add(f)
                Exit For
            Next
        Catch
        End Try
        For Each c In candidates
            If File.Exists(c) Then
                Try
                    Return File.ReadAllBytes(c)
                Catch
                End Try
            End If
        Next
        Return Nothing
    End Function

    Private Shared Function ResolveBundleResource(bundle As UnityFSBundle, name As String) As Byte()
        Dim e = bundle.GetEntry(name)
        If e IsNot Nothing AndAlso e.Data IsNot Nothing Then Return e.Data
        Return Nothing
    End Function

    Public Shared Function LoadFromBytes(bytes() As Byte, sourcePath As String) As SerializedFile
        Dim sf As New SerializedFile()
        sf.RawBytes = bytes
        sf.SourcePath = sourcePath

        Using ms = New MemoryStream(bytes)
            Using r = New EndianBinaryReader(ms, bigEndian:=True)
                sf.MetadataSize = r.ReadInt32()
                sf.FileSize = r.ReadInt32()
                sf.FormatVersion = r.ReadInt32()
                sf.DataOffset = r.ReadInt32()
                sf.Endianness = CByte(r.ReadByte())
                r.ReadBytes(3) ' reserved
            End Using
        End Using

        ' 读取版本字符串用于显示
        Try
            Using ms = New MemoryStream(bytes)
                Using r = New EndianBinaryReader(ms, bigEndian:=False)
                    r.Position = 20
                    sf.UnityVersion = r.ReadStringToNull()
                End Using
            End Using
        Catch
        End Try

        ' 扫描定位对象表
        Dim pw As Integer = 8
        Dim p = FindObjectTable(bytes, sf.DataOffset, sf.FileSize, pw)
        If p < 0 Then
            Throw New InvalidDataException("无法在 SerializedFile 中定位对象表: " & sourcePath)
        End If

        Dim objectCount = BitConverter.ToInt32(bytes, p)
        If objectCount < 0 OrElse objectCount > 5000000 Then
            Throw New InvalidDataException("对象数量异常 (" & objectCount & "): " & sourcePath)
        End If

        Dim entrySize = pw + 18
        Dim baseOff = p + 4
        For i = 0 To objectCount - 1
            Dim o = baseOff + i * entrySize
            If o + entrySize > bytes.Length Then Exit For
            Dim obj As New ObjectInfo()
            obj.Index = i
            If pw = 8 Then
                obj.PathID = BitConverter.ToInt64(bytes, o)
            Else
                obj.PathID = BitConverter.ToInt32(bytes, o)
            End If
            obj.Offset = BitConverter.ToInt32(bytes, o + pw)
            obj.Length = BitConverter.ToInt32(bytes, o + pw + 4)
            obj.TypeID = BitConverter.ToInt32(bytes, o + pw + 8)
            obj.ClassID = BitConverter.ToInt16(bytes, o + pw + 12)
            obj.ScriptTypeIndex = BitConverter.ToInt16(bytes, o + pw + 14)
            obj.Stripped = BitConverter.ToInt16(bytes, o + pw + 16)
            sf.Objects.Add(obj)
        Next

        Return sf
    End Function

    ''' <summary>在元数据区 [20, dataOffset) 内扫描对象表起始位置；返回 objectCount 的偏移，并通过 pathWidth 返回 pathID 宽度（8 或 4）。</summary>
    Private Shared Function FindObjectTable(bytes() As Byte, dataOffset As Integer, fileSize As Integer, ByRef pathWidth As Integer) As Integer
        For p = 20 To dataOffset - 60
            Dim oc = BitConverter.ToInt32(bytes, p)
            If oc < 1 OrElse oc > 2000000 Then Continue For

            For Each pw In New Integer() {8, 4}
                Dim baseOff = p + 4
                Dim off0 = BitConverter.ToInt32(bytes, baseOff + pw)
                Dim len0 = BitConverter.ToInt32(bytes, baseOff + pw + 4)
                Dim classID0 = BitConverter.ToInt16(bytes, baseOff + pw + 12)

                If off0 < dataOffset Then Continue For
                If len0 <= 0 OrElse len0 > 100000000 Then Continue For
                If off0 + len0 > fileSize Then Continue For
                If classID0 <= 0 OrElse classID0 >= 1000 Then Continue For

                ' 校验最后一个对象也合法，避免误命中
                If oc >= 2 Then
                    Dim lastBase = baseOff + (oc - 1) * (pw + 18)
                    If lastBase + (pw + 18) > bytes.Length Then Continue For
                    Dim offL = BitConverter.ToInt32(bytes, lastBase + pw)
                    Dim lenL = BitConverter.ToInt32(bytes, lastBase + pw + 4)
                    If offL < dataOffset OrElse offL + lenL > fileSize Then Continue For
                End If

                pathWidth = pw
                Return p
            Next
        Next
        Return -1
    End Function

    ''' <summary>返回对象自身记录字节（位于当前文件/包内，可能只含外部资源引用）。</summary>
    Public Function GetObjectBytes(obj As ObjectInfo) As Byte()
        If obj.Offset < 0 OrElse obj.Length <= 0 OrElse obj.Offset + obj.Length > RawBytes.Length Then
            Return New Byte(-1) {}
        End If
        Dim data(obj.Length - 1) As Byte
        Array.Copy(RawBytes, obj.Offset, data, 0, obj.Length)
        Return data
    End Function

    ''' <summary>返回定位到对象数据的读取器（小端）。</summary>
    Public Function CreateReader(obj As ObjectInfo) As EndianBinaryReader
        Dim ms = New MemoryStream(GetObjectBytes(obj))
        Return New EndianBinaryReader(ms, bigEndian:=False)
    End Function

    ''' <summary>解析外部资源（如 .resource 中的流数据），通过 ExternalResolver 获取其字节。</summary>
    Public Function GetExternalResource(name As String, offset As Long, size As Integer) As Byte()
        If ExternalResolver Is Nothing Then Return Nothing
        Dim all = ExternalResolver(name)
        If all Is Nothing OrElse offset < 0 OrElse size <= 0 Then Return Nothing
        If offset + size > all.Length Then
            ' 越界则尽量裁剪
            If offset >= all.Length Then Return Nothing
            size = CInt(all.Length - offset)
        End If
        Dim data(size - 1) As Byte
        Array.Copy(all, offset, data, 0, size)
        Return data
    End Function

    ''' <summary>
    ''' 读取 Unity Object 基础字段并跳过，返回 m_Name。
    ''' 5.6 布局：int32 hideFlags + PPtr m_PrefabInstance + 对齐字符串 m_Name。
    ''' </summary>
    Public Shared Function ReadObjectName(r As EndianBinaryReader) As String
        r.ReadInt32()          ' m_ObjectHideFlags
        r.ReadPPtr()           ' m_PrefabInstance (fileID int32, pathID int64)
        r.Align(4)
        Return r.ReadAlignedString() ' m_Name
    End Function

End Class

''' <summary>SerializedFile 中的对象信息。</summary>
Public Class ObjectInfo
    Public Index As Integer
    Public PathID As Long
    Public Offset As Integer
    Public Length As Integer
    Public TypeID As Integer
    Public ClassID As Integer
    Public ScriptTypeIndex As Integer
    Public Stripped As Integer

    Public Overrides Function ToString() As String
        Return "[" & ClassID & "] " & ClassIDToName(ClassID) & " #" & PathID & " @" & Offset & " len=" & Length
    End Function
End Class
