Option Strict On
Option Explicit On

''' <summary>
''' Unity 资源类型常量、classID 映射与纹理/音频格式枚举。
''' 注意：Release 构建剥离了 TypeTree，因此对象数据必须按 classID 硬编码布局读取。
''' </summary>
Public Module AssetTypeLayouts

    ' ===== 关键 classID（Unity 5.x / 2017 稳定值）=====
    Public Const ClassID_TextAsset As Integer = 49
    Public Const ClassID_Texture2D As Integer = 28
    Public Const ClassID_Sprite As Integer = 213
    Public Const ClassID_AudioClip As Integer = 83
    Public Const ClassID_Mesh As Integer = 43
    Public Const ClassID_AnimationClip As Integer = 74
    Public Const ClassID_MonoScript As Integer = 115
    Public Const ClassID_Shader As Integer = 48
    Public Const ClassID_Material As Integer = 21
    Public Const ClassID_Font As Integer = 128
    Public Const ClassID_Animator As Integer = 95
    Public Const ClassID_AnimatorController As Integer = 137
    Public Const ClassID_Cubemap As Integer = 89
    Public Const ClassID_LightmapParameters As Integer = 147

    ''' <summary>将 classID 映射为可读类型名。</summary>
    Public Function ClassIDToName(classID As Integer) As String
        Select Case classID
            Case 1 : Return "GameObject"
            Case 2 : Return "Transform"
            Case 3 : Return "Camera"
            Case 4 : Return "Material"
            Case 5 : Return "MeshRenderer"
            Case 21 : Return "Material"
            Case 28 : Return "Texture2D"
            Case 43 : Return "Mesh"
            Case 48 : Return "Shader"
            Case 49 : Return "TextAsset"
            Case 74 : Return "AnimationClip"
            Case 83 : Return "AudioClip"
            Case 89 : Return "Cubemap"
            Case 95 : Return "Animator"
            Case 115 : Return "MonoScript"
            Case 128 : Return "Font"
            Case 137 : Return "AnimatorController"
            Case 147 : Return "LightmapParameters"
            Case 213 : Return "Sprite"
            Case Else : Return "Class" & classID.ToString()
        End Select
    End Function

    ''' <summary>判断 classID 是否为计划提取的目标资源类型。</summary>
    Public Function IsExtractableType(classID As Integer) As Boolean
        Select Case classID
            Case ClassID_TextAsset, ClassID_Texture2D, ClassID_Sprite,
                 ClassID_AudioClip, ClassID_Mesh, ClassID_AnimationClip,
                 ClassID_MonoScript, ClassID_Shader, ClassID_Font
                Return True
            Case Else
                Return False
        End Select
    End Function

    ' ===== 资源大类（用于 Form1 分组与导出目录）=====
    Public Enum AssetCategory
        Unknown
        Texture
        Model
        Animation
        Audio
        Text
        Script
        Shader
        Font
    End Enum

    Public Function CategoryOf(classID As Integer) As AssetCategory
        Select Case classID
            Case ClassID_Texture2D, ClassID_Sprite, ClassID_Cubemap
                Return AssetCategory.Texture
            Case ClassID_Mesh
                Return AssetCategory.Model
            Case ClassID_AnimationClip, ClassID_Animator, ClassID_AnimatorController
                Return AssetCategory.Animation
            Case ClassID_AudioClip
                Return AssetCategory.Audio
            Case ClassID_TextAsset
                Return AssetCategory.Text
            Case ClassID_MonoScript
                Return AssetCategory.Script
            Case ClassID_Shader
                Return AssetCategory.Shader
            Case ClassID_Font
                Return AssetCategory.Font
            Case Else
                Return AssetCategory.Unknown
        End Select
    End Function

    Public Function CategoryFolder(cat As AssetCategory) As String
        Select Case cat
            Case AssetCategory.Texture : Return "Textures"
            Case AssetCategory.Model : Return "Models"
            Case AssetCategory.Animation : Return "Animations"
            Case AssetCategory.Audio : Return "Audio"
            Case AssetCategory.Text : Return "Text"
            Case AssetCategory.Script : Return "Scripts"
            Case AssetCategory.Shader : Return "Shaders"
            Case AssetCategory.Font : Return "Fonts"
            Case Else : Return "Others"
        End Select
    End Function

    ' ===== Unity 纹理像素格式（m_TextureFormat）=====
    ' 取值随 Unity 版本略有差异，以下为 5.6/2017 常用映射，将在实测中校验。
    Public Enum TexFormat As Integer
        Alpha8 = 1
        ARGB4444 = 2
        RGB24 = 3
        RGBA32 = 4
        ARGB32 = 5
        RGB565 = 6
        BGR24 = 7
        RGBA4444 = 8
        BGRA32 = 9
        RHalf = 10
        RGHalf = 11
        RGBAHalf = 12
        RFloat = 13
        RGFloat = 14
        RGBAFloat = 15
        YUY2 = 16
        RGB9e5Float = 17
        BC4 = 18
        BC5 = 19
        BC6H = 20
        BC7 = 21
        DXT1 = 22
        DXT5 = 23
        R4G4B4A4 = 24
        RGBA64 = 25
        ' 压缩格式（移动端常见）
        ETC_RGB4 = 34        ' ETC1
        EAC_R = 41
        EAC_RG = 42
        EAC_R_SIGNED = 43
        EAC_RG_SIGNED = 44
        ETC2_RGB4 = 45
        ETC2_RGBA1 = 46
        ETC2_RGBA8 = 47
        ASTC_RGB_4x4 = 48
        ASTC_RGB_5x5 = 49
        ASTC_RGB_6x6 = 50
        ASTC_RGB_8x8 = 51
        ASTC_RGB_10x10 = 52
        ASTC_RGB_12x12 = 53
        ASTC_RGBA_4x4 = 54
        ASTC_RGBA_5x5 = 55
        ASTC_RGBA_6x6 = 56
        ASTC_RGBA_8x8 = 57
        ASTC_RGBA_10x10 = 58
        ASTC_RGBA_12x12 = 59
    End Enum

    Public Function TexFormatName(fmt As Integer) As String
        Dim name As String = ""
        Select Case CType(fmt, TexFormat)
            Case TexFormat.Alpha8 : name = "Alpha8"
            Case TexFormat.ARGB4444 : name = "ARGB4444"
            Case TexFormat.RGB24 : name = "RGB24"
            Case TexFormat.RGBA32 : name = "RGBA32"
            Case TexFormat.ARGB32 : name = "ARGB32"
            Case TexFormat.RGB565 : name = "RGB565"
            Case TexFormat.BGR24 : name = "BGR24"
            Case TexFormat.RGBA4444 : name = "RGBA4444"
            Case TexFormat.BGRA32 : name = "BGRA32"
            Case TexFormat.RHalf : name = "RHalf"
            Case TexFormat.RGHalf : name = "RGHalf"
            Case TexFormat.RGBAHalf : name = "RGBAHalf"
            Case TexFormat.RFloat : name = "RFloat"
            Case TexFormat.RGFloat : name = "RGFloat"
            Case TexFormat.RGBAFloat : name = "RGBAFloat"
            Case TexFormat.YUY2 : name = "YUY2"
            Case TexFormat.BC4 : name = "BC4"
            Case TexFormat.BC5 : name = "BC5"
            Case TexFormat.BC6H : name = "BC6H"
            Case TexFormat.BC7 : name = "BC7"
            Case TexFormat.DXT1 : name = "DXT1"
            Case TexFormat.DXT5 : name = "DXT5"
            Case TexFormat.ETC_RGB4 : name = "ETC_RGB4(ETC1)"
            Case TexFormat.EAC_R : name = "EAC_R"
            Case TexFormat.EAC_RG : name = "EAC_RG"
            Case TexFormat.ETC2_RGB4 : name = "ETC2_RGB4"
            Case TexFormat.ETC2_RGBA1 : name = "ETC2_RGBA1"
            Case TexFormat.ETC2_RGBA8 : name = "ETC2_RGBA8"
            Case TexFormat.ASTC_RGB_4x4 To TexFormat.ASTC_RGBA_12x12 : name = "ASTC"
            Case Else : name = "Format#" & fmt.ToString()
        End Select
        Return name
    End Function

    ' ===== 音频压缩类型（m_CompressionType / AudioCompressionFormat）=====
    Public Enum AudioCompressionFormat As Integer
        PCM = 0
        Vorbis = 1
        ADPCM = 2
        MP3 = 3
        VAG = 4
        HEVAG = 5
        XMA = 6
        GCADPCM = 7
        ATRAC9 = 8
        [Namespace] = 9
    End Enum

    Public Function AudioFormatName(fmt As Integer) As String
        Select Case fmt
            Case 0 : Return "PCM"
            Case 1 : Return "Vorbis"
            Case 2 : Return "ADPCM"
            Case 3 : Return "MP3"
            Case 4 : Return "VAG"
            Case 5 : Return "HEVAG"
            Case 6 : Return "XMA"
            Case 7 : Return "GCADPCM"
            Case 8 : Return "ATRAC9"
            Case Else : Return "Audio#" & fmt.ToString()
        End Select
    End Function

End Module
