<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'UI 控件声明（深色工具风）
    Friend WithEvents tsMain As System.Windows.Forms.ToolStrip
    Friend WithEvents tsbSelect As System.Windows.Forms.ToolStripButton
    Friend WithEvents tscbFolder As System.Windows.Forms.ToolStripComboBox
    Friend WithEvents tsbScan As System.Windows.Forms.ToolStripButton
    Friend WithEvents tsbExportSel As System.Windows.Forms.ToolStripButton
    Friend WithEvents tsbExportAll As System.Windows.Forms.ToolStripButton
    Friend WithEvents tsbOpenOut As System.Windows.Forms.ToolStripButton

    Friend WithEvents scMain As System.Windows.Forms.SplitContainer
    Friend WithEvents tvAssets As System.Windows.Forms.TreeView
    Friend WithEvents ilIcons As System.Windows.Forms.ImageList

    Friend WithEvents tcPreview As System.Windows.Forms.TabControl
    Friend WithEvents tpImage As System.Windows.Forms.TabPage
    Friend WithEvents pbImage As System.Windows.Forms.PictureBox
    Friend WithEvents lblImageInfo As System.Windows.Forms.Label
    Friend WithEvents tpText As System.Windows.Forms.TabPage
    Friend WithEvents rtbText As System.Windows.Forms.RichTextBox
    Friend WithEvents tpStruct As System.Windows.Forms.TabPage
    Friend WithEvents lvStruct As System.Windows.Forms.ListView
    Friend WithEvents tpMeta As System.Windows.Forms.TabPage
    Friend WithEvents dgvMeta As System.Windows.Forms.DataGridView

    Friend WithEvents ssMain As System.Windows.Forms.StatusStrip
    Friend WithEvents tsslFolder As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents tspb As System.Windows.Forms.ToolStripProgressBar
    Friend WithEvents tsslStats As System.Windows.Forms.ToolStripStatusLabel

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()

        ' ---- 配色 ----
        Dim BG As System.Drawing.Color = System.Drawing.ColorTranslator.FromHtml("#1F2430")
        Dim PanelBG As System.Drawing.Color = System.Drawing.ColorTranslator.FromHtml("#272D3A")
        Dim Accent As System.Drawing.Color = System.Drawing.ColorTranslator.FromHtml("#1E88E5")
        Dim Accent2 As System.Drawing.Color = System.Drawing.ColorTranslator.FromHtml("#00BFA5")
        Dim Fore As System.Drawing.Color = System.Drawing.ColorTranslator.FromHtml("#E6EAF2")
        Dim Muted As System.Drawing.Color = System.Drawing.ColorTranslator.FromHtml("#AEB6C6")

        Dim yaHei = New System.Drawing.Font("Microsoft YaHei", 11.0F, System.Drawing.FontStyle.Regular)

        ' ===== Form =====
        Me.components = components
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1180, 720)
        Me.Text = "Unity 资源提取器"
        Me.BackColor = BG
        Me.ForeColor = Fore
        Me.Font = yaHei
        Me.MinimumSize = New System.Drawing.Size(900, 520)

        ' ===== ToolStrip =====
        tsMain = New System.Windows.Forms.ToolStrip()
        tsMain.Dock = System.Windows.Forms.DockStyle.Top
        tsMain.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden
        tsMain.Font = yaHei
        tsMain.Padding = New System.Windows.Forms.Padding(4, 2, 4, 2)
        tsMain.Renderer = New DarkToolStripRenderer()

        tsbSelect = New System.Windows.Forms.ToolStripButton("选择游戏文件夹")
        tsbSelect.ForeColor = Fore
        tsbSelect.BackColor = PanelBG
        tsbSelect.Margin = New System.Windows.Forms.Padding(2, 1, 2, 1)

        tscbFolder = New System.Windows.Forms.ToolStripComboBox()
        tscbFolder.ComboBox.Width = 420
        tscbFolder.ComboBox.Font = yaHei
        tscbFolder.ComboBox.ForeColor = Fore
        tscbFolder.ComboBox.BackColor = System.Drawing.ColorTranslator.FromHtml("#2E3545")
        tscbFolder.ComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown
        tscbFolder.Text = ""

        tsbScan = New System.Windows.Forms.ToolStripButton("开始提取")
        tsbScan.ForeColor = System.Drawing.Color.White
        tsbScan.BackColor = Accent
        tsbScan.Tag = "accent"
        tsbScan.Font = New System.Drawing.Font("Microsoft YaHei", 11.0F, System.Drawing.FontStyle.Bold)

        tsbExportSel = New System.Windows.Forms.ToolStripButton("导出选中")
        tsbExportSel.ForeColor = Fore
        tsbExportSel.BackColor = PanelBG

        tsbExportAll = New System.Windows.Forms.ToolStripButton("导出全部")
        tsbExportAll.ForeColor = Fore
        tsbExportAll.BackColor = PanelBG

        tsbOpenOut = New System.Windows.Forms.ToolStripButton("打开输出目录")
        tsbOpenOut.ForeColor = Fore
        tsbOpenOut.BackColor = PanelBG

        tsMain.Items.AddRange(New System.Windows.Forms.ToolStripItem() {
            tsbSelect, tscbFolder, tsbScan, tsbExportSel, tsbExportAll, tsbOpenOut})

        ' ===== SplitContainer =====
        scMain = New System.Windows.Forms.SplitContainer()
        scMain.Dock = System.Windows.Forms.DockStyle.Fill
        scMain.BorderStyle = System.Windows.Forms.BorderStyle.None
        scMain.SplitterWidth = 6
        scMain.SplitterDistance = 360
        scMain.Panel1.BackColor = PanelBG
        scMain.Panel2.BackColor = BG
        scMain.BackColor = BG

        ' ===== TreeView =====
        tvAssets = New System.Windows.Forms.TreeView()
        tvAssets.Dock = System.Windows.Forms.DockStyle.Fill
        tvAssets.BackColor = PanelBG
        tvAssets.ForeColor = Fore
        tvAssets.Font = yaHei
        tvAssets.BorderStyle = System.Windows.Forms.BorderStyle.None
        tvAssets.ShowLines = False
        tvAssets.ShowPlusMinus = True
        tvAssets.ShowRootLines = False
        tvAssets.HideSelection = False
        tvAssets.Indent = 18
        scMain.Panel1.Controls.Add(tvAssets)

        ilIcons = New System.Windows.Forms.ImageList(components)
        ilIcons.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit
        ilIcons.ImageSize = New System.Drawing.Size(16, 16)
        ilIcons.TransparentColor = System.Drawing.Color.Transparent
        tvAssets.ImageList = ilIcons

        ' ===== TabControl =====
        tcPreview = New System.Windows.Forms.TabControl()
        tcPreview.Dock = System.Windows.Forms.DockStyle.Fill
        tcPreview.BackColor = BG
        tcPreview.ForeColor = Fore
        tcPreview.Font = New System.Drawing.Font("Microsoft YaHei", 11.0F, System.Drawing.FontStyle.Regular)
        tcPreview.Appearance = System.Windows.Forms.TabAppearance.FlatButtons
        tcPreview.Padding = New System.Drawing.Point(8, 4)

        ' --- 图片预览 ---
        tpImage = New System.Windows.Forms.TabPage("图片预览")
        tpImage.BackColor = BG
        tpImage.ForeColor = Fore
        pbImage = New System.Windows.Forms.PictureBox()
        pbImage.Dock = System.Windows.Forms.DockStyle.Fill
        pbImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
        pbImage.BackColor = System.Drawing.ColorTranslator.FromHtml("#15181F")
        pbImage.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        lblImageInfo = New System.Windows.Forms.Label()
        lblImageInfo.Dock = System.Windows.Forms.DockStyle.Bottom
        lblImageInfo.Height = 26
        lblImageInfo.Text = "未选择贴图资源"
        lblImageInfo.ForeColor = Muted
        lblImageInfo.BackColor = PanelBG
        lblImageInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        lblImageInfo.Padding = New System.Windows.Forms.Padding(8, 0, 0, 0)
        tpImage.Controls.Add(pbImage)
        tpImage.Controls.Add(lblImageInfo)

        ' --- 文本预览 ---
        tpText = New System.Windows.Forms.TabPage("文本预览")
        tpText.BackColor = BG
        tpText.ForeColor = Fore
        rtbText = New System.Windows.Forms.RichTextBox()
        rtbText.Dock = System.Windows.Forms.DockStyle.Fill
        rtbText.ReadOnly = True
        rtbText.BackColor = System.Drawing.ColorTranslator.FromHtml("#15181F")
        rtbText.ForeColor = System.Drawing.ColorTranslator.FromHtml("#C7E0C8")
        rtbText.Font = New System.Drawing.Font("Consolas", 11.0F, System.Drawing.FontStyle.Regular)
        rtbText.WordWrap = True
        rtbText.Text = "未选择文本资源"
        tpText.Controls.Add(rtbText)

        ' --- 结构化信息 ---
        tpStruct = New System.Windows.Forms.TabPage("结构化信息")
        tpStruct.BackColor = BG
        tpStruct.ForeColor = Fore
        lvStruct = New System.Windows.Forms.ListView()
        lvStruct.Dock = System.Windows.Forms.DockStyle.Fill
        lvStruct.View = System.Windows.Forms.View.Details
        lvStruct.FullRowSelect = True
        lvStruct.GridLines = True
        lvStruct.BackColor = System.Drawing.ColorTranslator.FromHtml("#15181F")
        lvStruct.ForeColor = Fore
        lvStruct.Font = yaHei
        lvStruct.Columns.Add("属性", 180)
        lvStruct.Columns.Add("值", 520)
        tpStruct.Controls.Add(lvStruct)

        ' --- 元信息 ---
        tpMeta = New System.Windows.Forms.TabPage("元信息")
        tpMeta.BackColor = BG
        tpMeta.ForeColor = Fore
        dgvMeta = New System.Windows.Forms.DataGridView()
        dgvMeta.Dock = System.Windows.Forms.DockStyle.Fill
        dgvMeta.BackgroundColor = System.Drawing.ColorTranslator.FromHtml("#15181F")
        dgvMeta.ForeColor = Fore
        dgvMeta.Font = yaHei
        dgvMeta.BorderStyle = System.Windows.Forms.BorderStyle.None
        dgvMeta.AllowUserToAddRows = False
        dgvMeta.AllowUserToDeleteRows = False
        dgvMeta.ReadOnly = True
        dgvMeta.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        dgvMeta.RowHeadersVisible = False
        dgvMeta.ColumnHeadersDefaultCellStyle.BackColor = PanelBG
        dgvMeta.ColumnHeadersDefaultCellStyle.ForeColor = Accent2
        dgvMeta.EnableHeadersVisualStyles = False
        tpMeta.Controls.Add(dgvMeta)

        tcPreview.TabPages.AddRange(New System.Windows.Forms.TabPage() {tpImage, tpText, tpStruct, tpMeta})
        scMain.Panel2.Controls.Add(tcPreview)

        ' ===== StatusStrip =====
        ssMain = New System.Windows.Forms.StatusStrip()
        ssMain.Dock = System.Windows.Forms.DockStyle.Bottom
        ssMain.Font = yaHei
        ssMain.Renderer = New DarkToolStripRenderer()
        ssMain.BackColor = PanelBG
        tsslFolder = New System.Windows.Forms.ToolStripStatusLabel("未选择文件夹")
        tsslFolder.ForeColor = Muted
        tsslFolder.Spring = True
        tsslFolder.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        tspb = New System.Windows.Forms.ToolStripProgressBar()
        tspb.Visible = False
        tspb.Size = New System.Drawing.Size(220, 16)
        tsslStats = New System.Windows.Forms.ToolStripStatusLabel("就绪")
        tsslStats.ForeColor = Fore
        ssMain.Items.AddRange(New System.Windows.Forms.ToolStripItem() {tsslFolder, tspb, tsslStats})

        ' ===== 组装 =====
        Me.Controls.Add(scMain)
        Me.Controls.Add(tsMain)
        Me.Controls.Add(ssMain)

    End Sub

End Class
