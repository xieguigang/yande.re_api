Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms

''' <summary>
''' Unity 资源提取器主界面：选择游戏文件夹 → 扫描解析 → 富预览（图片/文本/结构化/元信息）→ 导出。
''' </summary>
Public Class Form1

#Region "字段"
    Private catalogs As New List(Of AssetCatalog)()
    Private previewDir As String = ""
    Private outputDir As String = ""
    Private scanRunning As Boolean = False
    Private iconMap As New Dictionary(Of AssetCategory, Integer)()
    Private previewImage As Image = Nothing
    Private previewStream As MemoryStream = Nothing
#End Region

#Region "构造 / 初始化"
    Public Sub New()
        InitializeComponent()
        InitIcons()
        InitMetaGrid()
        tscbFolder.Items.Add("Z:\klsdzj_4.11.1_1_20181220_141000_676167")
        tscbFolder.Items.Add("Z:\klsdzj_4.15.1_20190428_025416_686ef")
        tscbFolder.Text = tscbFolder.Items(0).ToString()
        previewDir = Path.Combine(Path.GetTempPath(), "UnityViewer", "preview")
        Directory.CreateDirectory(previewDir)
    End Sub

    ''' <summary>初始化元信息表格列（须在添加行之前建立，否则 Rows.Add 抛出无列异常）。</summary>
    Private Sub InitMetaGrid()
        dgvMeta.Columns.Clear()
        Dim colKey = New DataGridViewTextBoxColumn()
        colKey.Name = "colKey"
        colKey.HeaderText = "属性"
        colKey.Width = 110
        colKey.ReadOnly = True
        colKey.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#AEB6C6")
        colKey.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#272D3A")
        Dim colValue = New DataGridViewTextBoxColumn()
        colValue.Name = "colValue"
        colValue.HeaderText = "值"
        colValue.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        colValue.ReadOnly = True
        colValue.DefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#E6EAF2")
        colValue.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#272D3A")
        dgvMeta.Columns.Add(colKey)
        dgvMeta.Columns.Add(colValue)
        dgvMeta.RowHeadersVisible = False
        dgvMeta.BorderStyle = BorderStyle.None
        dgvMeta.EnableHeadersVisualStyles = False
        dgvMeta.ColumnHeadersDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#1F2430")
        dgvMeta.ColumnHeadersDefaultCellStyle.ForeColor = ColorTranslator.FromHtml("#AEB6C6")
        dgvMeta.BackgroundColor = ColorTranslator.FromHtml("#272D3A")
        dgvMeta.GridColor = ColorTranslator.FromHtml("#2E3545")
        dgvMeta.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#1E88E5")
        dgvMeta.DefaultCellStyle.SelectionForeColor = Color.White
    End Sub

    ''' <summary>按类别生成 16x16 图标（彩色圆角块 + 首字母）。</summary>
    Private Sub InitIcons()
        Dim defs As New List(Of KeyValuePair(Of AssetCategory, (String, Color))) From {
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Texture, ("T", ColorTranslator.FromHtml("#00BFA5"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Model, ("M", ColorTranslator.FromHtml("#1E88E5"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Animation, ("A", ColorTranslator.FromHtml("#7E57C2"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Audio, ("S", ColorTranslator.FromHtml("#FFB300"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Text, ("X", ColorTranslator.FromHtml("#26A69A"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Script, ("C", ColorTranslator.FromHtml("#78909C"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Shader, ("H", ColorTranslator.FromHtml("#EF5350"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Font, ("F", ColorTranslator.FromHtml("#AB47BC"))),
            New KeyValuePair(Of AssetCategory, (String, Color))(AssetCategory.Unknown, ("?", ColorTranslator.FromHtml("#607D8B")))
        }
        Dim idx = 0
        For Each d In defs
            Dim bmp As New Bitmap(16, 16)
            Using g = Graphics.FromImage(bmp)
                g.SmoothingMode = SmoothingMode.AntiAlias
                Using br = New SolidBrush(d.Value.Item2)
                    g.FillRoundedRect(br, 1, 1, 14, 14, 3)
                End Using
                Using sf = New StringFormat() With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
                    g.DrawString(d.Value.Item1, New Font("Microsoft YaHei", 9, FontStyle.Bold), Brushes.White, New RectangleF(0, 0, 16, 16), sf)
                End Using
            End Using
            ilIcons.Images.Add(bmp)
            iconMap(d.Key) = idx
            idx += 1
        Next
    End Sub
#End Region

#Region "按钮事件"
    Private Sub tsbSelect_Click(sender As Object, e As EventArgs) Handles tsbSelect.Click
        Using dlg = New FolderBrowserDialog()
            dlg.Description = "选择安卓游戏解包后的文件夹"
            dlg.ShowNewFolderButton = False
            If tscbFolder.Text <> "" AndAlso Directory.Exists(tscbFolder.Text) Then dlg.SelectedPath = tscbFolder.Text
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                tscbFolder.Text = dlg.SelectedPath
            End If
        End Using
    End Sub

    Private Sub tsbScan_Click(sender As Object, e As EventArgs) Handles tsbScan.Click
        StartScan()
    End Sub

    Private Sub tsbExportSel_Click(sender As Object, e As EventArgs) Handles tsbExportSel.Click
        Dim node = tvAssets.SelectedNode
        If node Is Nothing OrElse TypeOf node.Tag IsNot AssetEntry Then
            MessageBox.Show("请先在左侧资源树中选择一个资源。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        ExportOne(DirectCast(node.Tag, AssetEntry))
    End Sub

    Private Sub tsbExportAll_Click(sender As Object, e As EventArgs) Handles tsbExportAll.Click
        ExportAll()
    End Sub

    Private Sub tsbOpenOut_Click(sender As Object, e As EventArgs) Handles tsbOpenOut.Click
        If outputDir <> "" AndAlso Directory.Exists(outputDir) Then
            Process.Start("explorer.exe", outputDir)
        ElseIf tscbFolder.Text <> "" Then
            Process.Start("explorer.exe", tscbFolder.Text)
        End If
    End Sub
#End Region

#Region "扫描"
    Private Sub StartScan()
        If scanRunning Then Return
        Dim root = tscbFolder.Text.Trim()
        If root = "" OrElse Not Directory.Exists(root) Then
            MessageBox.Show("请先选择有效的游戏文件夹。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        scanRunning = True
        SetButtonsEnabled(False)
        tspb.Visible = True
        tspb.Style = ProgressBarStyle.Marquee
        tsslStats.Text = "正在扫描并解析资源..."
        tsslFolder.Text = root
        tvAssets.Nodes.Clear()
        catalogs.Clear()

        Dim scanTask As Task(Of List(Of AssetCatalog)) = Task.Run(Function()
                                                                      Return AssetScanner.ScanFolder(root)
                                                                  End Function)
        scanTask.ContinueWith(Sub(t)
                              Try
                                  Dim result = t.Result
                                  Me.Invoke(Sub() OnScanComplete(result, root))
                              Catch ex As Exception
                                  Me.Invoke(Sub() MessageBox.Show("扫描失败：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error))
                              Finally
                                  Me.Invoke(Sub()
                                                scanRunning = False
                                                SetButtonsEnabled(True)
                                                tspb.Visible = False
                                            End Sub)
                              End Try
                          End Sub)
    End Sub

    Private Sub OnScanComplete(result As List(Of AssetCatalog), root As String)
        catalogs = result
        outputDir = root & "_extracted"
        BuildTree()
        Dim total = 0
        For Each c In catalogs : total += c.Count : Next
        tsslStats.Text = String.Format("完成：{0} 个来源，{1} 个资源", catalogs.Count, total)
        If total = 0 Then
            MessageBox.Show("未在该文件夹中找到可提取的 Unity 资源（.assets / UnityFS）。" & vbCrLf &
                            "请确认已对安卓 APK 解包，并保留 assets/Android 目录与 .assets 文件。",
                            "无资源", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub BuildTree()
        tvAssets.BeginUpdate()
        tvAssets.Nodes.Clear()
        For Each cat As AssetCatalog In catalogs
            Dim rootNode As TreeNode = tvAssets.Nodes.Add(cat.SourceLabel)
            rootNode.Tag = cat
            rootNode.NodeFont = New Font("Microsoft YaHei", 11, FontStyle.Bold)
            rootNode.ForeColor = ColorTranslator.FromHtml("#E6EAF2")

            ' 按类别分组
            Dim groups As New Dictionary(Of AssetCategory, List(Of AssetEntry))()
            For Each e As AssetEntry In cat.Entries
                If Not groups.ContainsKey(e.Category) Then groups.Add(e.Category, New List(Of AssetEntry)())
                groups(e.Category).Add(e)
            Next
            For Each g As KeyValuePair(Of AssetCategory, List(Of AssetEntry)) In groups
                Dim catName As String = CategoryLabel(g.Key)
                Dim cn As TreeNode = rootNode.Nodes.Add("[" & catName & "] (" & g.Value.Count & ")")
                cn.Tag = New Object() {cat, g.Key}
                If iconMap.ContainsKey(g.Key) Then cn.ImageIndex = iconMap(g.Key) : cn.SelectedImageIndex = cn.ImageIndex
                cn.ForeColor = ColorTranslator.FromHtml("#AEB6C6")
                For Each entry As AssetEntry In g.Value
                    Dim label As String = If(entry.Name <> "", entry.Name, entry.TypeName & " #" & entry.PathID)
                    If entry.WasRawFallback Then label = label & " (原始)"
                    Dim en As TreeNode = cn.Nodes.Add(label)
                    en.Tag = entry
                    If iconMap.ContainsKey(entry.Category) Then en.ImageIndex = iconMap(entry.Category) : en.SelectedImageIndex = en.ImageIndex
                Next
            Next
            rootNode.Expand()
        Next
        tvAssets.EndUpdate()
    End Sub

    Private Function CategoryLabel(cat As AssetCategory) As String
        Select Case cat
            Case AssetCategory.Texture : Return "贴图"
            Case AssetCategory.Model : Return "模型"
            Case AssetCategory.Animation : Return "动画"
            Case AssetCategory.Audio : Return "音频"
            Case AssetCategory.Text : Return "文本"
            Case AssetCategory.Script : Return "脚本"
            Case AssetCategory.Shader : Return "Shader"
            Case AssetCategory.Font : Return "字体"
            Case Else : Return "其他"
        End Select
    End Function
#End Region

#Region "预览"
    Private Sub tvAssets_AfterSelect(sender As Object, e As TreeViewEventArgs) Handles tvAssets.AfterSelect
        If TypeOf e.Node.Tag Is AssetEntry Then
            ShowPreview(DirectCast(e.Node.Tag, AssetEntry))
        Else
            ClearPreview()
        End If
    End Sub

    Private Sub ClearPreview()
        SetPreviewImage(Nothing, Nothing)
        lblImageInfo.Text = "未选择贴图资源"
        rtbText.Text = "未选择文本资源"
        lvStruct.Items.Clear()
        dgvMeta.Rows.Clear()
    End Sub

    ''' <summary>替换图片预览内容并释放旧图像及其内存流（GDI+ 要求流与图像同生命周期）。</summary>
    Private Sub SetPreviewImage(img As Image, ms As MemoryStream)
        If previewImage IsNot Nothing Then
            previewImage.Dispose()
            previewImage = Nothing
        End If
        If previewStream IsNot Nothing Then
            previewStream.Dispose()
            previewStream = Nothing
        End If
        previewImage = img
        previewStream = ms
        pbImage.Image = img
    End Sub

    Private Sub ShowPreview(entry As AssetEntry)
        ' 懒加载：首次选中时解析（写入预览临时目录）
        If entry.StructuredInfo Is Nothing AndAlso entry.ExtractedPath = "" AndAlso entry.ErrorMessage = "" Then
            Try
                AssetExtractors.Extract(entry, previewDir)
            Catch ex As Exception
                entry.ErrorMessage = ex.Message
            End Try
        End If

        ' 图片预览：从导出的 PNG 文件加载（避免使用已释放的 GDI+ 位图）
        If entry.Category = AssetCategory.Texture Then
            Dim loaded = False
            If entry.ExtractedPath <> "" AndAlso File.Exists(entry.ExtractedPath) Then
                Try
                    Dim bytes = File.ReadAllBytes(entry.ExtractedPath)
                    Dim ms As New MemoryStream(bytes)
                    SetPreviewImage(Image.FromStream(ms), ms)
                    loaded = True
                Catch
                    loaded = False
                End Try
            End If
            If Not loaded Then
                SetPreviewImage(Nothing, Nothing)
            End If
            Dim dimInfo = If(entry.StructuredInfo IsNot Nothing, FindKV(entry.StructuredInfo, "尺寸"), "")
            lblImageInfo.Text = entry.Name & "   " & dimInfo & "   " &
                                If(entry.WasRawFallback, "（未支持格式·已存原始）", "") &
                                If(Not loaded, "（无可视化预览）", "")
            tcPreview.SelectedTab = tpImage
        ElseIf (entry.Category = AssetCategory.Text OrElse entry.Category = AssetCategory.Script OrElse
                entry.Category = AssetCategory.Shader) AndAlso entry.PreviewText IsNot Nothing Then
            rtbText.Text = entry.PreviewText
            tcPreview.SelectedTab = tpText
        Else
            tcPreview.SelectedTab = tpStruct
        End If

        ' 结构化信息
        lvStruct.Items.Clear()
        If entry.StructuredInfo IsNot Nothing Then
            For Each kv In entry.StructuredInfo
                Dim li = lvStruct.Items.Add(kv.Key)
                li.SubItems.Add(kv.Value)
                li.ForeColor = ColorTranslator.FromHtml("#E6EAF2")
            Next
        End If
        If entry.ErrorMessage <> "" Then
            Dim li = lvStruct.Items.Add("解析备注")
            li.SubItems.Add(entry.ErrorMessage)
            li.ForeColor = ColorTranslator.FromHtml("#FFB300")
        End If

        ' 元信息
        PopulateMeta(entry)
    End Sub

    Private Sub PopulateMeta(entry As AssetEntry)
        dgvMeta.Rows.Clear()
        Dim add = Sub(k As String, v As String)
                      dgvMeta.Rows.Add(k, v)
                  End Sub
        add("类型", entry.TypeName)
        add("类别", CategoryLabel(entry.Category))
        add("classID", entry.ClassID.ToString())
        add("pathID", entry.PathID.ToString())
        add("大小(字节)", entry.Size.ToString())
        add("来源", entry.SourceLabel)
        Dim comp = "内联"
        If entry.StructuredInfo IsNot Nothing Then
            Dim c = FindKV(entry.StructuredInfo, "外置资源")
            If c <> "" Then comp = c
        End If
        add("资源位置", comp)
        add("已导出", If(entry.ExtractedPath <> "", "是 → " & entry.ExtractedPath, "否"))
        add("回退原始", If(entry.WasRawFallback, "是", "否"))
        If entry.ErrorMessage <> "" Then add("错误", entry.ErrorMessage)
    End Sub

    Private Function FindKV(list As List(Of KeyValuePair(Of String, String)), key As String) As String
        For Each kv In list
            If kv.Key = key Then Return kv.Value
        Next
        Return ""
    End Function
