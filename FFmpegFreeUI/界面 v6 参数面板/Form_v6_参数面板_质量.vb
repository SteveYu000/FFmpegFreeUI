Imports System.ComponentModel

Public Class Form_v6_参数面板_质量

    Public 所属参数面板对象 As Form_v6_参数面板
    Private 正在同步质量控制方式 As Boolean = False
    Private 预制条目菜单已初始化 As Boolean = False
    Private ReadOnly 插件质量选项 As New Dictionary(Of String, 插件质量选项描述)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly 插件质量选项顺序 As New List(Of String)
    Private 正在重排插件质量选项 As Boolean

    Private Sub Form_v6_参数面板_质量_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        初始化质量参数名下拉框()
        初始化预制条目菜单()
        注册插件界面锚点()
    End Sub

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Sub 注册插件质量选项(choiceId As String, displayText As String, nativeFallbackChoiceId As String)
        Dim id = If(choiceId, "").Trim()
        If id = "" Then Throw New ArgumentException("ChoiceId 不能为空", NameOf(choiceId))
        If 获取原生质量选项索引(id) >= 0 OrElse 插件质量选项.ContainsKey(id) Then
            Throw New InvalidOperationException($"质量选项 {id} 已存在")
        End If
        If 获取原生质量选项索引(nativeFallbackChoiceId) < 0 Then
            Throw New ArgumentException($"原生回退选项 {nativeFallbackChoiceId} 不存在", NameOf(nativeFallbackChoiceId))
        End If

        插件质量选项(id) = New 插件质量选项描述 With {
            .ChoiceId = id,
            .DisplayText = If(displayText, "").Trim(),
            .NativeFallbackChoiceId = nativeFallbackChoiceId.Trim()
        }
        插件质量选项顺序.Add(id)
        MCB_全局质量控制方式.Items.Add(displayText)
    End Sub

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Sub 设置插件质量选项(items As IEnumerable(Of String()))
        Dim selectedId = 获取当前质量选项ID()
        Dim selectedFallback = 获取当前原生质量选项ID()
        正在重排插件质量选项 = True
        正在同步质量控制方式 = True
        Try
            For index = MCB_全局质量控制方式.Items.Count - 1 To 原生质量选项数量 Step -1
                MCB_全局质量控制方式.Items.RemoveAt(index)
            Next
            插件质量选项.Clear()
            插件质量选项顺序.Clear()

            For Each values In If(items, Array.Empty(Of String())())
                If values Is Nothing OrElse values.Length < 3 Then Continue For
                注册插件质量选项(values(0), values(1), values(2))
            Next

            Dim selectedIndex = 获取质量选项显示索引(selectedId)
            If selectedIndex < 0 Then selectedIndex = 获取原生质量选项索引(selectedFallback)
            MCB_全局质量控制方式.SelectedIndex = Math.Max(0, selectedIndex)
        Finally
            正在同步质量控制方式 = False
            正在重排插件质量选项 = False
        End Try
    End Sub

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Sub 注销插件质量选项(choiceId As String)
        Dim id = If(choiceId, "").Trim()
        Dim item As 插件质量选项描述 = Nothing
        If Not 插件质量选项.TryGetValue(id, item) Then Exit Sub

        Dim selected = 获取当前质量选项ID()
        Dim itemIndex = 获取插件质量选项索引(id)
        If String.Equals(selected, id, StringComparison.OrdinalIgnoreCase) Then
            选择质量选项(item.NativeFallbackChoiceId)
        End If
        If itemIndex >= 0 AndAlso itemIndex < MCB_全局质量控制方式.Items.Count Then
            MCB_全局质量控制方式.Items.RemoveAt(itemIndex)
        End If
        插件质量选项.Remove(id)
        插件质量选项顺序.RemoveAll(Function(value) String.Equals(value, id, StringComparison.OrdinalIgnoreCase))
    End Sub

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Function 获取当前质量选项ID() As String
        Dim index = MCB_全局质量控制方式.SelectedIndex
        Dim nativeId = 获取原生质量选项ID(index)
        If nativeId <> "" Then Return nativeId

        Dim offset = index - 原生质量选项数量
        If offset >= 0 AndAlso offset < 插件质量选项顺序.Count Then Return 插件质量选项顺序(offset)
        Return Ext插件界面选项_v2.视频质量未选择
    End Function

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Function 获取当前原生质量选项ID() As String
        Dim selectedId = 获取当前质量选项ID()
        Dim item As 插件质量选项描述 = Nothing
        If 插件质量选项.TryGetValue(selectedId, item) Then Return item.NativeFallbackChoiceId
        Return selectedId
    End Function

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Sub 选择质量选项(choiceId As String)
        Dim index = 获取原生质量选项索引(choiceId)
        If index < 0 Then index = 获取插件质量选项索引(choiceId)
        If index < 0 Then index = 0
        MCB_全局质量控制方式.SelectedIndex = index
    End Sub

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Function 获取质量选项显示索引(choiceId As String) As Integer
        Dim index = 获取原生质量选项索引(choiceId)
        If index >= 0 Then Return index
        Return 获取插件质量选项索引(choiceId)
    End Function

    <EditorBrowsable(EditorBrowsableState.Never)>
    Public Function 正在更新插件质量选项() As Boolean
        Return 正在重排插件质量选项
    End Function

    Private Const 原生质量选项数量 As Integer = 6

    Private Function 获取插件质量选项索引(choiceId As String) As Integer
        For index = 0 To 插件质量选项顺序.Count - 1
            If String.Equals(插件质量选项顺序(index), choiceId, StringComparison.OrdinalIgnoreCase) Then
                Return 原生质量选项数量 + index
            End If
        Next
        Return -1
    End Function

    Private Shared Function 获取原生质量选项索引(choiceId As String) As Integer
        Select Case If(choiceId, "").Trim().ToLowerInvariant()
            Case Ext插件界面选项_v2.视频质量未选择 : Return 0
            Case Ext插件界面选项_v2.视频质量CRF : Return 1
            Case Ext插件界面选项_v2.视频质量VBR : Return 2
            Case Ext插件界面选项_v2.视频质量CQP : Return 3
            Case Ext插件界面选项_v2.视频质量CBR : Return 4
            Case Ext插件界面选项_v2.视频质量TPE : Return 5
            Case Else : Return -1
        End Select
    End Function

    Private Shared Function 获取原生质量选项ID(index As Integer) As String
        Select Case index
            Case 0 : Return Ext插件界面选项_v2.视频质量未选择
            Case 1 : Return Ext插件界面选项_v2.视频质量CRF
            Case 2 : Return Ext插件界面选项_v2.视频质量VBR
            Case 3 : Return Ext插件界面选项_v2.视频质量CQP
            Case 4 : Return Ext插件界面选项_v2.视频质量CBR
            Case 5 : Return Ext插件界面选项_v2.视频质量TPE
            Case Else : Return ""
        End Select
    End Function

    Private NotInheritable Class 插件质量选项描述
        Public Property ChoiceId As String
        Public Property DisplayText As String
        Public Property NativeFallbackChoiceId As String
    End Class

    Private Sub 注册插件界面锚点()
        If 所属参数面板对象 Is Nothing Then Exit Sub
        Ext插件扩展桥接_v2.注册界面锚点(
            Ext插件界面锚点_v2.视频质量控制方式,
            MCB_全局质量控制方式,
            所属参数面板对象,
            Ext插件界面锚点位置_v2.装饰目标控件)
        Ext插件扩展桥接_v2.注册界面锚点(
            Ext插件界面锚点_v2.视频质量参数名,
            MCB_质量参数名称,
            所属参数面板对象,
            Ext插件界面锚点位置_v2.装饰目标控件)
        Ext插件扩展桥接_v2.注册界面锚点(
            Ext插件界面锚点_v2.视频质量值,
            MTB_质量值,
            所属参数面板对象,
            Ext插件界面锚点位置_v2.装饰目标控件)
        Ext插件扩展桥接_v2.注册界面锚点(
            Ext插件界面锚点_v2.全局质量控制之后,
            Panel2,
            所属参数面板对象,
            Ext插件界面锚点位置_v2.在目标之后)
        Ext插件扩展桥接_v2.注册界面锚点(
            Ext插件界面锚点_v2.进阶质量控制之前,
            HCL_进阶质量控制,
            所属参数面板对象,
            Ext插件界面锚点位置_v2.在目标之前)
        Ext插件扩展桥接_v2.注册界面锚点(
            Ext插件界面锚点_v2.视频质量页底部,
            JustEmptyControl8,
            所属参数面板对象,
            Ext插件界面锚点位置_v2.在目标之后)
    End Sub

    Private Sub 初始化质量参数名下拉框()
        Dim 当前文本 = MCB_质量参数名称.Text
        MCB_质量参数名称.Items.Clear()

        For Each 参数名 In 视频编码器数据库_v6.获取质量参数名列表()
            MCB_质量参数名称.Items.Add(参数名)
        Next

        If 当前文本 <> "" Then MCB_质量参数名称.Text = 当前文本
    End Sub

    Private Sub 初始化预制条目菜单()
        If 预制条目菜单已初始化 Then Exit Sub
        预制条目菜单已初始化 = True

        添加说明项(预制条目菜单, "前向参考帧")
        添加预制条目("-rc-lookahead 前向参考帧数 适用于 Nvidia/libx264", "-rc-lookahead ")
        添加预制条目("-look_ahead_depth 前向参考帧数 适用于 Intel", "-look_ahead_depth ")
        添加预制条目("-la_depth 前向参考帧数 适用于 libsvtav1", "-la_depth ")
        添加分割线(预制条目菜单)

        添加说明项(预制条目菜单, "GOP 和帧类型")
        添加预制条目("-g 关键帧 (i) 间隔", "-g ")
        添加预制条目("-bf 双向预测帧 (b) 数量", "-bf ")
        添加预制条目("-qp_i 关键帧质量", "-qp_i ")
        添加预制条目("-qp_p 前向参考帧质量", "-qp_p ")
        添加预制条目("-qp_b 双向参考帧质量", "-qp_b ")
        添加分割线(预制条目菜单)

        添加说明项(预制条目菜单, "量化控制")
        添加预制条目("-qmin 量化最小值（最高画质）", "-qmin ")
        添加预制条目("-qpmin 量化最小值（最高画质）", "-qpmin ")
        添加预制条目("-qmax 量化最大值（最低画质）", "-qmax ")
        添加预制条目("-qpmax 量化最大值（最低画质）", "-qpmax ")
        添加预制条目("-qcomp 量化系数非线性压缩因子 0.0~1.0", "-qcomp ")
        添加预制条目("-qvbr_quality_level AMD QVBR 质量级别", "-qvbr_quality_level ")
        添加分割线(预制条目菜单)

        添加说明项(预制条目菜单, "编码器专项")
        添加预制条目("-extbrc 1 启用激进比特率分配 适用于 Intel", "-extbrc 1")
        添加预制条目("-spatial-aq 1 启用 NVENC 空间 AQ", "-spatial-aq 1")
        添加预制条目("-temporal-aq 1 启用 NVENC 时间 AQ", "-temporal-aq 1")
        添加预制条目("-level 编码级别 较少使用", "-level ")
    End Sub

    Private Sub 添加说明项(menu As LakeUI.ModernContextMenu, text As String)
        menu.Items.Add(New LakeUI.ModernContextMenu.ModernMenuItem(text) With {.IsDescription = True})
    End Sub

    Private Sub 添加分割线(menu As LakeUI.ModernContextMenu)
        menu.Items.Add(New LakeUI.ModernContextMenu.ModernMenuItem() With {.IsSeparator = True})
    End Sub

    Private Sub 添加预制条目(menuText As String, insertText As String)
        Dim item As New LakeUI.ModernContextMenu.ModernMenuItem(menuText)
        AddHandler item.Click, Sub()
                                   插入进阶质量参数(insertText)
                               End Sub
        预制条目菜单.Items.Add(item)
    End Sub

    Private Sub 插入进阶质量参数(text As String)
        If String.IsNullOrEmpty(text) Then Exit Sub

        Dim insertText = text
        If MTB_进阶质量控制参数.Text <> "" Then
            Dim caret = MTB_进阶质量控制参数.SelectionStart
            Dim needsSpace = caret > 0 AndAlso Not Char.IsWhiteSpace(MTB_进阶质量控制参数.Text(caret - 1))
            If needsSpace Then insertText = " " & insertText
        End If

        MTB_进阶质量控制参数.SelectedText = insertText
        MTB_进阶质量控制参数.Focus()
        通知参数面板刷新()
    End Sub

    Private Sub MCB_全局质量控制方式_SelectedIndexChanged(sender As Object, e As EventArgs) Handles MCB_全局质量控制方式.SelectedIndexChanged
        If 正在同步质量控制方式 Then Exit Sub

        正在同步质量控制方式 = True
        Try
            Dim context As New Ext插件行为上下文_v2 With {
                .BehaviorId = Ext插件行为点_v2.视频质量模式已变更,
                .SurfaceId = Ext插件扩展桥接_v2.获取参数面板标识(所属参数面板对象)
            }
            context.Properties("selectedChoiceId") = 获取当前质量选项ID()
            context.Properties("nativeChoiceId") = 获取当前原生质量选项ID()
            context.Properties("parameterName") = MCB_质量参数名称.Text
            context.Properties("qualityValue") = MTB_质量值.Text

            Ext插件扩展桥接_v2.执行行为点(
                Ext插件行为点_v2.视频质量模式已变更,
                context,
                AddressOf 执行原生质量模式联动)

            If context.Properties.TryGetValue("parameterName", Nothing) Then
                MCB_质量参数名称.Text = context.Properties("parameterName")
            End If
            If context.Properties.TryGetValue("qualityValue", Nothing) Then
                MTB_质量值.Text = context.Properties("qualityValue")
            End If
        Finally
            正在同步质量控制方式 = False
        End Try

        通知参数面板刷新()
    End Sub

    Private Sub 执行原生质量模式联动(context As Ext插件行为上下文_v2)
        Dim nativeChoiceId = If(context.Properties.GetValueOrDefault("nativeChoiceId"), "")
        Select Case nativeChoiceId
            Case Ext插件界面选项_v2.视频质量未选择
                context.Properties("parameterName") = ""
                context.Properties("qualityValue") = ""
            Case Ext插件界面选项_v2.视频质量CRF
                context.Properties("parameterName") = "-crf"
            Case Ext插件界面选项_v2.视频质量VBR
                context.Properties("parameterName") = "-cq"
            Case Ext插件界面选项_v2.视频质量CQP
                context.Properties("parameterName") = If(当前视频编码器是AMF(), "", "-qp")
            Case Ext插件界面选项_v2.视频质量CBR, Ext插件界面选项_v2.视频质量TPE
                context.Properties("parameterName") = ""
                context.Properties("qualityValue") = ""
        End Select
    End Sub

    Public Sub 同步当前编码器质量参数名()
        If Not String.Equals(获取当前原生质量选项ID(), Ext插件界面选项_v2.视频质量CQP, StringComparison.OrdinalIgnoreCase) Then Exit Sub
        If 当前视频编码器是AMF() AndAlso String.Equals(MCB_质量参数名称.Text.Trim().TrimStart("-"c), "qp", StringComparison.OrdinalIgnoreCase) Then
            MCB_质量参数名称.Text = ""
            通知参数面板刷新()
        End If
    End Sub

    Private Function 当前视频编码器是AMF() As Boolean
        Dim 编码器 = If(所属参数面板对象?.私有界面_视频编码器?.MCB_具体编码器.Text, "").Trim()
        Return 编码器.EndsWith("_amf", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub MB_插入预制条目_Click(sender As Object, e As EventArgs) Handles MB_插入预制条目.Click
        初始化预制条目菜单()
        Dim labelPoint = HCL_全局质量控制.PointToScreen(Point.Empty)
        Dim formPoint = FormMain_v6.PointToScreen(Point.Empty)
        预制条目菜单.Show(labelPoint.X, formPoint.Y)
    End Sub

    Private Sub 通知参数面板刷新()
        If 所属参数面板对象 Is Nothing OrElse 所属参数面板对象.抑制自动刷新 Then Exit Sub
        所属参数面板对象.请求刷新参数状态()
    End Sub

End Class
