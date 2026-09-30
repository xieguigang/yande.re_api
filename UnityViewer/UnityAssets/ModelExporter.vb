Option Strict On
Option Explicit On

Imports System.IO
Imports System.Text

''' <summary>将解析得到的网格数据写出为 Wavefront OBJ 文本。</summary>
Public Module ModelExporter

    Public Sub WriteOBJ(path As String,
                        positions As List(Of Vector3),
                        normals As List(Of Vector3),
                        uvs As List(Of Vector2),
                        triangles As List(Of Integer),
                        Optional name As String = "mesh")
        Dim sb As New StringBuilder()
        sb.AppendLine("# Exported by UnityViewer")
        sb.AppendLine("o " & Sanitize(name))

        For Each p In positions
            sb.AppendLine("v " & p.X.ToString("F6") & " " & p.Y.ToString("F6") & " " & p.Z.ToString("F6"))
        Next
        For Each n In normals
            sb.AppendLine("vn " & n.X.ToString("F6") & " " & n.Y.ToString("F6") & " " & n.Z.ToString("F6"))
        Next
        For Each t In uvs
            sb.AppendLine("vt " & t.X.ToString("F6") & " " & t.Y.ToString("F6"))
        Next

        ' 三角形：OBJ 索引为 1-based
        sb.AppendLine("g " & Sanitize(name))
        sb.AppendLine("s off")
        For i = 0 To triangles.Count - 1 Step 3
            If i + 2 >= triangles.Count Then Exit For
            Dim a = triangles(i) + 1
            Dim b = triangles(i + 1) + 1
            Dim c = triangles(i + 2) + 1
            sb.AppendLine("f " & a & "/" & a & "/" & a & " " & b & "/" & b & "/" & b & " " & c & "/" & c & "/" & c)
        Next

        File.WriteAllText(path, sb.ToString())
    End Sub

    Private Function Sanitize(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return "mesh"
        Dim sb As New StringBuilder()
        For Each c In s
            If Char.IsLetterOrDigit(c) OrElse c = "_" OrElse c = "-" Then sb.Append(c) Else sb.Append("_"c)
        Next
        Return sb.ToString()
    End Function

End Module

Public Structure Vector2
    Public X As Single
    Public Y As Single
    Public Sub New(x As Single, y As Single)
        Me.X = x : Me.Y = y
    End Sub
End Structure

Public Structure Vector3
    Public X As Single
    Public Y As Single
    Public Z As Single
    Public Sub New(x As Single, y As Single, z As Single)
        Me.X = x : Me.Y = y : Me.Z = z
    End Sub
End Structure
