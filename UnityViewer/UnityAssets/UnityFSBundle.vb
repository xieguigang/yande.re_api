Option Strict On
Option Explicit On

Imports System.IO
Imports System.Text

''' <summary>
''' UnityFS 资源包解析（Unity 5.x 资源包，签名 "UnityFS"）。
''' 资源包内的数据块经 LZ4/LZMA 压缩，解压后包含若干 SerializedFile（或其原始流）。
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

        Using ms = New MemoryStream(bytes)
            Using r = New EndianBinaryReader(ms, bigEndian:=False)
                Dim sig = r.ReadStringToNull()
                If sig <> "UnityFS" Then
                    Throw New InvalidDataException("不是 UnityFS 资源包（签名=" & sig & "）。")
                End If
                bundle.Version = r.ReadInt32()

                ' 版本字符串（可能带长度前缀噪声），以容错方式读取用于显示
                bundle.UnityVersion = ReadCleanString(bytes, CInt(r.Position))
                bundle.UnityRevision = ReadCleanString(bytes, CInt(r.Position))
            End Using
        End Using

        ' ---- 自动定位头部字段块（size / compBI / uncompBI / flags）----
        Dim headerFieldOffset = FindHeaderFieldOffset(bytes)
        If headerFieldOffset < 0 Then
            Throw New InvalidDataException("无法定位 UnityFS 头部字段块。")
        End If

        Dim size = BitConverter.ToInt64(bytes, headerFieldOffset)
        Dim compBI = BitConverter.ToInt32(bytes, headerFieldOffset + 8)
        Dim uncompBI = BitConverter.ToInt32(bytes, headerFieldOffset + 12)
        bundle.Flags = BitConverter.ToInt32(bytes, headerFieldOffset + 16)
        bundle.Compression = bundle.Flags And &H3F

        ' ---- 解压 BlocksInfo（使用全局压缩方式）----
        Dim compData(compBI - 1) As Byte
        Array.Copy(bytes, headerFieldOffset + 20, compData, 0, compBI)
        Dim biBytes = DecompressBlock(compData, uncompBI, bundle.Compression)

        ' ---- 解析 BlocksInfo（大端）----
        Using ms = New MemoryStream(biBytes)
            Using r = New EndianBinaryReader(ms, bigEndian:=True)
                r.ReadBytes(16) ' uncompressed data hash
                Dim blocksCount = r.ReadInt32()
                For i = 0 To blocksCount - 1
                    Dim b As New BundleBlock()
                    b.CompressedSize = r.ReadInt32()
                    b.UncompressedSize = r.ReadInt32()
                    b.Flags = r.ReadInt16()
                    bundle.Blocks.Add(b)
                Next
                Dim dirCount = r.ReadInt32()
                For i = 0 To dirCount - 1
                    Dim e As New BundleEntry()
                    e.Offset = r.ReadInt64()
                    e.Size = r.ReadInt32()
                    e.Flags = r.ReadInt32()
                    e.Name = r.ReadStringToNull()
                    bundle.Entries.Add(e)
                Next
            End Using
        End Using

        ' ---- 解压所有数据块并拼接（块数据区从 BlocksInfo 之后顺序排布）----
        Dim dataStart = headerFieldOffset + 20 + compBI
        Dim pos = dataStart
        Using outMs = New MemoryStream()
            For Each b In bundle.Blocks
                Dim cb(b.CompressedSize - 1) As Byte
                Array.Copy(bytes, pos, cb, 0, b.CompressedSize)
                pos += b.CompressedSize
                Dim ub = DecompressBlock(cb, b.UncompressedSize, b.Flags)
                outMs.Write(ub, 0, ub.Length)
            Next
            bundle.ArchiveData = outMs.ToArray()
        End Using

        ' 回填每个目录项的实际字节
        For Each e In bundle.Entries
            If e.Offset + e.Size <= bundle.ArchiveData.Length Then
                Dim data(e.Size - 1) As Byte
                Array.Copy(bundle.ArchiveData, e.Offset, data, 0, e.Size)
                e.Data = data
            End If
        Next

        Return bundle
    End Function

    Private Shared Function FindHeaderFieldOffset(bytes() As Byte) As Integer
        For p = 8 To Math.Min(bytes.Length - 40, 200)
            Dim compBI = BitConverter.ToInt32(bytes, p + 8)
            Dim uncompBI = BitConverter.ToInt32(bytes, p + 12)
            Dim flags = BitConverter.ToInt32(bytes, p + 16)
            If compBI <= 0 OrElse compBI >= bytes.Length Then Continue For
            If uncompBI <= 0 OrElse uncompBI > 200000000 Then Continue For
            If (flags And &H3F) > 4 Then Continue For
            If compBI > uncompBI + 64 Then Continue For
            If p + 20 + compBI > bytes.Length Then Continue For

            Try
                Dim cd(compBI - 1) As Byte
                Array.Copy(bytes, p + 20, cd, 0, compBI)
                Dim decomp = DecompressBlock(cd, uncompBI, flags)
                If decomp.Length < 20 Then Continue For
                ' BlocksInfo: 16 字节 hash + int32 blocksCount（大端，合理小值）
                Dim bc = (CInt(decomp(16)) << 24) Or (CInt(decomp(17)) << 16) Or
                         (CInt(decomp(18)) << 8) Or decomp(19)
                If bc <= 0 OrElse bc > 2000 Then Continue For
                If ContainsAscii(decomp, "CAB-") OrElse bc < 64 Then
                    Return p
                End If
            Catch
                ' 忽略，继续尝试
            End Try
        Next
        Return -1
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

    Private Shared Function ContainsAscii(bytes() As Byte, needle As String) As Boolean
        Dim n = Encoding.ASCII.GetBytes(needle)
        If n.Length = 0 OrElse bytes.Length < n.Length Then Return False
        For i = 0 To bytes.Length - n.Length
            Dim ok = True
            For j = 0 To n.Length - 1
                If bytes(i + j) <> n(j) Then ok = False : Exit For
            Next
            If ok Then Return True
        Next
        Return False
    End Function

    ''' <summary>从 offset 处读取一个以 null 结尾的字符串并去掉前导不可打印字符（用于容错读取版本字符串）。</summary>
    Private Shared Function ReadCleanString(bytes() As Byte, offset As Integer) As String
        Dim i = offset
        While i < bytes.Length AndAlso (bytes(i) < 32 OrElse bytes(i) > 126)
            i += 1
        End While
        If i >= bytes.Length Then Return ""
        Dim sb As New StringBuilder()
        While i < bytes.Length AndAlso bytes(i) <> 0
            If bytes(i) >= 32 AndAlso bytes(i) <= 126 Then sb.Append(ChrW(bytes(i)))
            i += 1
        End While
        Return sb.ToString()
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
    Public Size As Integer
    Public Flags As Integer
    Public Data As Byte()            ' 解压后回填的实际字节
End Class