#End Region

#Region "导出"
    Private Sub ExportOne(entry As AssetEntry)
        If outputDir = "" Then outputDir = tscbFolder.Text & "_extracted"
        Directory.CreateDirectory(outputDir)
        Dim ok = AssetExtractors.Extract(entry, outputDir)
        If ok Then
            Dim note = If(entry.WasRawFallback, "（已回退为原始字节）", "")
            tsslStats.Text = "已导出：" & entry.Name & note
        Else
            tsslStats.Text = "导出失败：" & entry.ErrorMessage
        End If
        PopulateMeta(entry)
    End Sub

    Private Sub ExportAll()
        If catalogs.Count = 0 Then
            MessageBox.Show("请先开始提取。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If outputDir = "" Then outputDir = tscbFolder.Text & "_extracted"
        Directory.CreateDirectory(outputDir)

        Dim all As New List(Of AssetEntry)()
        For Each c In catalogs
            For Each e In c.Entries
                all.Add(e)
            Next
        Next

        scanRunning = True
        SetButtonsEnabled(False)
        tspb.Visible = True
        tspb.Style = ProgressBarStyle.Continuous
        tspb.Minimum = 0 : tspb.Maximum = all.Count : tspb.Value = 0

        Dim exportTask As Task = Task.Run(Sub()
                                        For i = 0 To all.Count - 1
                                            Try
                                                AssetExtractors.Extract(all(i), outputDir)
                                            Catch
                                            End Try
                                            Dim p = i + 1
                                            Me.Invoke(Sub()
                                                          tspb.Value = Math.Min(p, all.Count)
                                                          tsslStats.Text = "导出中 " & p & "/" & all.Count
                                                      End Sub)
                                        Next
                                    End Sub)
        exportTask.ContinueWith(Sub()
                              Me.Invoke(Sub()
                                            scanRunning = False
                                            SetButtonsEnabled(True)
                                            tspb.Visible = False
                                            tsslStats.Text = "导出完成 → " & outputDir
                                        End Sub)
                          End Sub)
    End Sub

    Private Sub SetButtonsEnabled(enabled As Boolean)
        tsbScan.Enabled = enabled
        tsbSelect.Enabled = enabled
        tsbExportSel.Enabled = enabled
        tsbExportAll.Enabled = enabled
        tscbFolder.Enabled = enabled
    End Sub
#End Region

End Class

''' <summary>Graphics 圆角矩形扩展。</summary>
Friend Module GraphicsExtensions
    <System.Runtime.CompilerServices.Extension()>
    Public Sub FillRoundedRect(g As Graphics, brush As Brush, x As Integer, y As Integer, w As Integer, h As Integer, r As Integer)
        Using path As New GraphicsPath()
            path.AddArc(x, y, r, r, 180, 90)
            path.AddArc(x + w - r, y, r, r, 270, 90)
            path.AddArc(x + w - r, y + h - r, r, r, 0, 90)
            path.AddArc(x, y + h - r, r, r, 90, 90)
            path.CloseFigure()
            g.FillPath(brush, path)
        End Using
    End Sub
End Module

''' <summary>深色工具条渲染器（ToolStrip / StatusStrip）。</summary>
Friend Class DarkToolStripRenderer
    Inherits ToolStripProfessionalRenderer

    Public Sub New()
        MyBase.New(New DarkColorTable())
    End Sub

    Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
        Using br As New SolidBrush(ColorTranslator.FromHtml("#272D3A"))
            e.Graphics.FillRectangle(br, e.AffectedBounds)
        End Using
    End Sub

    Protected Overrides Sub OnRenderButtonBackground(e As ToolStripItemRenderEventArgs)
        Dim btn = TryCast(e.Item, ToolStripButton)
        If btn Is Nothing Then Return
        Dim bounds = New Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1)
        Dim baseColor = "#2E3545"
        If btn.Tag IsNot Nothing AndAlso btn.Tag.ToString() = "accent" Then baseColor = "#1E88E5"
        If (btn.Pressed) Then baseColor = "#00BFA5"
        If (btn.Selected) Then baseColor = If(baseColor = "#1E88E5", "#1565C0", "#3A4150")
        Using br As New SolidBrush(ColorTranslator.FromHtml(baseColor))
            e.Graphics.FillRoundedRect(br, 1, 1, bounds.Width - 1, bounds.Height - 1, 4)
        End Using
    End Sub

    Protected Overrides Sub OnRenderLabelBackground(e As ToolStripItemRenderEventArgs)
        ' 状态栏标签透明背景
    End Sub

    Protected Overrides Sub OnRenderToolStripBorder(e As ToolStripRenderEventArgs)
        ' 无边框
    End Sub

    Protected Overrides Sub OnRenderSeparator(e As ToolStripSeparatorRenderEventArgs)
        Using pen As New Pen(ColorTranslator.FromHtml("#15181F"))
            e.Graphics.DrawLine(pen, e.Item.Bounds.Left, e.Item.Bounds.Height \ 2, e.Item.Bounds.Right, e.Item.Bounds.Height \ 2)
        End Using
    End Sub
