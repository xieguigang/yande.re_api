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
        Dim r = entry.File.CreateReader(entry.Obj)
        Dim name = SerializedFile.ReadObjectName(r)
        entry.Name = If(name, entry.TypeName & " #" & entry.PathID)

        Dim len = r.ReadInt32()
        If len < 0 OrElse len > r.BaseStream.Length - r.Position Then len = CInt(r.BaseStream.Length - r.Position)
        Dim bytes = r.ReadBytes(len)

        Dim ext As String
        If entry.ClassID = ClassID_Shader Then
            ext = ".shader"
        ElseIf entry.ClassID = ClassID_MonoScript Then
            ext = ".cs"
        Else
            ext = DetectTextExtension(name)
        End If
        Dim outPath = UniquePath(outputDir, CategoryFolder(entry.Category), name, ext)
        File.WriteAllBytes(outPath, bytes)

        entry.PreviewText = TryDecodeText(bytes)
        entry.ExtractedPath = outPath
        entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", entry.TypeName),
            KV("字节数", bytes.Length.ToString())
        }
        Return True
    End Function

    ' ============ 纹理 / 贴图 ============
    Private Function ExtractTexture(entry As AssetEntry, outputDir As String) As Boolean
        Dim r = entry.File.CreateReader(entry.Obj)
        Dim name = SerializedFile.ReadObjectName(r)
        entry.Name = If(name, entry.TypeName & " #" & entry.PathID)

        Dim forced = r.ReadInt32()
        Dim downscale = r.ReadBoolean()
        Dim width = r.ReadInt32()
        Dim height = r.ReadInt32()
        Dim completeImageSize = r.ReadInt32()
        Dim texFormat = r.ReadInt32()
        Dim mipCount = r.ReadInt32()

        ' StreamedResource: m_Source(string) + m_Offset(int64) + m_Size(int64)
        Dim source = r.ReadAlignedString()
        Dim resOffset = r.ReadInt64()
        Dim resSize = r.ReadInt64()

        Dim colorSpace = r.ReadInt32()
        Dim imageCount = r.ReadInt32()
        Dim texDim = r.ReadInt32()

        ' GLTextureSettings (7 字段)
        r.ReadInt32() : r.ReadInt32() : r.ReadSingle() : r.ReadInt32() : r.ReadInt32() : r.ReadInt32() : r.ReadInt32()
        Dim lightmapFmt = r.ReadInt32()
        Dim colorSpace2 = r.ReadInt32()

        Dim dataSize = r.ReadInt32()
        Dim imageData = r.ReadBytes(If(dataSize > 0, dataSize, 0))

        Dim pixelData = imageData
        If source <> "" AndAlso resSize > 0 Then
            Dim ext = entry.File.GetExternalResource(source, resOffset, CInt(resSize))
            If ext IsNot Nothing Then pixelData = ext
        End If
        If pixelData Is Nothing OrElse pixelData.Length = 0 Then
            pixelData = r.ReadBytes(CInt(r.BaseStream.Length - r.Position))
        End If

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
            Dim bmp = TextureDecoders.ToBitmap(rgba, width, height)
            entry.PreviewBitmap = bmp
            Dim pngPath = UniquePath(outputDir, "Textures", name, ".png")
            bmp.Save(pngPath, ImageFormat.Png)
            bmp.Dispose()
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
    Private Function ExtractAnimation(entry As AssetEntry, outputDir As String) As Boolean
        Dim r = entry.File.CreateReader(entry.Obj)
        Dim name = SerializedFile.ReadObjectName(r)
        entry.Name = If(name, entry.TypeName & " #" & entry.PathID)

        Dim legacy = r.ReadInt32()
        Dim compressed = r.ReadInt32()
        Dim useHQ = r.ReadBoolean()

        Dim rotCount = r.ReadInt32()
        Dim rotTracks As New List(Of String)()
        For i = 0 To rotCount - 1
            Dim p = r.ReadPPtr()
            rotTracks.Add("rot#" & p.PathID)
            SkipAnimationCurve(r)
        Next
        Dim cRotCount = r.ReadInt32()
        For i = 0 To cRotCount - 1
            r.ReadPPtr() : SkipCompressedAnimationCurve(r)
        Next
        Dim eulerCount = r.ReadInt32()
        For i = 0 To eulerCount - 1
            r.ReadPPtr() : SkipAnimationCurve(r)
        Next
        Dim posCount = r.ReadInt32()
        Dim posTracks As New List(Of String)()
        For i = 0 To posCount - 1
            Dim p = r.ReadPPtr()
            posTracks.Add("pos#" & p.PathID)
            SkipAnimationCurve(r)
        Next
        Dim scaleCount = r.ReadInt32()
        For i = 0 To scaleCount - 1
            r.ReadPPtr() : SkipAnimationCurve(r)
        Next
        Dim floatCount = r.ReadInt32()
        For i = 0 To floatCount - 1
            r.ReadPPtr() : SkipAnimationCurve(r)
        Next
        Dim pptrCount = r.ReadInt32()
        For i = 0 To pptrCount - 1
            r.ReadPPtr() : SkipAnimationCurve(r)
        Next

        Dim sampleRate = r.ReadSingle()
        Dim wrapMode = r.ReadInt32()
        ' AABB / Bounds (6 floats)
        r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle()
        ' MuscleClips
        Dim muscleCount = r.ReadInt32()
        Dim duration = 0.0F
        For i = 0 To muscleCount - 1
            ' MuscleClip: m_ClipBlobSize + blob + m_StartFrame + m_StopFrame + m_Clip(duration, sampleRate, events...)
            Dim blobSize = r.ReadInt32()
            Dim blob = r.ReadBytes(blobSize)
            r.ReadSingle() ' startFrame
            r.ReadSingle() ' stopFrame
            duration = r.ReadSingle() ' m_Duration
            r.ReadSingle() ' m_SampleRate
            Dim evCount = r.ReadInt32()
            For e = 0 To evCount - 1
                r.ReadSingle() ' time
                r.ReadAlignedString() ' functionName
                r.ReadInt32() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle() : r.ReadSingle()
            Next
        Next

        Dim info As New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", "AnimationClip"),
            KV("时长(秒)", duration.ToString("F3")),
            KV("采样率", sampleRate.ToString("F2")),
            KV("WrapMode", wrapMode.ToString()),
            KV("旋转轨道", rotCount.ToString()),
            KV("位移轨道", posCount.ToString()),
            KV("缩放轨道", scaleCount.ToString()),
            KV("浮点轨道", floatCount.ToString()),
            KV("MuscleClip", muscleCount.ToString())
        }
        entry.StructuredInfo = info

        Dim sb As New StringBuilder()
        sb.AppendLine("{" & vbCrLf)
        sb.AppendLine("  ""name"": """ & JsonEscape(name) & """,")
        sb.AppendLine("  ""duration"": " & duration.ToString("F3") & ",")
        sb.AppendLine("  ""sampleRate"": " & sampleRate.ToString("F2") & ",")
        sb.AppendLine("  ""wrapMode"": " & wrapMode & ",")
        sb.AppendLine("  ""tracks"": { ""rotation"": " & rotCount & ", ""position"": " & posCount & ", ""scale"": " & scaleCount & ", ""float"": " & floatCount & " },")
        sb.AppendLine("  ""compressed"": " & compressed)
        sb.AppendLine("}")
        Dim jsonPath = UniquePath(outputDir, "Animations", name, ".json")
        File.WriteAllText(jsonPath, sb.ToString())
        entry.PreviewText = sb.ToString()
        entry.ExtractedPath = jsonPath
        Return True
    End Function

    ' ============ 音频 ============
    Private Function ExtractAudio(entry As AssetEntry, outputDir As String) As Boolean
        Dim r = entry.File.CreateReader(entry.Obj)
        Dim name = SerializedFile.ReadObjectName(r)
        entry.Name = If(name, entry.TypeName & " #" & entry.PathID)

        Dim forceMono = r.ReadInt32()
        Dim compression = r.ReadInt32()
        Dim loadType = r.ReadInt32()
        Dim quality = r.ReadInt32()
        Dim frequency = r.ReadInt32()
        Dim channels = r.ReadInt32()
        Dim lengthMs = r.ReadInt32()
        Dim chunkCount = r.ReadInt32()
        Dim chunkSize = r.ReadInt32()

        ' StreamedResource
        Dim source = r.ReadAlignedString()
        Dim resOffset = r.ReadInt64()
        Dim resSize = r.ReadInt64()

        Dim audioDataSize = r.ReadInt32()
        Dim audioData = r.ReadBytes(If(audioDataSize > 0, audioDataSize, 0))

        Dim bytes = audioData
        If source <> "" AndAlso resSize > 0 Then
            Dim ext = entry.File.GetExternalResource(source, resOffset, CInt(resSize))
            If ext IsNot Nothing Then bytes = ext
        End If

        entry.StructuredInfo = New List(Of KeyValuePair(Of String, String)) From {
            KV("类型", "AudioClip"),
            KV("格式", AudioFormatName(compression)),
            KV("频率", frequency.ToString() & " Hz"),
            KV("声道", channels.ToString()),
            KV("时长(ms)", lengthMs.ToString()),
            KV("外置资源", If(source <> "", source, "内联"))
        }

        If compression = 0 Then
            ' PCM：写出 WAV
            Dim wavPath = UniquePath(outputDir, "Audio", name, ".wav")
            WriteWav(wavPath, bytes, frequency, channels, 16)
            entry.ExtractedPath = wavPath
        Else
            ' 压缩（Vorbis/MP3/FSB 容器）：保存原始数据
            Dim rawPath = UniquePath(outputDir, "Audio", name & "_" & AudioFormatName(compression), ".bin")
            File.WriteAllBytes(rawPath, bytes)
            entry.ExtractedPath = rawPath
            entry.WasRawFallback = True
            entry.ErrorMessage = "音频为 " & AudioFormatName(compression) & " 压缩格式（FSB/Vorbis 容器），已保存原始数据用于重建；完整解码需额外解码库。"
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

End Module
