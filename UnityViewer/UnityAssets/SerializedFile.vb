Option Strict On
Option Explicit On

Imports System.IO

''' <summary>
''' Unity SerializedFile（格式 17 / Unity 5.6.4p3，Release 剥离 TypeTree）解析。
''' 关键点：
'''  - 对象表条目的 byteStart 相对 dataOffset 存储（绝对偏移 = dataOffset + 存储值）。
'''  - 条目不含 classID，classID 存于类型表（type-list）：条目中的 typeID 为类型表索引。
'''  - 类型表：int32 数量 + N 条记录[classID(int32)+isStripped(1)+scriptTypeIndex(2)+hash(16)]（约 23 字节）。
''' </summary>
Public Class SerializedFile

    Public MetadataSize As Integer = 0
    Public FileSize As Integer = 0
    Public FormatVersion As Integer = 0
    Public DataOffset As Integer = 0
    Public Endianness As Byte = 0
    Public UnityVersion As String = ""
    Public TargetPlatform As Integer = 0

    Public Objects As New List(Of ObjectInfo)()
    Public SourcePath As String = ""
    Public RawBytes As Byte()
    Public ClassIDByTypeIndex As New List(Of Integer)()

    ''' <summary>外置资源（.resource / 包内 entry）解析回调：根据资源文件名返回其字节。</summary>
    Public ExternalResolver As Func(Of String, Byte())

    Public Sub New()
        Objects = New List(Of ObjectInfo)()
    End Sub

    Public Shared Function LoadFromFile(path As String) As SerializedFile
        Dim bytes = File.ReadAllBytes(path)
        Dim sf = LoadFromBytes(bytes, path)
        Dim dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))
        sf.ExternalResolver = Function(name As String) ResolveDiskResource(dir, name)
        Return sf
    End Function

    Public Shared Function LoadFromBundleEntry(entry As BundleEntry, bundle As UnityFSBundle, sourcePath As String) As SerializedFile
        Dim data = entry.Data
        Dim sf = LoadFromBytes(data, sourcePath)

        ' Unity 有时将对象数据延伸到本 entry 之后的数据区（同一解压数据流的后续部分），
        ' 此时用从 entry.Offset 起到数据区末尾的扩展视图重新解析。
        Dim overflow = False
        For Each obj In sf.Objects
            If obj.Offset + obj.Length > data.Length Then overflow = True : Exit For
        Next
        If overflow AndAlso bundle.ArchiveData IsNot Nothing AndAlso bundle.ArchiveData.Length > entry.Offset + data.Length Then
            Dim extLen = bundle.ArchiveData.Length - CInt(entry.Offset)
            Dim ext(extLen - 1) As Byte
            Array.Copy(bundle.ArchiveData, entry.Offset, ext, 0, extLen)
            sf = LoadFromBytes(ext, sourcePath)
        End If

        sf.ExternalResolver = Function(name As String) ResolveBundleResource(bundle, name)
        Return sf
    End Function

    Private Shared Function ResolveDiskResource(baseDir As String, name As String) As Byte()
        Dim leaf = name
        Dim idx = leaf.LastIndexOf("/"c)
        If idx >= 0 Then leaf = leaf.Substring(idx + 1)
        idx = leaf.LastIndexOf("\"c)
        If idx >= 0 Then leaf = leaf.Substring(idx + 1)
        Dim candidates As New List(Of String) From {System.IO.Path.Combine(baseDir, leaf)}
        Try
            For Each f In Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories)
                If System.IO.Path.GetFileName(f).IndexOf(leaf, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    candidates.Add(f)
                End If
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

        Try
            Using ms = New MemoryStream(bytes)
                Using r = New EndianBinaryReader(ms, bigEndian:=False)
                    r.Position = 20
                    sf.UnityVersion = r.ReadStringToNull()
                End Using
            End Using
        Catch
        End Try

        Dim typeListEnd As Integer = 0
        FindTypeList(bytes, sf, typeListEnd)
        Dim pw As Integer = 8
        Dim p = FindObjectTable(bytes, sf.DataOffset, sf.FileSize, sf.ClassIDByTypeIndex, typeListEnd, pw)
        If p < 0 Then
            Throw New InvalidDataException("无法在 SerializedFile 中定位对象表: " & sourcePath)
        End If

        Dim objectCount = BitConverter.ToInt32(bytes, p)
        If objectCount < 0 OrElse objectCount > 5000000 Then
            Throw New InvalidDataException("对象数量异常 (" & objectCount & "): " & sourcePath)
        End If

        Dim entrySize = pw + 12
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
            Dim storedOffset = BitConverter.ToInt32(bytes, o + pw)
            Dim absOff As Long = CLng(sf.DataOffset) + CLng(storedOffset)
            If storedOffset < 0 OrElse absOff < sf.DataOffset OrElse absOff + obj.Length > sf.FileSize Then
                Throw New InvalidDataException("对象偏移越界 @" & i & " in " & sourcePath)
            End If
            obj.Offset = CInt(absOff)
            obj.Length = BitConverter.ToInt32(bytes, o + pw + 4)
            obj.TypeID = BitConverter.ToInt32(bytes, o + pw + 8)
            If obj.TypeID >= 0 AndAlso obj.TypeID < sf.ClassIDByTypeIndex.Count Then
                obj.ClassID = sf.ClassIDByTypeIndex(obj.TypeID)
            Else
                obj.ClassID = obj.TypeID
            End If
            sf.Objects.Add(obj)
        Next

        Return sf
    End Function

    ''' <summary>解析类型表（typeID -> classID），并通过 typeListEnd 返回类型表之后的偏移。</summary>
    Private Shared Sub FindTypeList(bytes() As Byte, sf As SerializedFile, ByRef typeListEnd As Integer)
        typeListEnd = 0
        If TryParseTypeList(bytes, sf, typeListEnd) Then Return
        ScanTypeList(bytes, sf, typeListEnd)
    End Sub

    ''' <summary>
    ''' 按标准头部结构顺序解析：版本字符串 -> targetPlatform(int32) -> enableTypeTree(byte)
    ''' -> typesCount(int32) -> 各类型[classID(int32)+isStripped(1)+scriptTypeIndex(int16)+hash(16)]。
    ''' </summary>
    Private Shared Function TryParseTypeList(bytes() As Byte, sf As SerializedFile, ByRef typeListEnd As Integer) As Boolean
        Try
            Using ms = New MemoryStream(bytes)
                Using r = New EndianBinaryReader(ms, bigEndian:=False)
                    r.Position = 20
                    Dim ver = r.ReadStringToNull()
                    If ver Is Nothing OrElse ver.Length = 0 OrElse ver.Length > 64 Then Return False
                    If Environment.GetEnvironmentVariable("UV_DBG") = "1" Then Console.Error.WriteLine("DBG afterVer pos=" & r.Position)
                    Dim tp = r.ReadInt32()
                    If tp < 0 OrElse tp > 64 Then Return False
                    r.ReadByte() ' enableTypeTree
                    If Environment.GetEnvironmentVariable("UV_DBG") = "1" Then Console.Error.WriteLine("DBG afterEnable pos=" & r.Position)
                    Dim typesCount = r.ReadInt32()
                    If Environment.GetEnvironmentVariable("UV_DBG") = "1" Then
                        Console.Error.WriteLine("DBG TryParse ver='" & ver & "' tp=" & tp & " typesCount=" & typesCount & " pos=" & r.Position)
                    End If
                    If typesCount < 1 OrElse typesCount > 300 Then Return False
                    Dim list As New List(Of Integer)()
                    Dim dbgPos As String = ""
                    For i = 0 To typesCount - 1
                        If r.Position + 23 > bytes.Length Then Return False
                        Dim cid = r.ReadInt32()
                        If Environment.GetEnvironmentVariable("UV_DBG") = "1" Then
                            dbgPos = dbgPos & "[" & i & "@" & r.Position & "=" & cid & "]"
                        End If
                        If cid < 1 OrElse cid > 1000 Then
                            list.Add(-cid)
                        Else
                            list.Add(cid)
                        End If
                        r.ReadByte()       ' isStripped
                        r.ReadInt16()      ' scriptTypeIndex
                        r.ReadBytes(16)    ' hash
                    Next
                    If Environment.GetEnvironmentVariable("UV_DBG") = "1" Then
                        Console.Error.WriteLine("DBG TLENDS pos=" & r.Position & " entries=" & dbgPos)
                    End If
                    If Environment.GetEnvironmentVariable("UV_DBG") = "1" Then
                        Dim s As String = ""
                        For Each c In list : s = s & c & "," : Next
                        Console.Error.WriteLine("DBG TryParse OK count=" & list.Count & " end=" & r.Position & " cids=(" & s & ")")
                    End If
                    sf.ClassIDByTypeIndex = list
                    typeListEnd = CInt(r.Position)
                    Return True
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    ''' <summary>兜底：扫描所有候选类型表，选取 classID 数量最多者。</summary>
    Private Shared Sub ScanTypeList(bytes() As Byte, sf As SerializedFile, ByRef typeListEnd As Integer)
        Dim bestCount = -1
        For T = 20 To Math.Min(bytes.Length - 60, 4096)
            Dim C = BitConverter.ToInt32(bytes, T)
            If C < 1 OrElse C > 200 Then Continue For
            For Each entrySize In New Integer() {23, 24, 22, 25, 21}
                If T + 4 + C * entrySize > bytes.Length Then Continue For
                Dim ok = True
                Dim list As New List(Of Integer)()
                For i = 0 To C - 1
                    Dim eo = T + 4 + i * entrySize
                    Dim cid = BitConverter.ToInt32(bytes, eo)
                    If cid < 1 OrElse cid > 1000 Then ok = False : Exit For
                    list.Add(cid)
                Next
                If ok AndAlso list.Count > bestCount Then
                    bestCount = list.Count
                    sf.ClassIDByTypeIndex = list
                    typeListEnd = T + 4 + C * entrySize
                End If
            Next
        Next
    End Sub

    ''' <summary>
    ''' 在类型表之后扫描对象表。条目 = pathID(pw)+byteStart(4)+byteSize(4)+typeID(4)，
    ''' byteStart 相对 dataOffset。要求全部对象校验通过（偏移/大小/typeID范围）。
    ''' </summary>
    Private Shared Function FindObjectTable(bytes() As Byte, dataOffset As Integer, fileSize As Integer, typeList As List(Of Integer), typeListEnd As Integer, ByRef pathWidth As Integer) As Integer
        Dim startPos = Math.Max(20, typeListEnd)
        Dim endPos = Math.Min(fileSize - 40, dataOffset - 40)
        If endPos < startPos Then endPos = startPos

        ' 收集窗口内全部“全量校验通过”的候选，选取对象数最多者（真实表对象数远大于巧合误报）。
        Dim bestP As Integer = -1
        Dim bestPw As Integer = 8
        Dim bestOc As Integer = -1
        Dim bestDist As Integer = Integer.MaxValue

        For window = 0 To 1
            Dim hi = If(window = 0, Math.Min(endPos, startPos + 8192), endPos)
            For p = startPos To hi
                Dim oc = BitConverter.ToInt32(bytes, p)
                If oc < 1 OrElse oc > 2000000 Then Continue For
                For Each pw In New Integer() {8, 4}
                    Dim entrySize = pw + 12
                    Dim baseOff = p + 4
                    If baseOff + oc * entrySize > bytes.Length Then Continue For
                    Dim allOk = True
                    For i = 0 To oc - 1
                        Dim eo = baseOff + i * entrySize
                        Dim stored = BitConverter.ToInt32(bytes, eo + pw)
                        Dim len = BitConverter.ToInt32(bytes, eo + pw + 4)
                        Dim tid = BitConverter.ToInt32(bytes, eo + pw + 8)
                        Dim abs As Long = CLng(dataOffset) + CLng(stored)
                        If stored < 0 OrElse abs < dataOffset OrElse len <= 0 OrElse len > 200000000 OrElse abs + len > fileSize Then allOk = False : Exit For
                        If typeList.Count > 0 AndAlso (tid < 0 OrElse tid >= typeList.Count) Then allOk = False : Exit For
                    Next
                    If allOk Then
                        If oc > bestOc OrElse (oc = bestOc AndAlso Math.Abs(p - startPos) < bestDist) Then
                            bestOc = oc : bestP = p : bestPw = pw : bestDist = Math.Abs(p - startPos)
                        End If
                    End If
                    If Environment.GetEnvironmentVariable("UV_DBG") = "1" AndAlso p = 276 Then
                        Console.Error.WriteLine("DBG p276 pw=" & pw & " oc=" & oc & " allOk=" & allOk)
                    End If
                Next
            Next
            If bestP >= 0 Then Exit For
        Next
        If bestP >= 0 Then
            pathWidth = bestPw
            Return bestP
        End If
        If Environment.GetEnvironmentVariable("UV_DBG") = "1" Then
            Console.Error.WriteLine("DBG FindObjectTable -1: startPos=" & startPos & " endPos=" & endPos & " typeList.Count=" & typeList.Count & " bestOcSeen=" & bestOc)
        End If
        Return -1
    End Function

    ''' <summary>返回对象自身字节（位于当前文件/包内）。</summary>
    Public Function GetObjectBytes(obj As ObjectInfo) As Byte()
        If obj.Offset < 0 OrElse obj.Length <= 0 OrElse obj.Offset + obj.Length > RawBytes.Length Then
            If obj.Offset < 0 OrElse obj.Length <= 0 Then Return New Byte(-1) {}
            Dim maxLen = RawBytes.Length - obj.Offset
            If maxLen <= 0 Then Return New Byte(-1) {}
            Dim n = Math.Min(obj.Length, maxLen)
            Dim d(n - 1) As Byte
            Array.Copy(RawBytes, obj.Offset, d, 0, n)
            Return d
        End If
        Dim data(obj.Length - 1) As Byte
        Array.Copy(RawBytes, obj.Offset, data, 0, obj.Length)
        Return data
    End Function

    ''' <summary>返回定位到对象数据起始处的读取器（小端）。</summary>
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
    ''' <summary>
    ''' 读取对象名。实测（Unity 5.6.4p3, format 17, TypeTree 剥离）对象数据以
    ''' [int32 名字长度][名字字节] 开头（无 null 终止符），随后 4 字节对齐。
    ''' </summary>
    Public Shared Function ReadObjectName(r As EndianBinaryReader) As String
        Dim len = r.ReadInt32()
        If len < 0 OrElse len > 1024 OrElse r.Position + len > r.BaseStream.Length Then
            Return ""
        End If
        Dim s As String = ""
        If len > 0 Then
            Dim bytes = r.ReadBytes(len)
            s = System.Text.Encoding.UTF8.GetString(bytes)
        End If
        r.Align(4)
        Return s
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
