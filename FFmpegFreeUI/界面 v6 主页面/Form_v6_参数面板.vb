Imports LakeUI

Public Class Form_v6_参数面板

    Public 私有界面_参数总览 As New Form_v6_参数面板_参数总览
    Public 私有界面_预设管理 As New Form_v6_参数面板_预设管理 With {.所属参数面板对象 = Me}
    Public 私有界面_输出文件设置 As New Form_v6_参数面板_输出文件设置
    Public 私有界面_解码参数 As New Form_v6_参数面板_解码参数
    Public 私有界面_视频编码器 As New Form_v6_参数面板_视频编码器 With {.所属参数面板对象 = Me}
    Public 私有界面_画面帧 As New Form_v6_参数面板_画面帧 With {.所属参数面板对象 = Me}
    Public 私有界面_质量 As New Form_v6_参数面板_质量 With {.所属参数面板对象 = Me}
    Public 私有界面_色彩管理 As New Form_v6_参数面板_色彩管理
    Public 私有界面_视频帧服务器 As New Form_v6_参数面板_视频帧服务器
    Public 私有界面_音频参数 As New Form_v6_参数面板_音频参数
    Public 私有界面_剪辑区间 As New Form_v6_参数面板_剪辑区间
    Public 私有界面_滤镜排序 As New Form_v6_参数面板_滤镜排序 With {.所属参数面板对象 = Me}
    Public 私有界面_自定义参数 As New Form_v6_参数面板_自定义参数
    Public 私有界面_自定义参数说明 As New Form_v6_参数面板_自定义参数说明
    Public 私有界面_流自定义参数 As New Form_v6_参数面板_流自定义参数
    Public 私有界面_在位置插入参数 As New Form_v6_参数面板_在位置插入参数
    Public 私有界面_完全自己写模式 As New Form_v6_参数面板_完全自己写模式
    Public 私有界面_流控制 As New Form_v6_参数面板_流控制 With {.所属参数面板对象 = Me}
    Public 私有界面_附加内容 As New Form_v6_参数面板_附加内容
    Public 私有界面_元数据 As New Form_v6_参数面板_元数据
    Public 私有界面_章节 As New Form_v6_参数面板_章节
    Public 私有界面_附件 As New Form_v6_参数面板_附件

    Public Shared ReadOnly 共享界面_画面区域选择窗口 As New Form_v6_参数面板_画面区域选择窗口
    Public 抑制自动刷新 As Boolean = False
    Private Const 参数总览页索引 As Integer = 0
    Private Const 滤镜排序页索引 As Integer = 15
    Private _正在刷新聚合页面 As Boolean
    Private _插件参数面板目录已注册 As Boolean


    Private Sub Form_v6_参数面板_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.ModernTabListControl1.Items(0).BoundControl = 私有界面_参数总览
        绑定选项卡(私有界面_参数总览.ModernPanel1)
        Me.ModernTabListControl1.Items(1).BoundControl = 私有界面_预设管理
        绑定选项卡(私有界面_预设管理.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.Items(3).BoundControl = 私有界面_输出文件设置
        绑定选项卡(私有界面_输出文件设置.ModernPanel1)
        Me.ModernTabListControl1.Items(4).BoundControl = 私有界面_解码参数
        绑定选项卡(私有界面_解码参数.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.Items(6).BoundControl = 私有界面_视频编码器
        绑定选项卡(私有界面_视频编码器.ModernPanel1)
        Me.ModernTabListControl1.Items(7).BoundControl = 私有界面_画面帧
        私有界面_画面帧.私有窗口_着色器超分.所属参数面板对象 = Me
        绑定选项卡(私有界面_画面帧.ModernPanel1)
        Me.ModernTabListControl1.Items(8).BoundControl = 私有界面_质量
        绑定选项卡(私有界面_质量.ModernPanel1)
        Me.ModernTabListControl1.Items(9).BoundControl = 私有界面_色彩管理
        绑定选项卡(私有界面_色彩管理.ModernPanel1)
        Me.ModernTabListControl1.Items(10).BoundControl = 私有界面_视频帧服务器
        私有界面_视频帧服务器.所属参数面板对象 = Me
        绑定选项卡(私有界面_视频帧服务器.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.Items(12).BoundControl = 私有界面_音频参数
        绑定选项卡(私有界面_音频参数.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.Items(14).BoundControl = 私有界面_剪辑区间
        绑定选项卡(私有界面_剪辑区间.ModernPanel1)
        Me.ModernTabListControl1.Items(15).BoundControl = 私有界面_滤镜排序
        绑定选项卡(私有界面_滤镜排序.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.Items(16).BoundControl = 私有界面_自定义参数
        绑定选项卡(私有界面_自定义参数.ModernPanel1)
        If SP_UnLock AndAlso 设置_v6.实例对象.窗口样式 = 2 AndAlso 设置_v6.实例对象.SP_毛玻璃模式 > 0 Then
            私有界面_自定义参数.ModernTabControl1.TabStripBackColor = Color.Transparent
            私有界面_自定义参数.ModernTabControl1.ContentBackColor = Color.Transparent
            私有界面_自定义参数说明.ModernPanel1.Padding = New Padding(20, 0, 20, 20)
            私有界面_流自定义参数.ModernPanel1.Padding = New Padding(20, 0, 20, 20)
            私有界面_在位置插入参数.ModernPanel1.Padding = New Padding(20, 0, 20, 20)
            私有界面_完全自己写模式.ModernPanel1.Padding = New Padding(20, 0, 20, 20)
        End If
        Me.私有界面_自定义参数.ModernTabControl1.Items(0).BoundControl = 私有界面_自定义参数说明
        绑定选项卡(私有界面_自定义参数说明.ModernPanel1)
        Me.私有界面_自定义参数.ModernTabControl1.Items(2).BoundControl = 私有界面_流自定义参数
        绑定选项卡(私有界面_流自定义参数.ModernPanel1)
        Me.私有界面_自定义参数.ModernTabControl1.Items(3).BoundControl = 私有界面_在位置插入参数
        绑定选项卡(私有界面_在位置插入参数.ModernPanel1)
        Me.私有界面_自定义参数.ModernTabControl1.Items(5).BoundControl = 私有界面_完全自己写模式
        绑定选项卡(私有界面_完全自己写模式.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.Items(17).BoundControl = 私有界面_流控制
        绑定选项卡(私有界面_流控制.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.Items(19).BoundControl = 私有界面_附加内容
        绑定选项卡(私有界面_附加内容.ModernPanel1)
        If SP_UnLock AndAlso 设置_v6.实例对象.窗口样式 = 2 AndAlso 设置_v6.实例对象.SP_毛玻璃模式 > 0 Then
            私有界面_附加内容.ModernTabControl1.TabStripBackColor = Color.Transparent
            私有界面_附加内容.ModernTabControl1.ContentBackColor = Color.Transparent
            私有界面_元数据.ModernPanel1.Padding = New Padding(20, 0, 20, 20)
            私有界面_章节.ModernPanel1.Padding = New Padding(20, 0, 20, 20)
            私有界面_附件.ModernPanel1.Padding = New Padding(20, 0, 20, 20)
        End If
        Me.私有界面_附加内容.ModernTabControl1.Items(0).BoundControl = 私有界面_元数据
        私有界面_元数据.所属参数面板对象 = Me
        绑定选项卡(私有界面_元数据.ModernPanel1)
        Me.私有界面_附加内容.ModernTabControl1.Items(1).BoundControl = 私有界面_章节
        私有界面_章节.所属参数面板对象 = Me
        绑定选项卡(私有界面_章节.ModernPanel1)
        Me.私有界面_附加内容.ModernTabControl1.Items(2).BoundControl = 私有界面_附件
        私有界面_附件.所属参数面板对象 = Me
        绑定选项卡(私有界面_附件.ModernPanel1)
        '==================================================
        Me.ModernTabListControl1.SelectedIndex = 0
        Me.私有界面_自定义参数.ModernTabControl1.SelectedIndex = 0
        Me.私有界面_附加内容.ModernTabControl1.SelectedIndex = 0
        '==================================================
        确保注册插件参数面板目录()
        预设管理_v6.重置面板(Me)
        私有界面_预设管理.刷新预设列表()
    End Sub

    Private Sub Form_v6_参数面板_Shown(sender As Object, e As EventArgs) Handles Me.Shown

    End Sub

    Private Sub 绑定选项卡(选项卡的根面板容器 As ModernPanel)
        If SP_UnLock Then
            Select Case 设置_v6.实例对象.SP_毛玻璃模式
                Case > 0
                    选项卡的根面板容器.BackColor = Color.Transparent
                    选项卡的根面板容器.BackColor1 = Color.Transparent
                    选项卡的根面板容器.BackgroundSource = Me.ParentForm
            End Select
        End If
    End Sub

    Public Shared Sub 弹出画面区域选择窗口(完成按钮返回的控件 As Control, 标题栏 As String)
        If 共享界面_画面区域选择窗口.目标控件 IsNot Nothing Then
            ExFloatingTip("画面区域选择窗口正在使用中，请关闭后再试，为了节约性能这个窗口只能打开一个", 3000)
            Exit Sub
        End If
        共享界面_画面区域选择窗口.目标控件 = 完成按钮返回的控件
        共享界面_画面区域选择窗口.Text = 标题栏
        显示窗体(共享界面_画面区域选择窗口, FormMain_v6)
    End Sub

    Public Sub 请求刷新参数状态()
        If 抑制自动刷新 OrElse IsDisposed Then Exit Sub
        Select Case ModernTabListControl1.SelectedIndex
            Case 参数总览页索引
                刷新参数总览页()
            Case 滤镜排序页索引
                刷新滤镜排序页()
        End Select
    End Sub

    Private Sub ModernTabListControl1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ModernTabListControl1.SelectedIndexChanged
        If 抑制自动刷新 OrElse IsDisposed OrElse _正在刷新聚合页面 Then Exit Sub

        Select Case ModernTabListControl1.SelectedIndex
            Case 参数总览页索引
                刷新参数总览页()
            Case 滤镜排序页索引
                刷新滤镜排序页()
        End Select
    End Sub

    Private Sub 刷新参数总览页()
        If _正在刷新聚合页面 Then Exit Sub
        _正在刷新聚合页面 = True
        Try
            预设管理_v6.刷新参数总览(Me)
        Finally
            _正在刷新聚合页面 = False
        End Try
    End Sub

    Private Sub 刷新滤镜排序页()
        If _正在刷新聚合页面 Then Exit Sub
        _正在刷新聚合页面 = True
        Try
            预设管理_v6.同步全部内置滤镜到排序(Me, False)
        Finally
            _正在刷新聚合页面 = False
        End Try
    End Sub

    ''' <summary>
    ''' 把参数面板全部原生控件注册为可发现的装饰/替换锚点，并为每个一级参数页提供顶部和底部插入槽。
    ''' 此目录只扫描宿主原生控件；插件随后加入的控件归插件自己管理，不会被再次公开成宿主资源。
    ''' </summary>
    Friend Sub 确保注册插件参数面板目录()
        If _插件参数面板目录已注册 Then Exit Sub
        _插件参数面板目录已注册 = True

        Dim pages As (Id As String, DisplayName As String, Page As Control)() = {
            ("overview", "参数总览", 私有界面_参数总览),
            ("presets", "预设管理", 私有界面_预设管理),
            ("output", "输出文件设置", 私有界面_输出文件设置),
            ("decoder", "解码参数", 私有界面_解码参数),
            ("video-encoder", "视频编码器", 私有界面_视频编码器),
            ("video-frame", "画面与帧", 私有界面_画面帧),
            ("video-quality", "视频质量", 私有界面_质量),
            ("color", "色彩管理", 私有界面_色彩管理),
            ("frame-server", "视频帧服务器", 私有界面_视频帧服务器),
            ("audio", "音频参数", 私有界面_音频参数),
            ("trim", "剪辑区间", 私有界面_剪辑区间),
            ("filter-order", "滤镜排序", 私有界面_滤镜排序),
            ("custom", "自定义参数", 私有界面_自定义参数),
            ("custom-help", "自定义参数说明", 私有界面_自定义参数说明),
            ("custom-stream", "流自定义参数", 私有界面_流自定义参数),
            ("custom-position", "在位置插入参数", 私有界面_在位置插入参数),
            ("custom-full", "完全自己写模式", 私有界面_完全自己写模式),
            ("stream-control", "流控制", 私有界面_流控制),
            ("additional", "附加内容", 私有界面_附加内容),
            ("metadata", "元数据", 私有界面_元数据),
            ("chapters", "章节", 私有界面_章节),
            ("attachments", "附件", 私有界面_附件),
            ("video-frame-extract", "抽帧参数", 私有界面_画面帧.私有窗口_抽帧参数),
            ("video-frame-interpolate", "插帧参数", 私有界面_画面帧.私有窗口_插帧参数),
            ("video-frame-burn-subtitles", "烧录字幕", 私有界面_画面帧.私有窗口_烧录字幕),
            ("video-frame-denoise", "降噪", 私有界面_画面帧.私有窗口_降噪),
            ("video-frame-sharpen", "锐化", 私有界面_画面帧.私有窗口_锐化),
            ("video-frame-film-grain", "胶片颗粒", 私有界面_画面帧.私有窗口_胶片颗粒),
            ("video-frame-dynamic-blur", "动态模糊", 私有界面_画面帧.私有窗口_动态模糊),
            ("video-frame-super-resolution", "超分", 私有界面_画面帧.私有窗口_着色器超分),
            ("video-frame-scan-mode", "扫描方式", 私有界面_画面帧.私有窗口_扫描方式),
            ("video-frame-flip", "画面翻转", 私有界面_画面帧.私有窗口_画面翻转),
            ("video-frame-deband", "平滑断层", 私有界面_画面帧.私有窗口_平滑断层),
            ("video-frame-crop-selector", "画面区域选择", 共享界面_画面区域选择窗口)
        }

        Dim visited As New HashSet(Of Control)(ReferenceEqualityComparer.Instance)
        For Each page In pages
            Dim pageRoot = 获取插件页面根容器(page.Page)
            Ext插件扩展桥接_v2.注册参数面板页面(page.Id, page.DisplayName, pageRoot, Me)
            注册插件页面控件(page.Id, pageRoot, "", visited)
        Next
    End Sub

    Private Shared Function 获取插件页面根容器(page As Control) As Control
        If page Is Nothing Then Return Nothing
        Dim roots = page.Controls.Find("ModernPanel1", True)
        Return If(roots.Length > 0, roots(0), page)
    End Function

    Private Sub 注册插件页面控件(pageId As String,
                            control As Control,
                            parentPath As String,
                            visited As HashSet(Of Control),
                            Optional pathSegment As String = "")
        If control Is Nothing OrElse Not visited.Add(control) Then Exit Sub

        Dim segment = pathSegment
        If segment = "" Then segment = Uri.EscapeDataString(获取插件控件名称(control))
        Dim currentPath = If(parentPath = "", segment, parentPath & "/" & segment)
        Dim controlId = Ext插件参数面板ID_v2.控件(pageId, currentPath)
        Ext插件扩展桥接_v2.注册参数面板控件(
            controlId,
            pageId,
            currentPath,
            If(control.Name, ""),
            If(control.GetType().FullName, control.GetType().Name),
            获取插件控件默认值属性(control),
            control,
            Me)

        Dim children = control.Controls.Cast(Of Control)().ToList()
        children.RemoveAll(Function(child) TypeOf child Is Form)
        Dim totals = children.
            GroupBy(Function(child) 获取插件控件名称(child), StringComparer.OrdinalIgnoreCase).
            ToDictionary(Function(group) group.Key, Function(group) group.Count(), StringComparer.OrdinalIgnoreCase)
        Dim occurrences As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For Each child In children
            Dim childName = 获取插件控件名称(child)
            Dim occurrence = occurrences.GetValueOrDefault(childName)
            occurrences(childName) = occurrence + 1
            Dim childSegment = Uri.EscapeDataString(childName)
            If totals(childName) > 1 Then childSegment &= $"[{occurrence}]"
            注册插件页面控件(pageId, child, currentPath, visited, childSegment)
        Next
    End Sub

    Private Shared Function 获取插件控件名称(control As Control) As String
        Dim result = If(control?.Name, "").Trim()
        If result = "" Then result = control.GetType().Name
        Return result
    End Function

    Private Shared Function 获取插件控件默认值属性(control As Control) As String
        Dim type = control.GetType()
        Dim checkedProperty = type.GetProperty("Checked", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public Or Reflection.BindingFlags.IgnoreCase)
        If checkedProperty IsNot Nothing AndAlso checkedProperty.CanRead AndAlso checkedProperty.PropertyType Is GetType(Boolean) Then Return "Checked"
        Dim valueProperty = type.GetProperty("Value", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public Or Reflection.BindingFlags.IgnoreCase)
        If type.Name.Contains("TrackBar", StringComparison.OrdinalIgnoreCase) AndAlso valueProperty IsNot Nothing AndAlso valueProperty.CanRead Then Return "Value"
        Dim textProperty = type.GetProperty("Text", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public Or Reflection.BindingFlags.IgnoreCase)
        If textProperty IsNot Nothing AndAlso textProperty.CanRead Then Return "Text"
        If valueProperty IsNot Nothing AndAlso valueProperty.CanRead Then Return "Value"
        Dim selectedIndexProperty = type.GetProperty("SelectedIndex", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.Public Or Reflection.BindingFlags.IgnoreCase)
        If selectedIndexProperty IsNot Nothing AndAlso selectedIndexProperty.CanRead Then Return "SelectedIndex"
        Return ""
    End Function

End Class
