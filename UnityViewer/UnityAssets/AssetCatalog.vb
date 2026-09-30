Option Strict On
Option Explicit On

Imports System.Drawing

''' <summary>单个可提取资源的目录条目，供 Form1 绑定与导出复用。</summary>
Public Class AssetEntry
    Public Property SourceLabel As String = ""        ' 来源文件/包名
    Public Property TypeName As String = ""
    Public Property ClassID As Integer
    Public Property PathID As Long
    Public Property Name As String = ""
    Public Property Category As AssetCategory
    Public Property Size As Integer

    ' 解析上下文（提取时使用）
    Public Property File As SerializedFile
    Public Property Obj As ObjectInfo

    ' 提取结果（懒加载 / 导出时填充）
    Public Property PreviewBitmap As Bitmap
    Public Property PreviewText As String
    Public Property StructuredInfo As List(Of KeyValuePair(Of String, String))
    Public Property ExtractedPath As String = ""
    Public Property ErrorMessage As String = ""
    Public Property WasRawFallback As Boolean = False   ' 是否因解析异常而回退为原始字节

    Public Overrides Function ToString() As String
        If Name <> "" Then Return Name
        Return TypeName & " #" & PathID
    End Function
End Class

''' <summary>资源目录集合。</summary>
Public Class AssetCatalog
    Public Property Entries As New List(Of AssetEntry)()
    Public Property SourceLabel As String = ""

    Public ReadOnly Property Count As Integer
        Get
            Return Entries.Count
        End Get
    End Property

    Public ReadOnly Property CountByCategory(cat As AssetCategory) As Integer
        Get
            Dim n = 0
            For Each e In Entries
                If e.Category = cat Then n += 1
            Next
            Return n
        End Get
    End Property
End Class
