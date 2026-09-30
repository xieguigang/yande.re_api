Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Imaging

''' <summary>
''' Unity 纹理像素格式解码。非压缩格式（RGBA32/RGB24/等）可正确解码；
''' ETC1/ETC2 为尽力解码（基于常见 individual/differential 模式），请在 UI 中目视校验，
''' 若有偏差以原始数据（.bin）重建为准。
''' </summary>
Public Module TextureDecoders

    ' ETC 修正值表（8 张，每张 4 个有符号强度）
    Private ReadOnly ETC_MODIFIERS As Integer()() = {
        New Integer() {-8, -2, 2, 8},
        New Integer() {-17, -5, 5, 17},
        New Integer() {-29, -9, 9, 29},
        New Integer() {-42, -13, 13, 42},
        New Integer() {-60, -18, 18, 60},
        New Integer() {-80, -24, 24, 80},
        New Integer() {-106, -33, 33, 106},
        New Integer() {-183, -47, 47, 183}
    }

    ''' <summary>解码纹理为 32 位 RGBA 像素数组（长度 w*h*4）。不支持时返回 Nothing。</summary>
    Public Function DecodeToRGBA(format As Integer, w As Integer, h As Integer, data() As Byte) As Byte()
        If w <= 0 OrElse h <= 0 Then Return Nothing
        Select Case CType(format, TexFormat)
            Case TexFormat.Alpha8 : Return DecodeAlpha8(data, w, h)
            Case TexFormat.RGB24 : Return DecodeRGB24(data, w, h)
            Case TexFormat.RGBA32 : Return DecodeRGBA32(data, w, h)
            Case TexFormat.ARGB32 : Return DecodeARGB32(data, w, h)
            Case TexFormat.BGRA32 : Return DecodeBGRA32(data, w, h)
            Case TexFormat.RGB565 : Return DecodeRGB565(data, w, h)
            Case TexFormat.RGBA4444 : Return DecodeRGBA4444(data, w, h)
            Case TexFormat.ARGB4444 : Return DecodeARGB4444(data, w, h)
            Case TexFormat.ETC_RGB4 : Return DecodeETC(data, w, h, isEtc2:=False, hasAlpha:=False, alphaData:=Nothing)
            Case TexFormat.ETC2_RGB4 : Return DecodeETC(data, w, h, isEtc2:=True, hasAlpha:=False, alphaData:=Nothing)
            Case TexFormat.ETC2_RGBA1 : Return DecodeETC(data, w, h, isEtc2:=True, hasAlpha:=False, alphaData:=Nothing, alpha1:=True)
            Case TexFormat.ETC2_RGBA8 : Return DecodeETC2RGBA8(data, w, h)
            Case Else : Return Nothing
        End Select
    End Function

    Public Function ToBitmap(rgba() As Byte, w As Integer, h As Integer) As Bitmap
        If rgba Is Nothing Then Return Nothing
        Dim bmp As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim rect = New Rectangle(0, 0, w, h)
        Dim bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride = bmpData.Stride
            ' 构造 stride 对齐的 BGRA 缓冲（GDI+ 为 BGRA 布局）
            Dim buf(stride * h - 1) As Byte
            For y = 0 To h - 1
                For x = 0 To w - 1
                    Dim src = (y * w + x) * 4
                    Dim dst = y * stride + x * 4
                    buf(dst) = rgba(src + 2)      ' B
                    buf(dst + 1) = rgba(src + 1)  ' G
                    buf(dst + 2) = rgba(src)      ' R
                    buf(dst + 3) = rgba(src + 3)  ' A
                Next
            Next
            System.Runtime.InteropServices.Marshal.Copy(buf, 0, bmpData.Scan0, buf.Length)
        Finally
            bmp.UnlockBits(bmpData)
        End Try
        Return bmp
    End Function

    ' ===== 非压缩格式 =====
    Private Function DecodeAlpha8(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            out(i * 4) = 0 : out(i * 4 + 1) = 0 : out(i * 4 + 2) = 0
            out(i * 4 + 3) = d(i)
        Next
        Return out
    End Function

    Private Function DecodeRGB24(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            out(i * 4) = d(i * 3)
            out(i * 4 + 1) = d(i * 3 + 1)
            out(i * 4 + 2) = d(i * 3 + 2)
            out(i * 4 + 3) = 255
        Next
        Return out
    End Function

    Private Function DecodeRGBA32(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        Array.Copy(d, out, Math.Min(d.Length, out.Length))
        Return out
    End Function

    Private Function DecodeARGB32(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            out(i * 4) = d(i * 4 + 1)
            out(i * 4 + 1) = d(i * 4 + 2)
            out(i * 4 + 2) = d(i * 4 + 3)
            out(i * 4 + 3) = d(i * 4)
        Next
        Return out
    End Function

    Private Function DecodeBGRA32(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            out(i * 4) = d(i * 4 + 2)
            out(i * 4 + 1) = d(i * 4 + 1)
            out(i * 4 + 2) = d(i * 4)
            out(i * 4 + 3) = d(i * 4 + 3)
        Next
        Return out
    End Function

    Private Function DecodeRGB565(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            Dim v = CUShort(d(i * 2)) Or (CUShort(d(i * 2 + 1)) << 8)
            Dim r = ((v >> 11) And &H1F) * 255 \ 31
            Dim g = ((v >> 5) And &H3F) * 255 \ 63
            Dim b = (v And &H1F) * 255 \ 31
            out(i * 4) = CByte(r) : out(i * 4 + 1) = CByte(g) : out(i * 4 + 2) = CByte(b) : out(i * 4 + 3) = 255
        Next
        Return out
    End Function

    Private Function DecodeRGBA4444(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            Dim v = CUShort(d(i * 2)) Or (CUShort(d(i * 2 + 1)) << 8)
            out(i * 4) = CByte(((v >> 12) And &HF) * 255 \ 15)
            out(i * 4 + 1) = CByte(((v >> 8) And &HF) * 255 \ 15)
            out(i * 4 + 2) = CByte(((v >> 4) And &HF) * 255 \ 15)
            out(i * 4 + 3) = CByte((v And &HF) * 255 \ 15)
        Next
        Return out
    End Function

    Private Function DecodeARGB4444(d() As Byte, w As Integer, h As Integer) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        For i = 0 To w * h - 1
            Dim v = CUShort(d(i * 2)) Or (CUShort(d(i * 2 + 1)) << 8)
            Dim a = ((v >> 12) And &HF) * 255 \ 15
            Dim r = ((v >> 8) And &HF) * 255 \ 15
            Dim g = ((v >> 4) And &HF) * 255 \ 15
            Dim b = (v And &HF) * 255 \ 15
            out(i * 4) = CByte(r) : out(i * 4 + 1) = CByte(g) : out(i * 4 + 2) = CByte(b) : out(i * 4 + 3) = CByte(a)
        Next
        Return out
    End Function

    ' ===== ETC（尽力解码）=====
    Private Function DecodeETC(data() As Byte, w As Integer, h As Integer, isEtc2 As Boolean, hasAlpha As Boolean, alphaData As Byte(), Optional alpha1 As Boolean = False) As Byte()
        Dim out(w * h * 4 - 1) As Byte
        ' ETC 块大小为 4x4；数据按块排列（ETC2 RGB 每块 8 字节，ETC2 RGBA8 每块 16 字节含 Alpha 块）
        Dim blockBytes = If(hasAlpha, 16, 8)
        Dim bwBlocks = (w + 3) \ 4
        Dim bhBlocks = (h + 3) \ 4
        Dim pos = 0
        For by = 0 To bhBlocks - 1
            For bx = 0 To bwBlocks - 1
                Dim blockPos = pos
                ' Alpha 块（ETC2 RGBA8 的前 8 字节或单独处理）
                Dim alphaBlock As Byte() = Nothing
                Dim rgbBlock = blockPos
                If hasAlpha Then
                    alphaBlock = New Byte(7) {} : Array.Copy(data, blockPos, alphaBlock, 0, 8)
                End If
                Dim baseR(1) As Integer, baseG(1) As Integer, baseB(1) As Integer
                Dim cw(1) As Integer
                Dim flip As Integer
                ParseEtcBlock(data, rgbBlock, isEtc2, baseR, baseG, baseB, cw, flip)
                For y = 0 To 3
                    For x = 0 To 3
                        Dim px = bx * 4 + x
                        Dim py = by * 4 + y
                        If px >= w OrElse py >= h Then Continue For
                        Dim subIdx = If(flip = 0, If(x < 2, 0, 1), If(y < 2, 0, 1))
                        ' 像素码（2 bit）位于 data[rgbBlock+4 .. +7]
                        Dim pidx = y * 4 + x
                        Dim code = GetEtcPixelCode(data, rgbBlock + 4, pidx)
                        Dim m = ETC_MODIFIERS(cw(subIdx))(code)
                        Dim r = Clamp8(baseR(subIdx) + m)
                        Dim g = Clamp8(baseG(subIdx) + m)
                        Dim b = Clamp8(baseB(subIdx) + m)
                        Dim o = (py * w + px) * 4
                        out(o) = CByte(r) : out(o + 1) = CByte(g) : out(o + 2) = CByte(b)
                        If hasAlpha Then
                            out(o + 3) = CByte(Etc2Alpha(alphaBlock, pidx, alpha1))
                        Else
                            out(o + 3) = 255
                        End If
                    Next
                Next
                pos += blockBytes
            Next
        Next
        Return out
    End Function

    Private Function DecodeETC2RGBA8(data() As Byte, w As Integer, h As Integer) As Byte()
        ' 每块 16 字节：前 8 字节为 Alpha（EAC），后 8 字节为 RGB
        Dim out(w * h * 4 - 1) As Byte
        Dim bwBlocks = (w + 3) \ 4
        Dim bhBlocks = (h + 3) \ 4
        Dim pos = 0
        For by = 0 To bhBlocks - 1
            For bx = 0 To bwBlocks - 1
                Dim alphaBlock = pos
                Dim rgbBlock = pos + 8
                Dim baseR(1) As Integer, baseG(1) As Integer, baseB(1) As Integer
                Dim cw(1) As Integer, flip As Integer
                ParseEtcBlock(data, rgbBlock, True, baseR, baseG, baseB, cw, flip)
                For y = 0 To 3
                    For x = 0 To 3
                        Dim px = bx * 4 + x, py = by * 4 + y
                        If px >= w OrElse py >= h Then Continue For
                        Dim subIdx = If(flip = 0, If(x < 2, 0, 1), If(y < 2, 0, 1))
                        Dim pidx = y * 4 + x
                        Dim code = GetEtcPixelCode(data, rgbBlock + 4, pidx)
                        Dim m = ETC_MODIFIERS(cw(subIdx))(code)
                        Dim o = (py * w + px) * 4
                        out(o) = CByte(Clamp8(baseR(subIdx) + m))
                        out(o + 1) = CByte(Clamp8(baseG(subIdx) + m))
                        out(o + 2) = CByte(Clamp8(baseB(subIdx) + m))
                        out(o + 3) = CByte(Etc2Alpha(data, alphaBlock, False))
                    Next
                Next
                pos += 16
            Next
        Next
        Return out
    End Function

    Private Function Etc2Alpha(blockData() As Byte, blockPos As Integer, alpha1 As Boolean) As Integer
        ' 简化：读取 EAC alpha 块（8 字节）。完整 EAC 解码较复杂，这里返回基础近似（中间值），
        ' 真实 alpha 需要完整 EAC 解码，标记为尽力。
        If alpha1 Then
            ' 1-bit alpha（ETC2_RGBA1）：基于 RGB 块最低位，简化返回不透明
            Return 255
        End If
        ' EAC：取第一个 base code 近似
        Dim baseCode = (blockData(blockPos) >> 4) And &HF
        Return Clamp8(baseCode * 17)
    End Function

    Private Sub ParseEtcBlock(d() As Byte, off As Integer, isEtc2 As Boolean,
                              ByRef baseR() As Integer, ByRef baseG() As Integer, ByRef baseB() As Integer,
                              ByRef cw() As Integer, ByRef flip As Integer)
        Dim b0 = d(off), b1 = d(off + 1), b2 = d(off + 2), b3 = d(off + 3)
        Dim diff = (b0 And &H80) <> 0
        If diff Then
            Dim R = (b0 >> 3) And &H1F
            Dim G = ((b0 And &H7) << 2) Or ((b1 >> 6) And &H3)
            Dim B = (b1 >> 1) And &H1F
            Dim dR = SignExtend3((b2 >> 5) And &H7)
            Dim dG = SignExtend3((b2 >> 2) And &H7)
            Dim dB = SignExtend3(((b2 And &H3) << 1) Or ((b3 >> 7) And &H1))
            baseR(0) = Expand5(R + dR) : baseR(1) = Expand5(R - dR)
            baseG(0) = Expand5(G + dG) : baseG(1) = Expand5(G - dG)
            baseB(0) = Expand5(B + dB) : baseB(1) = Expand5(B - dB)
        Else
            Dim R = (b0 >> 3) And &HF
            Dim G = (((b0 And &H7) << 1) Or ((b1 >> 7) And &H1))
            Dim B = (b1 >> 3) And &HF
            baseR(0) = Expand4(R) : baseR(1) = Expand4(R)
            baseG(0) = Expand4(G) : baseG(1) = Expand4(G)
            baseB(0) = Expand4(B) : baseB(1) = Expand4(B)
        End If
        cw(0) = (b2 >> 2) And &H7
        cw(1) = (b3 >> 2) And &H7
        flip = (b3 >> 7) And &H1
    End Sub

    Private Function GetEtcPixelCode(d() As Byte, off As Integer, pidx As Integer) As Integer
        ' 32 位像素码（大端），取第 pidx 个 2-bit
        Dim bits = (CInt(d(off)) << 24) Or (CInt(d(off + 1)) << 16) Or (CInt(d(off + 2)) << 8) Or d(off + 3)
        Dim shift = 30 - (pidx * 2)
        Return (bits >> shift) And &H3
    End Function

    Private Function SignExtend3(v As Integer) As Integer
        If (v And &H4) <> 0 Then Return v Or &HFFFFFFF8
        Return v
    End Function

    Private Function Expand4(v As Integer) As Integer
        Return (v << 4) Or v
    End Function

    Private Function Expand5(v As Integer) As Integer
        Return (v << 3) Or (v >> 2)
    End Function

    Private Function Clamp8(v As Integer) As Integer
        If v < 0 Then Return 0
        If v > 255 Then Return 255
        Return v
    End Function

End Module