End Class

''' <summary>深色配色表。</summary>
Friend Class DarkColorTable
    Inherits ProfessionalColorTable
    Public Overrides ReadOnly Property ToolStripGradientBegin As Color = ColorTranslator.FromHtml("#272D3A")
    Public Overrides ReadOnly Property ToolStripGradientMiddle As Color = ColorTranslator.FromHtml("#272D3A")
    Public Overrides ReadOnly Property ToolStripGradientEnd As Color = ColorTranslator.FromHtml("#272D3A")
    Public Overrides ReadOnly Property StatusStripGradientBegin As Color = ColorTranslator.FromHtml("#272D3A")
    Public Overrides ReadOnly Property StatusStripGradientEnd As Color = ColorTranslator.FromHtml("#272D3A")
    Public Overrides ReadOnly Property ButtonSelectedGradientBegin As Color = ColorTranslator.FromHtml("#3A4150")
    Public Overrides ReadOnly Property ButtonSelectedGradientEnd As Color = ColorTranslator.FromHtml("#3A4150")
    Public Overrides ReadOnly Property ButtonPressedGradientBegin As Color = ColorTranslator.FromHtml("#00BFA5")
    Public Overrides ReadOnly Property ButtonPressedGradientEnd As Color = ColorTranslator.FromHtml("#00BFA5")
    Public Overrides ReadOnly Property MenuStripGradientBegin As Color = ColorTranslator.FromHtml("#272D3A")
    Public Overrides ReadOnly Property MenuStripGradientEnd As Color = ColorTranslator.FromHtml("#272D3A")
End Class
