Option Strict On
Option Explicit On

Imports System.IO
Imports System.Text

''' <summary>
''' 端序可切换的二进制读取器，用于解析 Unity 资源文件。
''' SerializedFile 头部字段为大端（BigEndian），其后的元数据与对象数据通常为小端；
''' UnityFS 的 BlocksInfo 为大端。通过 BigEndian 属性切换。
''' </summary>
Public Class EndianBinaryReader
    Implements IDisposable

    Private _reader As BinaryReader
    Private _stream As Stream

    Public Property BigEndian As Boolean = False

    Public Sub New(stream As Stream, Optional bigEndian As Boolean = False)
        _stream = stream
        _reader = New BinaryReader(stream, Encoding.UTF8, leaveOpen:=True)
        Me.BigEndian = bigEndian
    End Sub

    Public ReadOnly Property BaseStream As Stream
        Get
            Return _stream
        End Get
    End Property

    Public Property Position As Long
        Get
            Return _stream.Position
        End Get
        Set(value As Long)
            _stream.Position = value
        End Set
    End Property

    Public ReadOnly Property Length As Long
        Get
            Return _stream.Length
        End Get
    End Property

    Public ReadOnly Property EndOfStream As Boolean
        Get
            Return _stream.Position >= _stream.Length
        End Get
    End Property

    Public Function ReadByte() As Integer
        Return _reader.ReadByte()
    End Function

    Public Function ReadBytes(count As Integer) As Byte()
        If count <= 0 Then Return New Byte(-1) {}
        Return _reader.ReadBytes(count)
    End Function

    Public Function ReadSBytes(count As Integer) As SByte()
        Dim b = ReadBytes(count)
        Dim r(count - 1) As SByte
        For i = 0 To count - 1
            r(i) = CSByte(b(i))
        Next
        Return r
    End Function

    Public Function ReadBoolean() As Boolean
        Return _reader.ReadByte() <> 0
    End Function

    Public Function ReadInt16() As Short
        Dim b = _reader.ReadBytes(2)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToInt16(b, 0)
    End Function

    Public Function ReadUInt16() As UShort
        Dim b = _reader.ReadBytes(2)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToUInt16(b, 0)
    End Function

    Public Function ReadInt32() As Integer
        Dim b = _reader.ReadBytes(4)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToInt32(b, 0)
    End Function

    Public Function ReadUInt32() As UInteger
        Dim b = _reader.ReadBytes(4)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToUInt32(b, 0)
    End Function

    Public Function ReadInt64() As Long
        Dim b = _reader.ReadBytes(8)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToInt64(b, 0)
    End Function

    Public Function ReadUInt64() As ULong
        Dim b = _reader.ReadBytes(8)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToUInt64(b, 0)
    End Function

    Public Function ReadSingle() As Single
        Dim b = _reader.ReadBytes(4)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToSingle(b, 0)
    End Function

    Public Function ReadDouble() As Double
        Dim b = _reader.ReadBytes(8)
        If BigEndian Then Array.Reverse(b)
        Return BitConverter.ToDouble(b, 0)
    End Function

    ''' <summary>将当前位置对齐到 alignment 字节边界。</summary>
    Public Sub Align(alignment As Integer)
        If alignment <= 1 Then Return
        Dim pos = _stream.Position
        Dim rema = pos Mod alignment
        If rema <> 0 Then _stream.Position = pos + (alignment - rema)
    End Sub

    ''' <summary>读取以 null 结尾的 C 风格字符串（不消费末尾的 null 之后的内容，仅消费到 null）。</summary>
    Public Function ReadStringToNull(Optional maxLength As Integer = 8192) As String
        Dim sb As New StringBuilder()
        Dim b As Integer
        Dim n As Integer = 0
        Do
            b = _reader.ReadByte()
            If b <= 0 Then Exit Do
            n += 1
            If n > maxLength Then Exit Do
            sb.Append(ChrW(b))
        Loop
        Return sb.ToString()
    End Function

    ''' <summary>
    ''' 读取 Unity 的“对齐字符串”：int32 长度 = -(字符数+1)，随后字符、null 终止符，再 4 字节对齐。
    ''' 若为老格式（正数长度、无 null、无对齐）也能兼容。
    ''' </summary>
    Public Function ReadAlignedString() As String
        Dim len = ReadInt32()
        If len = 0 Then Return ""
        If len < 0 Then
            Dim count = -len - 1
            If count < 0 OrElse count > 100000000 Then
                _stream.Position -= 4
                Return ""
            End If
            Dim chars = _reader.ReadChars(count)
            _reader.ReadByte() ' null
            Align(4)
            Return New String(chars)
        Else
            ' 正数长度（老格式）：直接读取指定数量字符，无 null、无对齐
            If len > 100000000 Then
                _stream.Position -= 4
                Return ""
            End If
            Dim chars = _reader.ReadChars(len)
            Return New String(chars)
        End If
    End Function

    ''' <summary>跳过 Unity 对齐字符串。</summary>
    Public Sub SkipAlignedString()
        Dim len = ReadInt32()
        If len = 0 Then Return
        If len < 0 Then
            Dim count = -len - 1
            If count < 0 OrElse count > 100000000 Then
                _stream.Position -= 4
                Return
            End If
            _stream.Position += count + 1
            Align(4)
        Else
            If len > 100000000 Then
                _stream.Position -= 4
                Return
            End If
            _stream.Position += len
        End If
    End Sub

    ''' <summary>读取 Unity PPtr&lt;T&gt;：int32 fileID 后接 int64 pathID（format >= 14）。</summary>
    Public Function ReadPPtr() As PPtr
        Dim fileID = ReadInt32()
        Dim pathID = ReadInt64()
        Return New PPtr(fileID, pathID)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _reader IsNot Nothing Then _reader.Dispose()
        _reader = Nothing
    End Sub
End Class

''' <summary>Unity PPtr&lt;T&gt; 引用（文件 ID + 对象 pathID）。</summary>
Public Structure PPtr
    Public FileID As Integer
    Public PathID As Long
    Public Sub New(f As Integer, p As Long)
        FileID = f
        PathID = p
    End Sub
    Public ReadOnly Property IsValid As Boolean
        Get
            Return PathID <> 0
        End Get
    End Property
End Structure
