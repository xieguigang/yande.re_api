Option Strict On
Option Explicit On

Imports System.IO
Imports System.Text

''' <summary>
''' UnityFS 资源包解析（Unity 5.x 资源包，签名 "UnityFS"，版本 6）。
''' 头部与 BlocksInfo 均为【大端】；数据块按各自 flags 低 6 位选择 LZMA/LZ4 解压。
''' flags 0x40 = BlocksInfo 位于文件末尾；0x80 = 数据区起始需对齐填充。
''' </summary>
Public Class UnityFSBundle

    Public Property Version As Integer = 0
    Public Property UnityVersion As String = ""
    Public Property UnityRevision As String = ""
    Public Property Flags As Integer = 0
    Public Property Compression As Integer = 0

    Public Property Blocks As List(Of BundleBlock)
    Public Property Entries As List(Of BundleEntry)

    ''' <summary>所有块解压后拼接而成的完整数据区，目录项按 offset/size 索引此数组。</summary>
    Public Property ArchiveData As Byte()

    Public Shared Function Load(bytes() As Byte) As UnityFSBundle
        Dim bundle As New UnityFSBundle()
        bundle.Blocks = New List(Of BundleBlock)()
        bundle.Entries = New List(Of BundleEntry)()

        ' ---- 头部（大端）----
        Dim pos As Integer = 0
        Dim sig = ReadCString(bytes, pos)
        If sig <> "UnityFS" Then
            Throw New InvalidDataException("不是 UnityFS 资源包（签名=" & sig & "）。")
        End If
        bundle.Version = ReadInt32BE(bytes, pos)
        bundle.UnityVersion = ReadCString(bytes, pos)
        bundle.UnityRevision = ReadCString(bytes, pos)
        ReadInt64BE(bytes, pos)                       ' 包总大小
        Dim compBI = ReadInt32BE(bytes, pos)
        Dim uncompBI = ReadInt32BE(bytes, pos)
        bundle.Flags = ReadInt32BE(bytes, pos)
        bundle.Compression = bundle.Flags And &H3F

        Dim headerEnd = pos
        If compBI <= 0 OrElse uncompBI <= 0 OrElse headerEnd + Math.Max(compBI, 0) > bytes.Length Then
            Throw New InvalidDataException("UnityFS 头部字段异常。")
        End If

        ' ---- 读取并解压 BlocksInfo（实测：某些 5.6 写包器虽置 0x40 位，
        '      BlocksInfo 实际仍紧随头部；故两个位置都尝试并严格校验）----
        Dim infoBytes As Byte() = Nothing
        Dim dataStart As Integer = 0
        For Each tryPos In New Integer() {headerEnd, bytes.Length - compBI}
            If tryPos < headerEnd OrElse tryPos + compBI > bytes.Length Then Continue For
            Dim cd(compBI - 1) As Byte
            Array.Copy(bytes, tryPos, cd, 0, compBI)
            Dim cand As Byte() = Nothing
            Try
                cand = DecompressBlock(cd, uncompBI, bundle.Compression)
            Catch
                Continue For
            End Try
            If ValidateBlocksInfo(cand, bytes.Length, headerEnd, compBI, tryPos = headerEnd) Then
                infoBytes = cand
                dataStart = If(tryPos = headerEnd, headerEnd + compBI, headerEnd)
                Exit For
            End If
        Next
        If infoBytes Is Nothing Then
            Throw New InvalidDataException("无法定位/解压 UnityFS BlocksInfo。")
        End If

        ' ---- 解析 BlocksInfo（大端）----
        Dim bi As Integer = 0
        bi += 16 ' 未压缩数据 hash
        Dim blocksCount = ReadInt32BE(infoBytes, bi)
        If blocksCount < 0 OrElse blocksCount > 100000 Then
            Throw New InvalidDataException("数据块数量异常: " & blocksCount)
        End If
        For i = 0 To blocksCount - 1
            Dim b As New BundleBlock()
            b.CompressedSize = ReadInt32BE(infoBytes, bi)
            b.UncompressedSize = ReadInt32BE(infoBytes, bi)
            b.Flags = ReadInt16BE(infoBytes, bi)
            bundle.Blocks.Add(b)
        Next
        Dim dirCount = ReadInt32BE(infoBytes, bi)
        If dirCount < 0 OrElse dirCount > 100000 Then
            Throw New InvalidDataException("目录项数量异常: " & dirCount)
        End If
        For i = 0 To dirCount - 1
            Dim e As New BundleEntry()
            e.Offset = ReadInt64BE(infoBytes, bi)
            e.Size = ReadInt64BE(infoBytes, bi)
            e.Flags = ReadInt32BE(infoBytes, bi)
            e.Name = ReadCString(infoBytes, bi)
            bundle.Entries.Add(e)
        Next

        ' ---- 解压所有数据块并拼接 ----
        Dim dataStart = headerEnd
        If (bundle.Flags And &H40) = 0 Then dataStart = headerEnd + compBI
        If (bundle.Flags And &H80) <> 0 Then dataStart = (dataStart + 15) And Not 15

        Dim pos2 = dataStart
        Using outMs = New MemoryStream()
            For Each b In bundle.Blocks
                If pos2 + b.CompressedSize > bytes.Length Then
                    Exit For
                End If
                Dim cb(b.CompressedSize - 1) As Byte
                Array.Copy(bytes, pos2, cb, 0, b.CompressedSize)
                pos2 += b.CompressedSize
                Dim ub = DecompressBlock(cb, b.UncompressedSize, b.Flags)
                outMs.Write(ub, 0, ub.Length)
            Next
            bundle.ArchiveData = outMs.ToArray()
        End Using

        ' 回填每个目录项的实际字节
        For Each e In bundle.Entries
            If e.Offset >= 0 AndAlso e.Size > 0 AndAlso e.Offset + e.Size <= bundle.ArchiveData.Length Then
                Dim data(CInt(e.Size) - 1) As Byte
                Array.Copy(bundle.ArchiveData, e.Offset, data, 0, CInt(e.Size))
                e.Data = data
            End If
        Next

        Return bundle
    End Function

    ' ---- 大端原始读取 ----
    Private Shared Function ReadInt32BE(bytes() As Byte, ByRef pos As Integer) As Integer
        Dim v = (CInt(bytes(pos)) << 24) Or (CInt(bytes(pos + 1)) << 16) Or (CInt(bytes(pos + 2)) << 8) Or bytes(pos + 3)
        pos += 4
        Return v
    End Function

    Private Shared Function ReadInt16BE(bytes() As Byte, ByRef pos As Integer) As Short
        Dim v = CUShort((CInt(bytes(pos)) << 8) Or bytes(pos + 1))
        pos += 2
        Return CShort(v)
    End Function

    Private Shared Function ReadInt64BE(bytes() As Byte, ByRef pos As Integer) As Long
        Dim hi As Long = ReadInt32BE(bytes, pos)
        Dim lo As Long = CUInt(ReadInt32BE(bytes, pos) And &HFFFFFFFFL)
        Return (hi << 32) Or lo
    End Function

    Private Shared Function ReadCString(bytes() As Byte, ByRef pos As Integer) As String
        Dim start = pos
        Do While pos < bytes.Length AndAlso bytes(pos) <> 0
            pos += 1
        Loop
        Dim s = Encoding.UTF8.GetString(bytes, start, pos - start)
        If pos < bytes.Length Then pos += 1 ' 跳过 null
        Return s
    End Function

    ''' <summary>块解压：compression 取自该块/全局 flags 的低 6 位。</summary>
    Private Shared Function DecompressBlock(compBytes() As Byte, uncompSize As Integer, compression As Integer) As Byte()
        Select Case compression And &H3F
            Case 0 ' 不压缩
                Return compBytes
            Case 1 ' LZMA
                Dim ms As New MemoryStream()
                ms.Write(compBytes, 0, 5) ' props + dictsize
                Dim sizeBuf = BitConverter.GetBytes(CLng(uncompSize))
                ms.Write(sizeBuf, 0, 8)
                ms.Write(compBytes, 5, compBytes.Length - 5)
                ms.Position = 0
                Return LZ77Stream.Lzma.Decompress(ms)
            Case 2, 3 ' LZ4 / LZ4HC
                Return LZ77Stream.Lz4.DecodeBlock(compBytes, uncompSize)
            Case Else
                Throw New InvalidDataException("不支持的块压缩方式: " & (compression And &H3F).ToString())
        End Select
    End Function

    ''' <summary>按名称查找包内目录项（用于解析 externals 引用）。</summary>
    Public Function GetEntry(name As String) As BundleEntry
        For Each e In Entries
            If String.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) Then Return e
            ' 也尝试去掉路径前缀比较
            Dim leaf = e.Name
            Dim idx = leaf.LastIndexOf("/"c)
            If idx >= 0 Then leaf = leaf.Substring(idx + 1)
            If String.Equals(leaf, name, StringComparison.OrdinalIgnoreCase) Then Return e
        Next
        Return Nothing
    End Function

End Class

''' <summary>UnityFS 数据块描述。</summary>
Public Class BundleBlock
    Public CompressedSize As Integer
    Public UncompressedSize As Integer
    Public Flags As Integer          ' 低 6 位为压缩方式
    Public ReadOnly Property Compression As Integer
        Get
            Return Flags And &H3F
        End Get
    End Property
End Class

''' <summary>UnityFS 目录项（包内文件，通常是 SerializedFile 或流数据）。</summary>
Public Class BundleEntry
    Public Name As String = ""
    Public Offset As Long
    Public Size As Long
    Public Flags As Integer
    Public Data As Byte()            ' 解压后回填的实际字节
End Class
