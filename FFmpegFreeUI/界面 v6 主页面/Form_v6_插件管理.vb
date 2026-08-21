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
    Private ReadOnly TB_插件详情 As New TextBox()
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
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 142.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))

        Dim title As New Label With {
            .AutoSize = False,
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .Font = New Font("Microsoft YaHei UI", 15.0F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(230, 230, 230),
            .Text = "插件管理",
            .TextAlign = ContentAlignment.MiddleLeft
        }

        Dim toolbar As New FlowLayoutPanel With {
            .AutoSize = False,
            .BackColor = Color.Transparent,
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .Padding = New Padding(0, 5, 0, 5),
            .WrapContents = False
        }
        配置按钮(MB_切换启用, "禁用", 82)
        配置按钮(MB_上移, "上移", 62)
        配置按钮(MB_下移, "下移", 62)
        配置按钮(MB_刷新, "刷新", 62)
        配置按钮(MB_打开目录, "打开插件目录", 116)
        配置按钮(MB_重启应用, "重启并应用", 108)
        toolbar.Controls.AddRange({MB_切换启用, MB_上移, MB_下移, MB_刷新, MB_打开目录, MB_重启应用})

        UDLV_插件列表.AllowDragReorder = True
        UDLV_插件列表.BackgroundColor = Color.FromArgb(40, 220, 220, 220)
        UDLV_插件列表.BorderRadius = 10
        UDLV_插件列表.BorderSize = 0
        UDLV_插件列表.Dock = DockStyle.Fill
        UDLV_插件列表.DragSelectZoneWidth = 300
        UDLV_插件列表.GroupBorderColor = Color.Silver
        UDLV_插件列表.GroupHeight = 35
        UDLV_插件列表.HeaderBackColor = Color.Transparent
        UDLV_插件列表.HeaderBorderColor = Color.FromArgb(80, 220, 220, 220)
        UDLV_插件列表.HeaderHeight = 40
        UDLV_插件列表.ItemCornerRadius = 10
        UDLV_插件列表.ItemPadding = New Padding(10, 5, 10, 5)
        UDLV_插件列表.ItemSelectedBackColor = Color.FromArgb(40, 220, 220, 220)
        UDLV_插件列表.MultiSelect = False
        UDLV_插件列表.Padding = New Padding(5, 0, 5, 5)
        UDLV_插件列表.ScrollBarThumbColor = Color.FromArgb(40, 220, 220, 220)
        UDLV_插件列表.ScrollBarThumbHoverColor = Color.FromArgb(120, 220, 220, 220)
        UDLV_插件列表.ScrollBarTrackColor = Color.FromArgb(20, 220, 220, 220)
        UDLV_插件列表.SelectionRectBorderColor = Color.FromArgb(80, 220, 220, 220)
        UDLV_插件列表.SelectionRectFillColor = Color.FromArgb(40, 220, 220, 220)
        添加列("顺序", 60)
        添加列("状态", 74)
        添加列("插件", 230)
        添加列("接口类型", 105)
        添加列("插件版本", 115)
        添加列("Ext SDK / API", 205)
        添加列("加载状态", 170)

        TB_插件详情.BackColor = Color.FromArgb(18, 18, 18)
        TB_插件详情.BorderStyle = BorderStyle.None
        TB_插件详情.Dock = DockStyle.Fill
        TB_插件详情.Font = New Font("Microsoft YaHei UI", 9.5F)
        TB_插件详情.ForeColor = Color.FromArgb(205, 205, 205)
        TB_插件详情.Margin = New Padding(0, 10, 0, 0)
        TB_插件详情.Multiline = True
        TB_插件详情.Padding = New Padding(8)
        TB_插件详情.ReadOnly = True
        TB_插件详情.ScrollBars = ScrollBars.Vertical
        TB_插件详情.Text = "选择一个插件查看详细信息。"
        TB_插件详情.WordWrap = True

        L_说明.AutoEllipsis = True
        L_说明.BackColor = Color.Transparent
        L_说明.Dock = DockStyle.Fill
        L_说明.Font = New Font("Microsoft YaHei UI", 9.0F)
        L_说明.ForeColor = Color.FromArgb(145, 200, 200, 200)
        L_说明.Text = "同一事件中的插件按列表从上到下依次执行，不会同时发送。拖动或上移/下移后立即生效；启用与禁用需重启。"
        L_说明.TextAlign = ContentAlignment.MiddleLeft

        layout.Controls.Add(title, 0, 0)
        layout.Controls.Add(toolbar, 0, 1)
        layout.Controls.Add(UDLV_插件列表, 0, 2)
        layout.Controls.Add(TB_插件详情, 0, 3)
        layout.Controls.Add(L_说明, 0, 4)
        ModernPanel1.Controls.Add(layout)
        Controls.Add(ModernPanel1)
        ResumeLayout(False)
    End Sub

    Private Shared Sub 配置按钮(button As ModernButton, text As String, width As Integer)
        button.BackColor1 = Color.FromArgb(40, 220, 220, 220)
        button.BorderRadius = 10
        button.BorderSize = 0
        button.ForeColor = Color.CornflowerBlue
        button.HoverBackColor1 = Color.FromArgb(60, 220, 220, 220)
        button.Margin = New Padding(0, 0, 10, 0)
        button.PressedBackColor1 = Color.FromArgb(80, 220, 220, 220)
        button.Size = New Size(width, 36)
        button.Text = text
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

        If plugins.Count = 0 Then
            TB_插件详情.Text = $"{插件管理.插件文件夹路径}{Environment.NewLine}{Environment.NewLine}没有找到 *.3fui.dll 插件。"
        Else
            Dim targetIndex = plugins.FindIndex(Function(item) String.Equals(item.插件键, selectedKey, StringComparison.OrdinalIgnoreCase))
            UDLV_插件列表.SelectedIndex = If(targetIndex >= 0, targetIndex, 0)
        End If
        显示选中插件详情()
        调整列宽()
    End Sub

    Private Function 创建列表项(plugin As 插件信息_v6, displayOrder As Integer) As UltraDetailListView.ListItem
        Dim enabledText = If(plugin.已启用, "启用", "禁用")
        If plugin.等待重启 Then enabledText &= " *"
        Dim extText = "-"
        If plugin.接口类型 = 插件接口类型_v6.ExtAPI OrElse plugin.接口类型 = 插件接口类型_v6.官方与Ext Then
            extText = $"SDK {空值替代(plugin.ExtSDK程序集版本)} / API ≥ {空值替代(plugin.ExtAPI最低版本)}"
        End If
        Return New UltraDetailListView.ListItem(
            New UltraDetailListView.ListSubItem(displayOrder.ToString()),
            New UltraDetailListView.ListSubItem(enabledText),
            New UltraDetailListView.ListSubItem(空值替代(plugin.显示名称)),
            New UltraDetailListView.ListSubItem(接口类型文本(plugin.接口类型)),
            New UltraDetailListView.ListSubItem(空值替代(plugin.插件版本)),
            New UltraDetailListView.ListSubItem(extText),
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
            MB_切换启用.Text = "启用/禁用"
            Exit Sub
        End If

        MB_切换启用.Text = If(plugin.已启用, "禁用", "启用")
        Dim lines As New List(Of String) From {
            $"名称：{空值替代(plugin.显示名称)}    文件：{plugin.文件名}",
            $"接口：{接口类型文本(plugin.接口类型)}    插件版本：{空值替代(plugin.插件版本)}    程序集：{空值替代(plugin.程序集名称)} {空值替代(plugin.程序集版本)}",
            $"Ext SDK 程序集引用：{空值替代(plugin.ExtSDK程序集版本)}    推断的最低 Ext API：{空值替代(plugin.ExtAPI最低版本)}",
            $"Ext 插件 ID：{If(plugin.Ext插件标识.Count = 0, "-", String.Join("、", plugin.Ext插件标识))}",
            $"状态：{plugin.加载状态}    当前配置：{If(plugin.已启用, "启用", "禁用")}{If(plugin.等待重启, "（等待重启生效）", "")}",
            $"路径：{plugin.文件路径}"
        }
        If Not String.IsNullOrWhiteSpace(plugin.加载错误) Then lines.Add($"加载错误：{plugin.加载错误}")
        If Not String.IsNullOrWhiteSpace(plugin.元数据错误) Then lines.Add($"元数据错误：{plugin.元数据错误}")
        Dim configError = 插件管理.获取配置错误()
        If Not String.IsNullOrWhiteSpace(configError) Then lines.Add($"配置错误：{configError}")
        TB_插件详情.Text = String.Join(Environment.NewLine, lines)
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

    Private Sub MB_打开目录_Click(sender As Object, e As EventArgs) Handles MB_打开目录.Click
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
        If UDLV_插件列表.Columns.Count < 7 OrElse UDLV_插件列表.ClientSize.Width <= 0 Then Exit Sub
        Dim fixedWidth = 60 + 74 + 105 + 115 + 205 + 170
        Dim available = UDLV_插件列表.ClientSize.Width - UDLV_插件列表.Padding.Left - UDLV_插件列表.Padding.Right - 34
        UDLV_插件列表.Columns(2).Width = Math.Max(180, available - fixedWidth)
    End Sub
End Class
