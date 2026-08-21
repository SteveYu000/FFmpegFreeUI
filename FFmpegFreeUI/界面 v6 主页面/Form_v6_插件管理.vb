Imports System.Diagnostics
Imports System.IO
Imports LakeUI

Public Class Form_v6_插件管理
    Inherits Form

    Public ReadOnly ModernPanel1 As New ModernPanel()
    Private WithEvents UDLV_插件列表 As New UltraDetailListView()
    Private WithEvents MB_切换启用 As New ModernButton()
    Private WithEvents MB_上移 As New ModernButton()
    Private WithEvents MB_下移 As New ModernButton()
    Private WithEvents MB_刷新 As New ModernButton()
    Private WithEvents MB_打开目录 As New ModernButton()
    Private WithEvents MB_重启应用 As New ModernButton()
    Private WithEvents MB_空状态打开目录 As New ModernButton()
    Private ReadOnly P_空状态 As New Panel()
    Private ReadOnly L_插件统计 As New Label()
    Private ReadOnly L_启用统计 As New Label()
    Private ReadOnly L_待重启统计 As New Label()
    Private ReadOnly L_详情状态 As New Label()
    Private ReadOnly L_详情名称 As New Label()
    Private ReadOnly L_详情文件 As New Label()
    Private ReadOnly L_详情接口 As New Label()
    Private ReadOnly L_详情插件版本 As New Label()
    Private ReadOnly L_详情加载状态 As New Label()
    Private ReadOnly L_详情SDK As New Label()
    Private ReadOnly L_详情API As New Label()
    Private ReadOnly L_详情插件ID As New Label()
    Private ReadOnly L_详情程序集 As New Label()
    Private ReadOnly L_详情路径 As New Label()
    Private ReadOnly L_详情消息标题 As New Label()
    Private ReadOnly L_详情消息 As New Label()
    Private ReadOnly L_说明 As New Label()
    Private ReadOnly 快照 As New Dictionary(Of String, 插件信息_v6)(StringComparer.OrdinalIgnoreCase)
    Private 正在填充列表 As Boolean
    Private 忽略管理器通知 As Boolean

    Public Sub New()
        MyBase.New()
        初始化界面()
        AddHandler 插件管理.插件列表已变化, AddressOf 插件列表变化
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then RemoveHandler 插件管理.插件列表已变化, AddressOf 插件列表变化
        MyBase.Dispose(disposing)
    End Sub

    Private Sub 初始化界面()
        SuspendLayout()
        BackColor = Color.FromArgb(24, 24, 24)
        ClientSize = New Size(980, 680)
        FormBorderStyle = FormBorderStyle.None
        MinimumSize = New Size(860, 560)
        Name = NameOf(Form_v6_插件管理)
        Text = "插件管理"

        ModernPanel1.BackColor1 = Color.FromArgb(24, 24, 24)
        ModernPanel1.BorderSize = 0
        ModernPanel1.Dock = DockStyle.Fill
        ModernPanel1.Name = "ModernPanel1"
        ModernPanel1.Padding = New Padding(20)

        Dim layout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 1,
            .Dock = DockStyle.Fill,
            .RowCount = 5
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 116.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 14.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 14.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))

        Dim headerCard = 创建卡片()
        headerCard.Padding = New Padding(22, 16, 18, 16)
        Dim headerLayout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 2,
            .Dock = DockStyle.Fill,
            .RowCount = 1
        }
        headerLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 56.0F))
        headerLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 44.0F))
        headerLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim titleLayout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 1,
            .Dock = DockStyle.Fill,
            .RowCount = 3
        }
        titleLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        titleLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        titleLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Dim title As New Label With {
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .Font = New Font("Microsoft YaHei UI", 17.0F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(238, 238, 238),
            .Text = "插件管理",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim subtitle As New Label With {
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .Font = New Font("Microsoft YaHei UI", 9.2F),
            .ForeColor = Color.FromArgb(145, 190, 190, 190),
            .Text = "管理插件状态、接口兼容性与事件处理顺序",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim stats As New FlowLayoutPanel With {
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .Padding = New Padding(0, 2, 0, 0),
            .WrapContents = False
        }
        配置概览标签(L_插件统计, Color.FromArgb(210, 210, 210))
        配置概览标签(L_启用统计, Color.FromArgb(130, 210, 155))
        配置概览标签(L_待重启统计, Color.FromArgb(235, 180, 105))
        stats.Controls.AddRange({L_插件统计, L_启用统计, L_待重启统计})
        titleLayout.Controls.Add(title, 0, 0)
        titleLayout.Controls.Add(subtitle, 0, 1)
        titleLayout.Controls.Add(stats, 0, 2)

        Dim topActions As New FlowLayoutPanel With {
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(0, 24, 0, 0),
            .WrapContents = False
        }
        配置按钮(MB_打开目录, "打开插件目录", 126)
        配置按钮(MB_刷新, "刷新", 72)
        配置按钮(MB_重启应用, "重启应用", 96, primary:=True)
        topActions.Controls.AddRange({MB_重启应用, MB_刷新, MB_打开目录})
        headerLayout.Controls.Add(titleLayout, 0, 0)
        headerLayout.Controls.Add(topActions, 1, 0)
        headerCard.Controls.Add(headerLayout)

        Dim contentLayout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 2,
            .Dock = DockStyle.Fill,
            .RowCount = 1
        }
        contentLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 67.0F))
        contentLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.0F))
        contentLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim listCard = 创建卡片()
        listCard.Margin = New Padding(0, 0, 7, 0)
        listCard.Padding = New Padding(14)
        Dim listLayout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 1,
            .Dock = DockStyle.Fill,
            .RowCount = 2
        }
        listLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        listLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        listLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Dim listHeader As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 2,
            .Dock = DockStyle.Fill,
            .RowCount = 1
        }
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 248.0F))
        Dim listTitleLayout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 1,
            .Dock = DockStyle.Fill,
            .RowCount = 2
        }
        listTitleLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        listTitleLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        listTitleLayout.Controls.Add(创建文本标签("已安装插件", 12.0F, Color.FromArgb(230, 230, 230)), 0, 0)
        listTitleLayout.Controls.Add(创建文本标签("拖动列表即可调整同一事件中的执行顺序", 8.7F, Color.FromArgb(135, 185, 185, 185)), 0, 1)
        Dim listActions As New FlowLayoutPanel With {
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(0, 10, 0, 0),
            .WrapContents = False
        }
        配置按钮(MB_切换启用, "停用插件", 96, primary:=True)
        配置按钮(MB_上移, "上移", 62)
        配置按钮(MB_下移, "下移", 62)
        listActions.Controls.AddRange({MB_下移, MB_上移, MB_切换启用})
        listHeader.Controls.Add(listTitleLayout, 0, 0)
        listHeader.Controls.Add(listActions, 1, 0)

        Dim listBody As New Panel With {.BackColor = Color.Transparent, .Dock = DockStyle.Fill}

        UDLV_插件列表.AllowDragReorder = True
        UDLV_插件列表.BackgroundColor = Color.FromArgb(16, 16, 16)
        UDLV_插件列表.BorderRadius = 8
        UDLV_插件列表.BorderSize = 0
        UDLV_插件列表.Dock = DockStyle.Fill
        UDLV_插件列表.DragSelectZoneWidth = 300
        UDLV_插件列表.ForeColor = Color.FromArgb(215, 215, 215)
        UDLV_插件列表.GroupBorderColor = Color.FromArgb(55, 55, 55)
        UDLV_插件列表.GroupHeight = 35
        UDLV_插件列表.HeaderBackColor = Color.FromArgb(22, 22, 22)
        UDLV_插件列表.HeaderBorderColor = Color.FromArgb(48, 48, 48)
        UDLV_插件列表.HeaderHeight = 42
        UDLV_插件列表.ItemCornerRadius = 8
        UDLV_插件列表.ItemPadding = New Padding(12, 7, 12, 7)
        UDLV_插件列表.ItemSelectedBackColor = Color.FromArgb(38, 55, 74)
        UDLV_插件列表.MultiSelect = False
        UDLV_插件列表.Padding = New Padding(5, 0, 5, 5)
        UDLV_插件列表.ScrollBarThumbColor = Color.FromArgb(62, 62, 62)
        UDLV_插件列表.ScrollBarThumbHoverColor = Color.FromArgb(92, 92, 92)
        UDLV_插件列表.ScrollBarTrackColor = Color.FromArgb(22, 22, 22)
        UDLV_插件列表.SelectionRectBorderColor = Color.FromArgb(82, 125, 170)
        UDLV_插件列表.SelectionRectFillColor = Color.FromArgb(35, 70, 105)
        添加列("顺序", 54)
        添加列("状态", 72)
        添加列("插件", 220)
        添加列("接口", 100)
        添加列("版本", 110)
        添加列("加载状态", 132)

        配置空状态()
        listBody.Controls.Add(UDLV_插件列表)
        listBody.Controls.Add(P_空状态)
        listLayout.Controls.Add(listHeader, 0, 0)
        listLayout.Controls.Add(listBody, 0, 1)
        listCard.Controls.Add(listLayout)

        Dim detailCard = 创建卡片()
        detailCard.Margin = New Padding(7, 0, 0, 0)
        detailCard.Padding = New Padding(18)
        detailCard.Controls.Add(创建详情布局())
        contentLayout.Controls.Add(listCard, 0, 0)
        contentLayout.Controls.Add(detailCard, 1, 0)

        L_说明.AutoEllipsis = True
        L_说明.BackColor = Color.FromArgb(31, 31, 31)
        L_说明.Dock = DockStyle.Fill
        L_说明.Font = New Font("Microsoft YaHei UI", 9.0F)
        L_说明.ForeColor = Color.FromArgb(155, 205, 205, 205)
        L_说明.Padding = New Padding(14, 0, 14, 0)
        L_说明.Text = "处理顺序会立即用于下一次事件；安装、替换、启用或停用插件需要重启应用。"
        L_说明.TextAlign = ContentAlignment.MiddleLeft

        layout.Controls.Add(headerCard, 0, 0)
        layout.Controls.Add(contentLayout, 0, 2)
        layout.Controls.Add(L_说明, 0, 4)
        ModernPanel1.Controls.Add(layout)
        Controls.Add(ModernPanel1)
        ResumeLayout(False)
    End Sub

    Private Shared Sub 配置按钮(button As ModernButton, text As String, width As Integer, Optional primary As Boolean = False)
        button.BackColor1 = If(primary, Color.FromArgb(45, 69, 94), Color.FromArgb(38, 38, 38))
        button.BorderRadius = 9
        button.BorderSize = 0
        button.Font = New Font("Microsoft YaHei UI", 9.0F)
        button.ForeColor = If(primary, Color.FromArgb(205, 228, 252), Color.FromArgb(200, 210, 220))
        button.HoverBackColor1 = If(primary, Color.FromArgb(57, 85, 114), Color.FromArgb(50, 50, 50))
        button.Margin = New Padding(8, 0, 0, 0)
        button.PressedBackColor1 = If(primary, Color.FromArgb(67, 97, 128), Color.FromArgb(60, 60, 60))
        button.Size = New Size(width, 34)
        button.Text = text
    End Sub

    Private Shared Function 创建卡片() As ModernPanel
        Return New ModernPanel With {
            .BackColor = Color.Transparent,
            .BackColor1 = Color.FromArgb(14, 14, 14),
            .BorderColor = Color.FromArgb(42, 42, 42),
            .BorderRadius = 12,
            .BorderSize = 1,
            .Dock = DockStyle.Fill
        }
    End Function

    Private Shared Function 创建文本标签(text As String, fontSize As Single, color As Color) As Label
        Return New Label With {
            .AutoEllipsis = True,
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .Font = New Font("Microsoft YaHei UI", fontSize),
            .ForeColor = color,
            .Text = text,
            .TextAlign = ContentAlignment.MiddleLeft
        }
    End Function

    Private Shared Sub 配置概览标签(label As Label, color As Color)
        label.AutoSize = True
        label.BackColor = Color.FromArgb(35, 35, 35)
        label.Font = New Font("Microsoft YaHei UI", 8.5F)
        label.ForeColor = color
        label.Margin = New Padding(0, 0, 8, 0)
        label.Padding = New Padding(9, 3, 9, 3)
        label.Text = "0"
    End Sub

    Private Sub 配置空状态()
        P_空状态.BackColor = Color.FromArgb(16, 16, 16)
        P_空状态.Dock = DockStyle.Fill
        P_空状态.Visible = False

        Dim emptyLayout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 1,
            .Dock = DockStyle.Fill,
            .RowCount = 7
        }
        emptyLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        emptyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 45.0F))
        emptyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 62.0F))
        emptyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        emptyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        emptyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        emptyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        emptyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 55.0F))

        Dim icon As New Label With {
            .Anchor = AnchorStyles.None,
            .BackColor = Color.FromArgb(34, 42, 50),
            .Font = New Font("Microsoft YaHei UI", 24.0F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(135, 190, 235),
            .Size = New Size(56, 56),
            .Text = "+",
            .TextAlign = ContentAlignment.MiddleCenter
        }
        Dim emptyTitle = 创建文本标签("还没有安装插件", 13.0F, Color.FromArgb(225, 225, 225))
        emptyTitle.TextAlign = ContentAlignment.MiddleCenter
        Dim emptyDescription = 创建文本标签("把 *.3fui.dll 放入插件目录，然后重启应用即可加载。", 9.0F, Color.FromArgb(145, 190, 190, 190))
        emptyDescription.TextAlign = ContentAlignment.TopCenter
        Dim pathLabel = 创建文本标签(插件管理.插件文件夹路径, 8.5F, Color.FromArgb(115, 165, 175, 185))
        pathLabel.Padding = New Padding(24, 0, 24, 0)
        pathLabel.TextAlign = ContentAlignment.MiddleCenter
        配置按钮(MB_空状态打开目录, "打开插件目录", 126, primary:=True)
        MB_空状态打开目录.Anchor = AnchorStyles.None
        MB_空状态打开目录.Margin = New Padding(0)

        emptyLayout.Controls.Add(icon, 0, 1)
        emptyLayout.Controls.Add(emptyTitle, 0, 2)
        emptyLayout.Controls.Add(emptyDescription, 0, 3)
        emptyLayout.Controls.Add(pathLabel, 0, 4)
        emptyLayout.Controls.Add(MB_空状态打开目录, 0, 5)
        P_空状态.Controls.Add(emptyLayout)
    End Sub

    Private Function 创建详情布局() As Control
        Dim detailLayout As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 1,
            .Dock = DockStyle.Fill,
            .RowCount = 9
        }
        detailLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 22.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 8.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 189.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 20.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 22.0F))
        detailLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim detailHeader As New Panel With {.BackColor = Color.Transparent, .Dock = DockStyle.Fill}
        Dim detailTitle = 创建文本标签("插件详情", 12.0F, Color.FromArgb(230, 230, 230))
        detailTitle.Dock = DockStyle.Fill
        L_详情状态.AutoEllipsis = True
        L_详情状态.BackColor = Color.FromArgb(35, 35, 35)
        L_详情状态.Dock = DockStyle.Right
        L_详情状态.Font = New Font("Microsoft YaHei UI", 8.5F)
        L_详情状态.ForeColor = Color.FromArgb(170, 190, 205)
        L_详情状态.Padding = New Padding(8, 0, 8, 0)
        L_详情状态.Size = New Size(96, 32)
        L_详情状态.Text = "未选择"
        L_详情状态.TextAlign = ContentAlignment.MiddleCenter
        detailHeader.Controls.Add(detailTitle)
        detailHeader.Controls.Add(L_详情状态)

        配置详情标签(L_详情名称, 12.0F, Color.FromArgb(225, 225, 225), FontStyle.Regular)
        配置详情标签(L_详情文件, 8.5F, Color.FromArgb(125, 180, 180, 180), FontStyle.Regular)

        Dim separator As New Panel With {
            .BackColor = Color.FromArgb(42, 42, 42),
            .Dock = DockStyle.Top,
            .Height = 1,
            .Margin = New Padding(0, 4, 0, 3)
        }

        Dim infoGrid As New TableLayoutPanel With {
            .BackColor = Color.Transparent,
            .ColumnCount = 2,
            .Dock = DockStyle.Fill,
            .RowCount = 7
        }
        infoGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 39.0F))
        infoGrid.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 61.0F))
        For index = 0 To 6
            infoGrid.RowStyles.Add(New RowStyle(SizeType.Absolute, 27.0F))
        Next
        添加详情行(infoGrid, 0, "接口类型", L_详情接口)
        添加详情行(infoGrid, 1, "插件版本", L_详情插件版本)
        添加详情行(infoGrid, 2, "加载状态", L_详情加载状态)
        添加详情行(infoGrid, 3, "Ext SDK 引用", L_详情SDK)
        添加详情行(infoGrid, 4, "最低 Ext API", L_详情API)
        添加详情行(infoGrid, 5, "Ext 插件 ID", L_详情插件ID)
        添加详情行(infoGrid, 6, "程序集版本", L_详情程序集)

        Dim pathTitle = 创建文本标签("文件位置", 8.5F, Color.FromArgb(125, 180, 180, 180))
        配置详情标签(L_详情路径, 8.5F, Color.FromArgb(190, 205, 215), FontStyle.Regular)
        L_详情路径.TextAlign = ContentAlignment.TopLeft

        配置详情标签(L_详情消息标题, 8.5F, Color.FromArgb(125, 180, 180, 180), FontStyle.Regular)
        配置详情标签(L_详情消息, 8.5F, Color.FromArgb(165, 195, 205), FontStyle.Regular)
        L_详情消息.Padding = New Padding(10, 7, 10, 7)
        L_详情消息.BackColor = Color.FromArgb(24, 28, 32)
        L_详情消息.TextAlign = ContentAlignment.TopLeft

        detailLayout.Controls.Add(detailHeader, 0, 0)
        detailLayout.Controls.Add(L_详情名称, 0, 1)
        detailLayout.Controls.Add(L_详情文件, 0, 2)
        detailLayout.Controls.Add(separator, 0, 3)
        detailLayout.Controls.Add(infoGrid, 0, 4)
        detailLayout.Controls.Add(pathTitle, 0, 5)
        detailLayout.Controls.Add(L_详情路径, 0, 6)
        detailLayout.Controls.Add(L_详情消息标题, 0, 7)
        detailLayout.Controls.Add(L_详情消息, 0, 8)
        Return detailLayout
    End Function

    Private Shared Sub 配置详情标签(label As Label, fontSize As Single, color As Color, style As FontStyle)
        label.AutoEllipsis = True
        label.BackColor = Color.Transparent
        label.Dock = DockStyle.Fill
        label.Font = New Font("Microsoft YaHei UI", fontSize, style)
        label.ForeColor = color
        label.Text = "-"
        label.TextAlign = ContentAlignment.MiddleLeft
    End Sub

    Private Shared Sub 添加详情行(table As TableLayoutPanel, row As Integer, caption As String, valueLabel As Label)
        Dim captionLabel = 创建文本标签(caption, 8.5F, Color.FromArgb(120, 175, 175, 175))
        配置详情标签(valueLabel, 8.7F, Color.FromArgb(205, 205, 205), FontStyle.Regular)
        table.Controls.Add(captionLabel, 0, row)
        table.Controls.Add(valueLabel, 1, row)
    End Sub

    Private Sub 添加列(text As String, width As Integer)
        UDLV_插件列表.Columns.Add(New UltraDetailListView.ListColumn With {.Text = text, .Width = width})
    End Sub

    Private Sub Form_v6_插件管理_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        刷新列表(重新扫描:=False)
        调整列宽()
    End Sub

    Private Sub 插件列表变化(sender As Object, e As EventArgs)
        If 忽略管理器通知 OrElse IsDisposed Then Exit Sub
        If InvokeRequired Then
            BeginInvoke(Sub() 刷新列表(重新扫描:=False))
        Else
            刷新列表(重新扫描:=False)
        End If
    End Sub

    Private Sub 刷新列表(重新扫描 As Boolean)
        Dim selectedKey = 获取选中插件键()
        If 重新扫描 Then
            忽略管理器通知 = True
            Try
                插件管理.刷新插件目录()
            Finally
                忽略管理器通知 = False
            End Try
        End If

        Dim plugins = 插件管理.获取插件列表()
        快照.Clear()
        For Each plugin In plugins
            快照(plugin.插件键) = plugin
        Next
        更新概览(plugins)

        正在填充列表 = True
        UDLV_插件列表.BeginUpdate()
        Try
            UDLV_插件列表.Items.Clear()
            For index = 0 To plugins.Count - 1
                UDLV_插件列表.Items.Add(创建列表项(plugins(index), index + 1))
            Next
        Finally
            UDLV_插件列表.EndUpdate()
            正在填充列表 = False
        End Try

        Dim isEmpty = plugins.Count = 0
        UDLV_插件列表.Visible = Not isEmpty
        P_空状态.Visible = isEmpty
        If isEmpty Then
            UDLV_插件列表.SelectedIndex = -1
            P_空状态.BringToFront()
        Else
            Dim targetIndex = plugins.FindIndex(Function(item) String.Equals(item.插件键, selectedKey, StringComparison.OrdinalIgnoreCase))
            UDLV_插件列表.SelectedIndex = If(targetIndex >= 0, targetIndex, 0)
            UDLV_插件列表.BringToFront()
        End If
        显示选中插件详情()
        调整列宽()
    End Sub

    Private Sub 更新概览(plugins As IReadOnlyCollection(Of 插件信息_v6))
        Dim enabledCount = 0
        Dim pendingCount = 0
        For Each plugin In plugins
            If plugin.已启用 Then enabledCount += 1
            If plugin.等待重启 Then pendingCount += 1
        Next
        L_插件统计.Text = $"{plugins.Count} 个插件"
        L_启用统计.Text = $"{enabledCount} 个已启用"
        L_待重启统计.Text = If(pendingCount = 0, "无需重启", $"{pendingCount} 项待重启")
        L_待重启统计.ForeColor = If(
            pendingCount = 0,
            Color.FromArgb(145, 185, 190),
            Color.FromArgb(235, 180, 105))
    End Sub

    Private Function 创建列表项(plugin As 插件信息_v6, displayOrder As Integer) As UltraDetailListView.ListItem
        Dim enabledText = If(plugin.已启用, "启用", "禁用")
        If plugin.等待重启 Then enabledText &= " *"
        Return New UltraDetailListView.ListItem(
            New UltraDetailListView.ListSubItem(displayOrder.ToString()),
            New UltraDetailListView.ListSubItem(enabledText),
            New UltraDetailListView.ListSubItem(空值替代(plugin.显示名称)),
            New UltraDetailListView.ListSubItem(接口类型文本(plugin.接口类型)),
            New UltraDetailListView.ListSubItem(空值替代(plugin.插件版本)),
            New UltraDetailListView.ListSubItem(空值替代(plugin.加载状态))
        ) With {.Tag = plugin.插件键}
    End Function

    Private Shared Function 空值替代(value As String) As String
        Return If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
    End Function

    Private Shared Function 接口类型文本(value As 插件接口类型_v6) As String
        Select Case value
            Case 插件接口类型_v6.官方API
                Return "官方 API"
            Case 插件接口类型_v6.ExtAPI
                Return "Ext API"
            Case 插件接口类型_v6.官方与Ext
                Return "官方 + Ext"
            Case Else
                Return "未识别"
        End Select
    End Function

    Private Function 获取选中插件键() As String
        Dim item = UDLV_插件列表.SelectedItem
        Return If(TryCast(item?.Tag, String), "")
    End Function

    Private Function 获取选中插件() As 插件信息_v6
        Dim key = 获取选中插件键()
        Dim plugin As 插件信息_v6 = Nothing
        If key <> "" AndAlso 快照.TryGetValue(key, plugin) Then Return plugin
        Return Nothing
    End Function

    Private Sub 显示选中插件详情()
        Dim plugin = 获取选中插件()
        If plugin Is Nothing Then
            MB_切换启用.Text = "启用插件"
            MB_切换启用.Enabled = False
            MB_上移.Enabled = False
            MB_下移.Enabled = False
            L_详情状态.Text = If(快照.Count = 0, "暂无插件", "未选择")
            L_详情状态.ForeColor = Color.FromArgb(150, 175, 195)
            L_详情名称.Text = If(快照.Count = 0, "插件目录为空", "请选择一个插件")
            L_详情文件.Text = "-"
            L_详情接口.Text = "-"
            L_详情插件版本.Text = "-"
            L_详情加载状态.Text = "-"
            L_详情SDK.Text = "-"
            L_详情API.Text = "-"
            L_详情插件ID.Text = "-"
            L_详情程序集.Text = "-"
            L_详情路径.Text = 插件管理.插件文件夹路径
            Dim emptyStateConfigError = 插件管理.获取配置错误()
            If Not String.IsNullOrWhiteSpace(emptyStateConfigError) Then
                设置详情消息("配置异常", emptyStateConfigError, True)
            ElseIf 快照.Count = 0 Then
                设置详情消息("安装插件", "将 *.3fui.dll 放入左侧所示目录，刷新列表并重启应用。", False)
            Else
                设置详情消息("使用提示", "从左侧选择插件后，可查看接口、版本和加载信息。", False)
            End If
            Exit Sub
        End If

        MB_切换启用.Text = If(plugin.已启用, "停用插件", "启用插件")
        MB_切换启用.Enabled = True
        MB_上移.Enabled = UDLV_插件列表.SelectedIndex > 0
        MB_下移.Enabled = UDLV_插件列表.SelectedIndex >= 0 AndAlso UDLV_插件列表.SelectedIndex < UDLV_插件列表.Items.Count - 1

        L_详情状态.Text = If(plugin.等待重启, "等待重启", If(plugin.已启用, "已启用", "已停用"))
        L_详情状态.ForeColor = If(
            plugin.等待重启,
            Color.FromArgb(235, 180, 105),
            If(plugin.已启用, Color.FromArgb(130, 210, 155), Color.FromArgb(165, 175, 185)))
        L_详情名称.Text = 空值替代(plugin.显示名称)
        L_详情文件.Text = 空值替代(plugin.文件名)
        L_详情接口.Text = 接口类型文本(plugin.接口类型)
        L_详情插件版本.Text = 空值替代(plugin.插件版本)
        L_详情加载状态.Text = 空值替代(plugin.加载状态)
        Dim usesExt = plugin.接口类型 = 插件接口类型_v6.ExtAPI OrElse plugin.接口类型 = 插件接口类型_v6.官方与Ext
        L_详情SDK.Text = If(usesExt, 空值替代(plugin.ExtSDK程序集版本), "不适用")
        L_详情API.Text = If(usesExt, 空值替代(plugin.ExtAPI最低版本), "不适用")
        L_详情插件ID.Text = If(plugin.Ext插件标识.Count = 0, "-", String.Join("、", plugin.Ext插件标识))
        L_详情程序集.Text = $"{空值替代(plugin.程序集名称)} · {空值替代(plugin.程序集版本)}"
        L_详情路径.Text = 空值替代(plugin.文件路径)

        Dim errors As New List(Of String)
        If Not String.IsNullOrWhiteSpace(plugin.加载错误) Then errors.Add($"加载错误：{plugin.加载错误}")
        If Not String.IsNullOrWhiteSpace(plugin.元数据错误) Then errors.Add($"元数据错误：{plugin.元数据错误}")
        Dim configError = 插件管理.获取配置错误()
        If Not String.IsNullOrWhiteSpace(configError) Then errors.Add($"配置错误：{configError}")
        If errors.Count > 0 Then
            设置详情消息("需要处理", String.Join(Environment.NewLine, errors), True)
        ElseIf plugin.等待重启 Then
            设置详情消息("等待应用", "启用状态已经保存，重启应用后生效。", False, warning:=True)
        Else
            设置详情消息("运行状态", "未发现加载错误。处理顺序的调整会立即用于下一次插件事件。", False)
        End If
    End Sub

    Private Sub 设置详情消息(title As String, message As String, isError As Boolean, Optional warning As Boolean = False)
        L_详情消息标题.Text = title
        L_详情消息.Text = message
        If isError Then
            L_详情消息标题.ForeColor = Color.FromArgb(235, 125, 125)
            L_详情消息.ForeColor = Color.FromArgb(235, 170, 170)
            L_详情消息.BackColor = Color.FromArgb(43, 25, 27)
        ElseIf warning Then
            L_详情消息标题.ForeColor = Color.FromArgb(235, 180, 105)
            L_详情消息.ForeColor = Color.FromArgb(225, 200, 155)
            L_详情消息.BackColor = Color.FromArgb(40, 34, 23)
        Else
            L_详情消息标题.ForeColor = Color.FromArgb(125, 180, 180, 180)
            L_详情消息.ForeColor = Color.FromArgb(165, 195, 205)
            L_详情消息.BackColor = Color.FromArgb(24, 28, 32)
        End If
    End Sub

    Private Sub MB_切换启用_Click(sender As Object, e As EventArgs) Handles MB_切换启用.Click
        Dim plugin = 获取选中插件()
        If plugin Is Nothing Then Exit Sub
        Try
            插件管理.设置插件启用状态(plugin.插件键, Not plugin.已启用)
            刷新列表(重新扫描:=False)
        Catch ex As Exception
            ExOverlayMsgBox(FormMain_v6, $"保存插件状态失败：{ex.Message}", MsgBoxStyle.Critical, "插件管理")
            Exit Sub
        End Try

        Dim result = ExOverlayMsgBox(
            FormMain_v6,
            "插件的启用状态已经保存。由于官方插件没有卸载协议，需重启程序才能安全生效。",
            {"立即重启", "稍后"},
            "需要重启",
            MsgBoxStyle.Question,
            1)
        If result = 0 Then System.Windows.Forms.Application.Restart()
    End Sub

    Private Sub MB_上移_Click(sender As Object, e As EventArgs) Handles MB_上移.Click
        移动选中项(-1)
    End Sub

    Private Sub MB_下移_Click(sender As Object, e As EventArgs) Handles MB_下移.Click
        移动选中项(1)
    End Sub

    Private Sub 移动选中项(direction As Integer)
        Dim index = UDLV_插件列表.SelectedIndex
        Dim target = index + direction
        If index < 0 OrElse target < 0 OrElse target >= UDLV_插件列表.Items.Count Then Exit Sub
        正在填充列表 = True
        Try
            Dim moving = UDLV_插件列表.Items(index)
            UDLV_插件列表.Items.RemoveAt(index)
            UDLV_插件列表.Items.Insert(target, moving)
            UDLV_插件列表.SelectedIndex = target
        Finally
            正在填充列表 = False
        End Try
        保存当前顺序()
    End Sub

    Private Sub UDLV_插件列表_ItemOrderChanged(sender As Object, e As EventArgs) Handles UDLV_插件列表.ItemOrderChanged
        If Not 正在填充列表 Then 保存当前顺序()
    End Sub

    Private Sub 保存当前顺序()
        Try
            Dim keys = UDLV_插件列表.Items.Select(Function(item) If(TryCast(item.Tag, String), "")).ToList()
            插件管理.保存插件处理顺序(keys)
            刷新列表(重新扫描:=False)
        Catch ex As Exception
            ExOverlayMsgBox(FormMain_v6, $"保存插件顺序失败：{ex.Message}", MsgBoxStyle.Critical, "插件管理")
        End Try
    End Sub

    Private Sub MB_刷新_Click(sender As Object, e As EventArgs) Handles MB_刷新.Click
        Try
            刷新列表(重新扫描:=True)
            ExFloatingTip(UDLV_插件列表, "插件目录已刷新；新加入的插件将在重启后加载", 1800)
        Catch ex As Exception
            ExOverlayMsgBox(FormMain_v6, $"刷新插件目录失败：{ex.Message}", MsgBoxStyle.Critical, "插件管理")
        End Try
    End Sub

    Private Sub MB_打开目录_Click(sender As Object, e As EventArgs) Handles MB_打开目录.Click, MB_空状态打开目录.Click
        Try
            Directory.CreateDirectory(插件管理.插件文件夹路径)
            Dim startInfo As New ProcessStartInfo With {.FileName = "explorer.exe", .UseShellExecute = True}
            startInfo.ArgumentList.Add(插件管理.插件文件夹路径)
            Process.Start(startInfo)
        Catch ex As Exception
            ExOverlayMsgBox(FormMain_v6, $"打开插件目录失败：{ex.Message}", MsgBoxStyle.Critical, "插件管理")
        End Try
    End Sub

    Private Sub MB_重启应用_Click(sender As Object, e As EventArgs) Handles MB_重启应用.Click
        Dim result = ExOverlayMsgBox(FormMain_v6, "确定要重启 FFmpegFreeUI 以应用插件启用状态吗？", {"重启", "取消"}, "重启应用", MsgBoxStyle.Question, 1)
        If result = 0 Then System.Windows.Forms.Application.Restart()
    End Sub

    Private Sub UDLV_插件列表_SelectedIndexChanged(sender As Object, e As EventArgs) Handles UDLV_插件列表.SelectedIndexChanged
        显示选中插件详情()
    End Sub

    Private Sub UDLV_插件列表_SizeChanged(sender As Object, e As EventArgs) Handles UDLV_插件列表.SizeChanged
        调整列宽()
    End Sub

    Private Sub 调整列宽()
        If UDLV_插件列表.Columns.Count < 6 OrElse UDLV_插件列表.ClientSize.Width <= 0 Then Exit Sub
        Dim fixedWidth = 54 + 72 + 100 + 110 + 132
        Dim available = UDLV_插件列表.ClientSize.Width - UDLV_插件列表.Padding.Left - UDLV_插件列表.Padding.Right - 34
        UDLV_插件列表.Columns(2).Width = Math.Max(150, available - fixedWidth)
    End Sub
End Class
