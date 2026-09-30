Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Text

''' <summary>
''' 按 classID 硬编码的 Unity 5.6 字段布局提取各类资源。
''' 每个提取器均容错：解析异常时回退为保存原始字节（.bin）。
''' </summary>
Public Module AssetExtractors

    Public Function Extract(entry As AssetEntry, outputDir As String) As Boolean
        Try
            Select Case entry.ClassID
                Case ClassID_TextAsset, ClassID_MonoScript, ClassID_Shader
                    Return ExtractText(entry, outputDir)
                Case ClassID_Texture2D, ClassID_Sprite
                    Return ExtractTexture(entry, outputDir)
                Case ClassID_Mesh
                    Return ExtractMesh(entry, outputDir)
                Case ClassID_AnimationClip
                    Return ExtractAnimation(entry, outputDir)
                Case ClassID_AudioClip
                    Return ExtractAudio(entry, outputDir)
                Case ClassID_Font
                    Return ExtractFont(entry, outputDir)
                Case Else
                    Return ExtractRaw(entry, outputDir)
            End Select
        Catch ex As Exception
            Try
                Return ExtractRaw(entry, outputDir, ex.Message)
            Catch
                entry.ErrorMessage = ex.Message
                Return False
            End Try
        End Try
    End Function

    ' ============ 文本 / 脚本 / Shader ============
    Private Function ExtractText(entry As AssetEntry, outputDir As String) As Boolean
        Dim objBytes = entry.File.GetObjectBytes(entry.Obj)
        If objBytes Is Nothing OrElse objBytes.Length < 4 Then
            Return ExtractRaw(entry, outputDir, "对象数据过短")
        End If

        Dim pos As Integer = 0
        Dim name = ReadLenString(objBytes, pos)
        entry.Name = If(name <> "", name, entry.TypeName & " #" & entry.PathID)

        ' MonoScript: 名字后为 [assemblyName][namespace][className] 三个长度前缀字符串
        If entry.ClassID = ClassID_MonoScript Then
            Dim sb As New System.Text.StringBuilder()
            sb.AppendLine("// MonoScript（程序集信息，用于重建脚本引用）")
            Dim fields = {"Assembly", "Namespace", "Class"}
            For Each f In fields
                Dim s = TryReadLenString(objBytes, pos)
                sb.AppendLine("// " & f & ": " & If(s, "(未能解析)"))
            Next
            Dim outPath = UniquePath(outputDir, CategoryFolder(entry.Category), name, ".cs.txt")
            File.WriteAllText(outPath, sb.ToString())
            entry.PreviewText = sb.ToString()
            entry.ExtractedPath = outPath
            entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
                KV("类型", "MonoScript"), KV("字节数", objBytes.Length.ToString())
            }
            Return True
        End If

        ' Shader: 压缩 blob，无法直接解码，保存原始字节
        If entry.ClassID = ClassID_Shader Then
            Dim rawPath = UniquePath(outputDir, CategoryFolder(entry.Category), name, ".shader.bin")
            File.WriteAllBytes(rawPath, objBytes)
            entry.ExtractedPath = rawPath
            entry.WasRawFallback = True
            entry.ErrorMessage = "Shader 为编译后压缩 blob，已保存原始字节用于重建。"
            entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
                KV("类型", "Shader"), KV("字节数", objBytes.Length.ToString())
            }
            Return True
        End If

        ' TextAsset: [名字][int len][文本字节]
        Dim len = ReadIntAt(objBytes, pos)
        Dim dataStart = pos + 4
        If len < 0 OrElse dataStart + len > objBytes.Length Then
            len = objBytes.Length - dataStart
            If len < 0 Then len = 0
        End If
        Dim bytes(len - 1) As Byte
        If len > 0 Then Array.Copy(objBytes, dataStart, bytes, 0, len)

        Dim ext = DetectTextExtension(name)
        Dim outPath2 = UniquePath(outputDir, CategoryFolder(entry.Category), name, ext)
        File.WriteAllBytes(outPath2, bytes)

        entry.PreviewText = TryDecodeText(bytes)
        entry.ExtractedPath = outPath2
        entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", entry.TypeName),
            KV("字节数", bytes.Length.ToString())
        }
        Return True
    End Function

    ' ============ 纹理 / 贴图 ============
    ''' <summary>
    ''' 实测布局（Unity 5.6.4p3, TypeTree 剥离）：
    ''' [名字(len+bytes)] [width][height][completeImageSize][format][mipCount]
    ''' [bool][bool+对齐][imageCount][dimension][GL设置6项]
    ''' [dataSize][数据][m_Source(len+bytes)][m_Offset(long)]（内联时）
    ''' 数据与外置资源通过扫描定位，避免对中间字段布局的脆弱依赖。
    ''' </summary>
    Private Function ExtractTexture(entry As AssetEntry, outputDir As String) As Boolean
        Dim objBytes = entry.File.GetObjectBytes(entry.Obj)
        If objBytes Is Nothing OrElse objBytes.Length < 24 Then
            Return ExtractRaw(entry, outputDir, "纹理对象数据过短")
        End If

        Dim pos As Integer = 0
        Dim name = ReadLenString(objBytes, pos)
        entry.Name = If(name <> "", name, entry.TypeName & " #" & entry.PathID)

        If pos + 20 > objBytes.Length Then
            Return ExtractRaw(entry, outputDir, "纹理头部不完整")
        End If
        Dim width = ReadIntAt(objBytes, pos)
        Dim height = ReadIntAt(objBytes, pos + 4)
        Dim completeImageSize = ReadIntAt(objBytes, pos + 8)
        Dim texFormat = ReadIntAt(objBytes, pos + 12)
        Dim mipCount = ReadIntAt(objBytes, pos + 16)
        pos += 20

        Dim valid = width > 0 AndAlso height > 0 AndAlso width <= 8192 AndAlso height <= 8192 AndAlso
                    completeImageSize >= 0 AndAlso completeImageSize <= width * height * 16
        If Not valid Then
            Return ExtractRaw(entry, outputDir, "纹理字段异常 " & width & "x" & height & " fmt=" & texFormat)
        End If

        Dim pixelData As Byte() = Nothing
        Dim source As String = ""
        Dim resOffset As Long = 0, resSize As Long = 0

        ' 内联数据：从尾部向前扫描 [dataSize == completeImageSize][数据]
        If completeImageSize > 0 Then
            pixelData = FindSizedBlock(objBytes, pos, completeImageSize)
        End If

        ' 外置资源：扫描路径字符串 + offset/size
        If pixelData Is Nothing Then
            Dim path As String = Nothing, off As Long = 0, sz As Long = 0
            If FindExternalResource(objBytes, pos, path, off, sz) Then
                source = path : resOffset = off : resSize = sz
                Dim ext = entry.File.GetExternalResource(source, resOffset, CInt(resSize))
                If ext IsNot Nothing Then pixelData = ext
            End If
        End If

        If pixelData Is Nothing Then pixelData = New Byte() {}

        entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", entry.TypeName),
            KV("尺寸", width & " x " & height),
            KV("格式", TexFormatName(texFormat) & " (" & texFormat & ")"),
            KV("mipCount", mipCount.ToString()),
            KV("外置资源", If(source <> "", source, "内联")),
            KV("数据字节", pixelData.Length.ToString())
        }

        Dim rgba = TextureDecoders.DecodeToRGBA(texFormat, width, height, pixelData)
        If rgba IsNot Nothing AndAlso width > 0 AndAlso height > 0 Then
            Dim pngPath = UniquePath(outputDir, "Textures", name, ".png")
            Using bmp = TextureDecoders.ToBitmap(rgba, width, height)
                bmp.Save(pngPath, ImageFormat.Png)
            End Using
            ' 不持有 Bitmap（GDI+ 位图体积大且释放后不可再绘制）；
            ' 预览时由 Form1 从 ExtractedPath 的 PNG 文件按需加载。
            entry.ExtractedPath = pngPath
        Else
            Dim rawPath = UniquePath(outputDir, "Textures", name & "_" & TexFormatName(texFormat).Replace(" "c, "_"c), ".bin")
            File.WriteAllBytes(rawPath, pixelData)
            entry.ExtractedPath = rawPath
            entry.ErrorMessage = "纹理格式 " & TexFormatName(texFormat) & " 暂未实现可视化解码；已保存原始数据，可用于重建。"
            entry.WasRawFallback = True
        End If
        Return True
    End Function

    ' ============ 网格 / 模型 ============
    Private Function ExtractMesh(entry As AssetEntry, outputDir As String) As Boolean
        Dim r = entry.File.CreateReader(entry.Obj)
        Dim name = SerializedFile.ReadObjectName(r)
        entry.Name = If(name, entry.TypeName & " #" & entry.PathID)

        ' 子网格
        Dim subMeshCount = r.ReadInt32()
        Dim totalIndices = 0
        For i = 0 To subMeshCount - 1
            Dim firstIndex = r.ReadInt32()
            Dim indexCount = r.ReadInt32()
            Dim topology = r.ReadInt32()
            Dim baseVertex = r.ReadInt32()
            Dim firstVertex = r.ReadInt32()
            Dim smVertexCount = r.ReadInt32()
            r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() ' AABB
            totalIndices += indexCount
        Next

        ' BlendShapes（尽力跳过）
        Dim shapeCount = r.ReadInt32()
        For i = 0 To shapeCount - 1
            r.ReadAlignedString() ' shape name
            Dim frameCount = r.ReadInt32()
            For f = 0 To frameCount - 1
                r.ReadSingle() ' weight
                Dim vc = r.ReadInt32()
                r.ReadBytes(vc * 3 * 4) ' deltaVertex
                r.ReadBytes(vc * 3 * 4) ' deltaNormal
                r.ReadBytes(vc * 4 * 4) ' deltaTangent
            Next
        Next

        ' BindPoses
        Dim bindPoseCount = r.ReadInt32()
        r.ReadBytes(bindPoseCount * 16 * 4)

        ' Bones（尽力跳过）
        Dim boneCount = r.ReadInt32()
        For i = 0 To boneCount - 1
            r.ReadPPtr()
        Next

        ' VertexData
        Dim vertexCount = r.ReadInt32()
        Dim channelCount = r.ReadInt32()
        Dim channels(channelCount - 1) As ChannelInfo
        For i = 0 To channelCount - 1
            channels(i).Stream = r.ReadInt32()
            channels(i).Offset = r.ReadInt32()
            channels(i).Stride = r.ReadInt32()
        Next
        Dim dataSize = r.ReadInt32()
        Dim vdata = r.ReadBytes(dataSize)

        ' IndexBuffer
        Dim idxSize = r.ReadInt32()
        Dim idata = r.ReadBytes(idxSize)

        ' 解析顶点
        Dim indexFormat = 2
        If idxSize = totalIndices * 4 Then indexFormat = 4
        Dim positions As New List(Of Vector3)()
        Dim normals As New List(Of Vector3)()
        Dim uvs As New List(Of Vector2)()
        Dim triangles As New List(Of Integer)()

        Dim posCh = GetChannel(channels, 0)
        Dim nrmCh = GetChannel(channels, 1)
        Dim uvCh = GetChannel(channels, 2)
        If posCh IsNot Nothing Then
            For v = 0 To vertexCount - 1
                positions.Add(ReadVec3(vdata, posCh.Value, v))
                If nrmCh IsNot Nothing Then normals.Add(ReadVec3(vdata, nrmCh.Value, v)) Else normals.Add(New Vector3(0, 0, 0))
                If uvCh IsNot Nothing Then uvs.Add(ReadVec2(vdata, uvCh.Value, v)) Else uvs.Add(New Vector2(0, 0))
            Next
        End If
        If indexFormat = 4 Then
            For i = 0 To idxSize \ 4 - 1
                triangles.Add(BitConverter.ToInt32(idata, i * 4))
            Next
        Else
            For i = 0 To idxSize \ 2 - 1
                triangles.Add(BitConverter.ToUInt16(idata, i * 2))
            Next
        End If

        Dim objPath = UniquePath(outputDir, "Models", name, ".obj")
        ModelExporter.WriteOBJ(objPath, positions, normals, uvs, triangles, name)
        entry.ExtractedPath = objPath

        entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", "Mesh"),
            KV("顶点数", vertexCount.ToString()),
            KV("三角形数", (triangles.Count \ 3).ToString()),
            KV("子网格数", subMeshCount.ToString()),
            KV("UV 通道", If(uvCh IsNot Nothing, "有", "无")),
            KV("法线通道", If(nrmCh IsNot Nothing, "有", "无"))
        }
        Return True
    End Function

    ' ============ 动画片段 ============
    ''' <summary>
    ''' 实测布局（Unity 5.6.4p3）：
    ''' [名字(len+对齐)][legacy(bool)][compressed(bool)][useHQ(bool)]+对齐
    ''' 后接各轨道组 { path(len+对齐), curve{ 关键帧(time,value,inSlope,outSlope 各float=16字节)[, pre/postInfinity] } }。
    ''' 逐组容错解析，始终输出 JSON 摘要 + 原始 clip 字节（用于重建导入）。
    ''' </summary>
    Private Function ExtractAnimation(entry As AssetEntry, outputDir As String) As Boolean
        Dim objBytes = entry.File.GetObjectBytes(entry.Obj)
        If objBytes Is Nothing OrElse objBytes.Length < 8 Then
            Return ExtractRaw(entry, outputDir, "动画对象数据过短")
        End If

        Dim pos As Integer = 0
        Dim name = ReadLenString(objBytes, pos)
        entry.Name = If(name <> "", name, "AnimationClip #" & entry.PathID)

        Dim legacy = False, compressed = False
        Dim rotCount = 0, eulerCount = 0, posCount = 0, scaleCount = 0, floatCount = 0, pptrCount = 0
        Dim sampleRate As Single = 0
        Dim tracks As New List(Of String)()
        Dim parseError As String = ""

        Try
            If pos + 3 > objBytes.Length Then Throw New InvalidDataException("头部不完整")
            legacy = objBytes(pos) <> 0
            compressed = objBytes(pos + 1) <> 0
            pos = (pos + 3 + 3) And Not 3

            rotCount = ReadCurveGroup(objBytes, pos, tracks, "rot", 56)     ' Quaternion 关键帧
            eulerCount = ReadCurveGroup(objBytes, pos, tracks, "euler", 44) ' Vector3 关键帧
            posCount = ReadCurveGroup(objBytes, pos, tracks, "pos", 44)
            scaleCount = ReadCurveGroup(objBytes, pos, tracks, "scale", 44)
            floatCount = ReadFloatCurveGroup(objBytes, pos, tracks)

            ' PPtrCurves: { curve{ keyCount, 关键帧×16字节(time+PPtr), pre, post }, path }
            pptrCount = ReadIntAt(objBytes, pos) : pos += 4
            If pptrCount < 0 OrElse pptrCount > 10000 Then Throw New InvalidDataException("PPtr 轨道数异常")
            For i = 0 To pptrCount - 1
                SkipPPtrCurve(objBytes, pos)
                If TryReadLenString(objBytes, pos) Is Nothing Then Throw New InvalidDataException("PPtr 路径")
            Next

            sampleRate = ReadFloatAt(objBytes, pos)
        Catch ex As Exception
            parseError = ex.Message
        End Try

        ' ---- JSON 摘要 ----
        Dim sb As New StringBuilder()
        sb.AppendLine("{")
        sb.AppendLine("  ""name"": """ & JsonEscape(name) & """,")
        sb.AppendLine("  ""legacy"": " & If(legacy, "true", "false") & ",")
        sb.AppendLine("  ""compressed"": " & If(compressed, "true", "false") & ",")
        sb.AppendLine("  ""sampleRate"": " & sampleRate.ToString("F2", Globalization.CultureInfo.InvariantCulture) & ",")
        sb.AppendLine("  ""trackCounts"": { ""rotation"": " & rotCount & ", ""euler"": " & eulerCount &
                      ", ""position"": " & posCount & ", ""scale"": " & scaleCount &
                      ", ""float"": " & floatCount & ", ""pptr"": " & pptrCount & " },")
        sb.AppendLine("  ""tracks"": [")
        Dim shown = Math.Min(tracks.Count, 200)
        For i = 0 To shown - 1
            sb.Append("    """ & JsonEscape(tracks(i)) & """" & If(i < shown - 1, ",", "")).AppendLine()
        Next
        sb.AppendLine("  ]")
        If parseError <> "" Then
            sb.AppendLine("  ,""parseNote"": """ & JsonEscape(parseError) & """")
        End If
        sb.AppendLine("}")
        Dim jsonPath = UniquePath(outputDir, "Animations", name, ".json")
        File.WriteAllText(jsonPath, sb.ToString())

        ' ---- 原始 clip 字节（用于重建导入）----
        Dim rawPath = UniquePath(outputDir, "Animations", name & "_clip", ".animbin")
        File.WriteAllBytes(rawPath, objBytes)

        entry.PreviewText = sb.ToString()
        entry.ExtractedPath = jsonPath
        entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", "AnimationClip"),
            KV("采样率", If(sampleRate > 0, sampleRate.ToString("F2") & " fps", "未知")),
            KV("旋转轨道", rotCount.ToString()),
            KV("位移轨道", posCount.ToString()),
            KV("缩放轨道", scaleCount.ToString()),
            KV("浮点轨道", floatCount.ToString()),
            KV("压缩", If(compressed, "是", "否"))
        }
        If parseError <> "" Then
            entry.ErrorMessage = "部分字段解析未完成（" & parseError & "）；已保存 JSON 摘要与原始 clip 数据。"
        End If
        Return True
    End Function

    ''' <summary>
    ''' 轨道组：{ curve{ keyCount, 关键帧×keyStride 字节, preInfinity, postInfinity }, path(len+对齐) }。
    ''' 实测曲线在前、path 在后。Vector3 关键帧 = 44 字节，Quaternion = 56 字节，float = 20 字节。
    ''' </summary>
    Private Function ReadCurveGroup(bytes() As Byte, ByRef pos As Integer, tracks As List(Of String), prefix As String, keyStride As Integer) As Integer
        Dim count = ReadIntAt(bytes, pos) : pos += 4
        If count < 0 OrElse count > 10000 Then Throw New InvalidDataException(prefix & " 轨道数异常")
        For i = 0 To count - 1
            Dim keyCount = ReadIntAt(bytes, pos) : pos += 4
            If keyCount < 0 OrElse keyCount > 200000 Then Throw New InvalidDataException(prefix & " 关键帧数异常")
            If pos + keyCount * keyStride + 8 > bytes.Length Then Throw New InvalidDataException(prefix & " 关键帧越界")
            pos += keyCount * keyStride
            pos += 8 ' preInfinity + postInfinity
            Dim path = TryReadLenString(bytes, pos)
            If path Is Nothing Then Throw New InvalidDataException(prefix & " 路径")
            If path <> "" Then tracks.Add(prefix & ": " & path)
        Next
        Return count
    End Function

    ''' <summary>浮点轨道组：{ curve{ keyCount, 关键帧×20, pre, post }, path, attribute, classID, script(PPtr) }。</summary>
    Private Function ReadFloatCurveGroup(bytes() As Byte, ByRef pos As Integer, tracks As List(Of String)) As Integer
        Dim count = ReadIntAt(bytes, pos) : pos += 4
        If count < 0 OrElse count > 10000 Then Throw New InvalidDataException("float 轨道数异常")
        For i = 0 To count - 1
            Dim keyCount = ReadIntAt(bytes, pos) : pos += 4
            If keyCount < 0 OrElse keyCount > 200000 Then Throw New InvalidDataException("float 关键帧数异常")
            If pos + keyCount * 20 + 8 > bytes.Length Then Throw New InvalidDataException("float 关键帧越界")
            pos += keyCount * 20 + 8
            Dim path = TryReadLenString(bytes, pos)
            Dim attr = TryReadLenString(bytes, pos)
            If path Is Nothing OrElse attr Is Nothing Then Throw New InvalidDataException("float 路径")
            If pos + 16 > bytes.Length Then Throw New InvalidDataException("float 轨道尾部越界")
            pos += 4  ' classID
            pos += 12 ' script PPtr
            If path <> "" Then tracks.Add("float: " & path & If(attr <> "", "." & attr, ""))
        Next
        Return count
    End Function

    ''' <summary>PPtr 轨道曲线：{ keyCount, 关键帧×16字节(time+PPtr), pre/postInfinity }（曲线在前，path 由调用方读取）。</summary>
    Private Sub SkipPPtrCurve(bytes() As Byte, ByRef pos As Integer)
        Dim keyCount = ReadIntAt(bytes, pos) : pos += 4
        If keyCount < 0 OrElse keyCount > 200000 Then Throw New InvalidDataException("PPtr 关键帧数异常")
        If pos + keyCount * 16 + 8 > bytes.Length Then Throw New InvalidDataException("PPtr 关键帧越界")
        pos += keyCount * 16 + 8
    End Sub

    ' ============ 音频 ============
    ''' <summary>
    ''' 实测布局（Unity 5.6.4p3）：[名字(len+bytes)]
    ''' [loadType][bool+对齐][channels][frequency][bits][length(float)][int][int][int]
    ''' [m_Source(len+bytes)]（null+对齐）[m_Offset(long)][m_Size(long)]。
    ''' 通过扫描定位外置资源路径与 offset/size，元数据扫描频率/声道。
    ''' </summary>
    Private Function ExtractAudio(entry As AssetEntry, outputDir As String) As Boolean
        Dim objBytes = entry.File.GetObjectBytes(entry.Obj)
        If objBytes Is Nothing OrElse objBytes.Length < 16 Then
            Return ExtractRaw(entry, outputDir, "音频对象数据过短")
        End If

        Dim pos As Integer = 0
        Dim name = ReadLenString(objBytes, pos)
        entry.Name = If(name <> "", name, entry.TypeName & " #" & entry.PathID)

        ' 扫描频率（8000..192000）与其前的声道数（1..8）
        Dim frequency = 0, channels = 0, lengthMs = 0
        Dim scanEnd = Math.Min(objBytes.Length - 8, pos + 64)
        Dim p = (pos + 3) And Not 3
        Do While p <= scanEnd
            Dim v = ReadIntAt(objBytes, p)
            If v >= 8000 AndAlso v <= 192000 Then
                Dim ch = If(p >= 4, ReadIntAt(objBytes, p - 4), 0)
                If ch >= 1 AndAlso ch <= 8 Then
                    channels = ch : frequency = v
                    ' 时长（float 秒）通常位于频率后 8 字节
                    If p + 12 <= objBytes.Length Then
                        Dim secs = ReadFloatAt(objBytes, p + 8)
                        If secs > 0 AndAlso secs < 3600 Then lengthMs = CInt(secs * 1000)
                    End If
                    Exit Do
                End If
            End If
            p += 4
        Loop

        ' 外置资源（FSB 数据通常在 .resource 中）
        Dim bytes As Byte() = Nothing
        Dim source As String = ""
        Dim path As String = Nothing, off As Long = 0, sz As Long = 0
        If FindExternalResource(objBytes, pos, path, off, sz) Then
            source = path
            Dim ext = entry.File.GetExternalResource(source, off, CInt(sz))
            If ext IsNot Nothing Then bytes = ext
        End If

        ' 内联数据兜底：取名字之后剩余的全部字节
        If bytes Is Nothing OrElse bytes.Length = 0 Then
            If pos < objBytes.Length Then
                Dim rest(objBytes.Length - pos - 1) As Byte
                Array.Copy(objBytes, pos, rest, 0, rest.Length)
                bytes = rest
            Else
                bytes = New Byte() {}
            End If
        End If

        entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", "AudioClip"),
            KV("频率", If(frequency > 0, frequency.ToString() & " Hz", "未知")),
            KV("声道", If(channels > 0, channels.ToString(), "未知")),
            KV("时长(ms)", If(lengthMs > 0, lengthMs.ToString(), "未知")),
            KV("外置资源", If(source <> "", source, "内联")),
            KV("数据字节", bytes.Length.ToString())
        }

        ' 判断是否为 FSB 容器（"FSB5"/"FSB4" 魔数）或压缩数据
        Dim isFsb = bytes.Length > 4 AndAlso bytes(0) = AscW("F"c) AndAlso bytes(1) = AscW("S"c) AndAlso bytes(2) = AscW("B"c)
        Dim isPcm = Not isFsb AndAlso frequency > 0 AndAlso channels > 0

        If isPcm Then
            Dim wavPath = UniquePath(outputDir, "Audio", name, ".wav")
            WriteWav(wavPath, bytes, frequency, channels, 16)
            entry.ExtractedPath = wavPath
        Else
            Dim extName = If(isFsb, "FSB", "RAW")
            Dim rawPath = UniquePath(outputDir, "Audio", name & "_" & extName, If(isFsb, ".fsb", ".bin"))
            File.WriteAllBytes(rawPath, bytes)
            entry.ExtractedPath = rawPath
            entry.WasRawFallback = True
            entry.ErrorMessage = "音频为 " & extName & " 压缩容器（Vorbis/FSB），已保存原始数据用于重建；完整解码需额外解码库。"
        End If
        Return True
    End Function

    ' ============ 字体 ============
    Private Function ExtractFont(entry As AssetEntry, outputDir As String) As Boolean
        Dim r = entry.File.CreateReader(entry.Obj)
        Dim name = SerializedFile.ReadObjectName(r)
        entry.Name = If(name, entry.TypeName & " #" & entry.PathID)
        ' Font 含 m_FontData(byte[]) 通常为 TTF/OTF
        ' 后续字段较多，直接回退原始字节并尽力寻找 TTF
        Dim remaining = r.ReadBytes(CInt(r.BaseStream.Length - r.Position))
        Dim outPath = UniquePath(outputDir, "Fonts", name, ".bin")
        File.WriteAllBytes(outPath, remaining)
        entry.ExtractedPath = outPath
        entry.WasRawFallback = True
        Return True
    End Function

    ' ============ 原始字节兜底 ============
    Private Function ExtractRaw(entry As AssetEntry, outputDir As String, Optional note As String = "") As Boolean
        Dim bytes = entry.File.GetObjectBytes(entry.Obj)
        Dim outPath = UniquePath(outputDir, "Others", entry.TypeName & "_" & entry.PathID, ".bin")
        File.WriteAllBytes(outPath, bytes)
        entry.ExtractedPath = outPath
        entry.WasRawFallback = True
        If note <> "" Then entry.ErrorMessage = note
        Return True
    End Function

    ' ============ 辅助 ============
    Private Function GetChannel(ch() As ChannelInfo, i As Integer) As ChannelInfo?
        If i >= 0 AndAlso i < ch.Length Then Return ch(i)
        Return Nothing
    End Function

    Private Function ReadVec3(data() As Byte, ch As ChannelInfo, v As Integer) As Vector3
        Dim off = ch.Offset + v * ch.Stride
        If off + 12 > data.Length Then Return New Vector3(0, 0, 0)
        Return New Vector3(BitConverter.ToSingle(data, off),
                           BitConverter.ToSingle(data, off + 4),
                           BitConverter.ToSingle(data, off + 8))
    End Function

    Private Function ReadVec2(data() As Byte, ch As ChannelInfo, v As Integer) As Vector2
        Dim off = ch.Offset + v * ch.Stride
        If off + 8 > data.Length Then Return New Vector2(0, 0)
        Return New Vector2(BitConverter.ToSingle(data, off),
                           BitConverter.ToSingle(data, off + 4))
    End Function

    Private Sub SkipAnimationCurve(r As EndianBinaryReader)
        ' AnimationCurve: int32 count; each: time(float), value(float), inTan(float), outTan(float), inMode(int), outMode(int)
        Dim count = r.ReadInt32()
        For i = 0 To count - 1
            r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle()
            r.ReadInt32() : r.ReadInt32()
        Next
    End Sub

    Private Sub SkipCompressedAnimationCurve(r As EndianBinaryReader)
        ' CompressedAnimationCurve: PPtr? + small blob
        Dim blobSize = r.ReadInt32()
        r.ReadBytes(blobSize)
    End Sub

    Private Sub WriteWav(path As String, pcm16() As Byte, freq As Integer, channels As Integer, bits As Integer)
        Using fs = New FileStream(path, FileMode.Create)
            Using bw = New BinaryWriter(fs)
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"))
                bw.Write(36 + pcm16.Length)
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"))
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "))
                bw.Write(16)
                bw.Write(CUShort(1)) ' PCM
                bw.Write(CUShort(channels))
                bw.Write(freq)
                bw.Write(freq * channels * bits \ 8)
                bw.Write(CUShort(channels * bits \ 8))
                bw.Write(CUShort(bits))
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"))
                bw.Write(pcm16.Length)
                bw.Write(pcm16)
            End Using
        End Using
    End Sub

    Private Function DetectTextExtension(name As String) As String
        If name Is Nothing Then Return ".txt"
        Dim lower = name.ToLower()
        If lower.EndsWith(".txt") OrElse lower.EndsWith(".json") OrElse lower.EndsWith(".xml") OrElse
           lower.EndsWith(".csv") OrElse lower.EndsWith(".html") OrElse lower.EndsWith(".csv") Then
            Return ""
        End If
        Return ".txt"
    End Function

    Private Function TryDecodeText(bytes() As Byte) As String
        ' 尝试 UTF-8，含大量不可打印字符则截断
        Dim s = System.Text.Encoding.UTF8.GetString(bytes)
        If s.Length > 200000 Then s = s.Substring(0, 200000)
        Return s
    End Function

    Private Function JsonEscape(s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "").Replace(vbLf, "\n").Replace(vbTab, "\t")
    End Function

    Private Function KV(k As String, v As String) As KeyValuePair(Of String, String)
        Return New KeyValuePair(Of String, String)(k, v)
    End Function

    Private Function UniquePath(baseDir As String, subDir As String, name As String, ext As String) As String
        Dim dir = Path.Combine(baseDir, subDir)
        Directory.CreateDirectory(dir)
        Dim base = Path.Combine(dir, SanitizeName(name) & ext)
        If Not File.Exists(base) Then Return base
        Dim i = 1
        While File.Exists(Path.Combine(dir, SanitizeName(name) & "_" & i & ext))
            i += 1
        End While
        Return Path.Combine(dir, SanitizeName(name) & "_" & i & ext)
    End Function

    Private Function SanitizeName(name As String) As String
        If String.IsNullOrWhiteSpace(name) Then Return "asset"
        Dim sb As New StringBuilder()
        For Each c In name
            If Char.IsLetterOrDigit(c) OrElse c = "_" OrElse c = "-" OrElse c = "." Then sb.Append(c) Else sb.Append("_"c)
        Next
        Dim s = sb.ToString()
        If s.Length > 120 Then s = s.Substring(0, 120)
        Return s
    End Function

    Private Structure ChannelInfo
        Public Stream As Integer
        Public Offset As Integer
        Public Stride As Integer
    End Structure

    ' ============ 对象字节扫描辅助（实测 Unity 5.6.4p3 布局） ============

    ''' <summary>
    ''' 读取长度前缀字符串并推进 pos。实测格式（Unity 5.6.4p3）：
    ''' [int32 len][len 字节] 之后 4 字节对齐，无 null 终止符。
    ''' </summary>
    Private Function ReadLenString(bytes() As Byte, ByRef pos As Integer) As String
        Dim s = TryReadLenString(bytes, pos)
        If s Is Nothing Then Return ""
        Return s
    End Function

    ''' <summary>尽力读取长度前缀字符串；非法时返回 Nothing 且不推进 pos。</summary>
    Private Function TryReadLenString(bytes() As Byte, ByRef pos As Integer) As String
        If pos + 4 > bytes.Length Then Return Nothing
        Dim len = ReadIntAt(bytes, pos)
        If len < 0 OrElse len > 4096 OrElse pos + 4 + len > bytes.Length Then Return Nothing
        Dim endPos = pos + 4 + len
        Dim b(len - 1) As Byte
        If len > 0 Then Array.Copy(bytes, pos + 4, b, 0, len)
        pos = (endPos + 3) And Not 3 ' 4 字节对齐
        If len = 0 Then Return ""
        Return System.Text.Encoding.UTF8.GetString(b)
    End Function

    Private Function ReadIntAt(bytes() As Byte, pos As Integer) As Integer
        If pos < 0 OrElse pos + 4 > bytes.Length Then Return 0
        Return BitConverter.ToInt32(bytes, pos)
    End Function

    Private Function ReadLongAt(bytes() As Byte, pos As Integer) As Long
        If pos < 0 OrElse pos + 8 > bytes.Length Then Return 0
        Return BitConverter.ToInt64(bytes, pos)
    End Function

    Private Function ReadFloatAt(bytes() As Byte, pos As Integer) As Single
        If pos < 0 OrElse pos + 4 > bytes.Length Then Return 0.0F
        Return BitConverter.ToSingle(bytes, pos)
    End Function

    ''' <summary>
    ''' 从尾部向前扫描 [int32 == dataSize][dataSize 字节] 的数据块（要求块后剩余字节很少）。
    ''' </summary>
    Private Function FindSizedBlock(bytes() As Byte, start As Integer, dataSize As Integer) As Byte()
        If dataSize <= 0 Then Return Nothing
        Dim p = (bytes.Length - 4) And Not 3
        Do While p >= start
            If ReadIntAt(bytes, p) = dataSize Then
                Dim blockStart = p + 4
                Dim blockEnd = blockStart + dataSize
                If blockEnd <= bytes.Length AndAlso bytes.Length - blockEnd <= 64 Then
                    Dim b(dataSize - 1) As Byte
                    Array.Copy(bytes, blockStart, b, 0, dataSize)
                    Return b
                End If
            End If
            p -= 4
        Loop
        Return Nothing
    End Function

    ''' <summary>
    ''' 扫描定位流式外部资源：[路径长度][可打印路径字符]，其后（可选 null+对齐）
    ''' 跟随 [offset(long)][size(long)]。路径须包含 "."。
    ''' </summary>
    Private Function FindExternalResource(bytes() As Byte, start As Integer, ByRef source As String, ByRef offset As Long, ByRef size As Long) As Boolean
        source = Nothing
        offset = 0 : size = 0
        Dim p = (start + 3) And Not 3
        Do While p + 8 <= bytes.Length
            Dim L = ReadIntAt(bytes, p)
            If L >= 4 AndAlso L <= 300 AndAlso p + 4 + L <= bytes.Length Then
                Dim okPath = True
                For i = 0 To L - 1
                    Dim c = bytes(p + 4 + i)
                    If c < 32 OrElse c > 126 Then okPath = False : Exit For
                Next
                If okPath Then
                    Dim path = System.Text.Encoding.ASCII.GetString(bytes, p + 4, L)
                    If path.Contains(".") Then
                        ' 路径串之后 4 字节对齐，随后为 [offset(long)][size(long)]
                        Dim q = (p + 4 + L + 3) And Not 3
                        If TryParseRes(bytes, q, offset, size) Then
                            source = path
                            Return True
                        End If
                    End If
                End If
            End If
            p += 4
        Loop
        Return False
    End Function

    Private Function TryParseRes(bytes() As Byte, q As Integer, ByRef offset As Long, ByRef size As Long) As Boolean
        If q + 16 > bytes.Length Then Return False
        Dim off = ReadLongAt(bytes, q)
        Dim sz = ReadLongAt(bytes, q + 8)
        If off >= 0 AndAlso off < 2147483647L AndAlso sz > 0 AndAlso sz < 536870912L Then
            offset = off : size = sz
            Return True
        End If
        Return False
    End Function

End Module
