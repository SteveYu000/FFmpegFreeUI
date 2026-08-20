Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Runtime.ExceptionServices
Imports System.Text.Json
Imports System.Threading

''' <summary>界面扩展相对于原生锚点的布局方式。</summary>
Public Enum Ext插件界面锚点位置_v2
    在目标之前
    在目标之后
    装饰目标控件
    容器顶部
    容器底部
End Enum

''' <summary>
''' FFmpegFreeUI 核心与可选插件宿主之间传递的中立上下文。
''' 此类型不引用 Ext Plugin SDK，因此缺少 SDK 时核心处理链仍可正常运行。
''' </summary>
Public NotInheritable Class Ext插件管线上下文_v2
    Public Property StageId As String = ""
    Public Property PresetJson As String = ""
    Public Property InputPath As String = ""
    Public Property OutputPath As String = ""
    Public Property CommandLine As String = ""
    Public Property ProcessFileName As String = ""
    Public Property TaskId As String = ""
    Public Property SurfaceId As String = ""
    Public Property PhaseName As String = ""
    Public Property IsPreview As Boolean
    Public Property ExitCode As Integer?
    Public Property TaskStatus As String = "unknown"
    Public ReadOnly Property Properties As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Friend Property 进度回调 As Action(Of String, Double?)
    Friend Property 结果回调 As Action(Of String, String, String, String, String)

    Public Sub ReportProgress(message As String, fraction As Double?)
        进度回调?.Invoke(If(message, ""), fraction)
    End Sub

    Public Sub ReportResult(pluginId As String, key As String, value As String, displayName As String, unit As String)
        结果回调?.Invoke(pluginId, key, value, displayName, unit)
    End Sub
End Class

''' <summary>核心与可选宿主之间传递的声明式命令解析上下文。</summary>
Public NotInheritable Class Ext插件命令解析上下文_v2
    Public Property PresetJson As String = ""
    Public Property InputPath As String = ""
    Public Property OutputPath As String = ""
    Public Property TaskId As String = ""
    Public Property PhaseName As String = ""
    Public Property IsPreview As Boolean
    Public ReadOnly Property Properties As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
End Class

Public Enum Ext插件命令参数位置_v2
    全局
    输入之前
    输入之后
    输出之前
    输出之后
End Enum

Public NotInheritable Class Ext插件命令参数_v2
    Public Property PluginId As String = ""
    Public Property ProviderId As String = ""
    Public Property Position As Ext插件命令参数位置_v2
    Public Property Text As String = ""
    Public Property Order As Integer
    Public Property Description As String = ""
End Class

Public Enum Ext插件命令步骤位置_v2
    原生步骤之前
    原生步骤之后
End Enum

Public NotInheritable Class Ext插件命令步骤_v2
    Public Property PluginId As String = ""
    Public Property ProviderId As String = ""
    Public Property StepId As String = ""
    Public Property DisplayName As String = ""
    Public Property ProcessFileName As String = ""
    Public Property Arguments As String = ""
    Public Property WorkingDirectory As String = ""
    Public Property Placement As Ext插件命令步骤位置_v2
    Public Property Order As Integer
    Public Property ParseFFmpegProgress As Boolean
End Class

''' <summary>核心内部使用的稳定 UI 锚点 ID。</summary>
Friend Module Ext插件界面锚点_v2
    Friend Const 视频质量控制方式 As String = "ext.parameters.video.quality.mode"
    Friend Const 视频质量参数名 As String = "ext.parameters.video.quality.parameter-name"
    Friend Const 视频质量值 As String = "ext.parameters.video.quality.value"
    Friend Const 全局质量控制之后 As String = "ext.parameters.video.quality.global.after"
    Friend Const 进阶质量控制之前 As String = "ext.parameters.video.quality.advanced.before"
    Friend Const 视频质量页底部 As String = "ext.parameters.video.quality.page.bottom"
End Module

''' <summary>参数面板动态目录的稳定 ID 规则；必须与 Ext SDK 中的同名规则保持一致。</summary>
Friend Module Ext插件参数面板ID_v2
    Friend Const 页面锚点前缀 As String = "ext.parameters.page."
    Friend Const 控件锚点前缀 As String = "ext.parameters.control."
    Friend Const 控件资源前缀 As String = "ext.parameters.control-resource."

    Friend Function 页面顶部(pageId As String) As String
        Return 页面锚点前缀 & 标准化页面ID(pageId) & ".top"
    End Function

    Friend Function 页面底部(pageId As String) As String
        Return 页面锚点前缀 & 标准化页面ID(pageId) & ".bottom"
    End Function

    Friend Function 控件(pageId As String, escapedControlPath As String) As String
        Return 控件锚点前缀 & 标准化页面ID(pageId) & "." & escapedControlPath.Trim()
    End Function

    Friend Function 控件资源(controlAnchorId As String) As String
        Dim value = If(controlAnchorId, "").Trim()
        If value.StartsWith(控件锚点前缀, StringComparison.OrdinalIgnoreCase) Then
            value = value.Substring(控件锚点前缀.Length)
        End If
        Return 控件资源前缀 & value
    End Function

    Private Function 标准化页面ID(pageId As String) As String
        Return If(pageId, "").Trim().ToLowerInvariant()
    End Function
End Module

''' <summary>核心与可选宿主共同使用的原生下拉项稳定 ID。</summary>
Friend Module Ext插件界面选项_v2
    Friend Const 视频质量未选择 As String = "ext.video-quality.none"
    Friend Const 视频质量CRF As String = "ext.video-quality.crf"
    Friend Const 视频质量VBR As String = "ext.video-quality.vbr"
    Friend Const 视频质量CQP As String = "ext.video-quality.cqp"
    Friend Const 视频质量CBR As String = "ext.video-quality.cbr"
    Friend Const 视频质量TPE As String = "ext.video-quality.tpe"
End Module

''' <summary>核心内部使用的稳定处理阶段 ID。</summary>
Friend Module Ext插件处理阶段_v2
    Friend Const 捕获预设之前 As String = "ext.preset.before-capture"
    Friend Const 捕获预设之后 As String = "ext.preset.after-capture"
    Friend Const 应用预设之前 As String = "ext.preset.before-apply"
    Friend Const 应用预设之后 As String = "ext.preset.after-apply"
    Friend Const 加入队列之前 As String = "ext.queue.before-add"
    Friend Const 准备任务之前 As String = "ext.task.before-prepare"
    Friend Const 准备任务之后 As String = "ext.task.after-prepare"
    Friend Const 构建命令之前 As String = "ext.command.before-build"
    Friend Const 构建命令之后 As String = "ext.command.after-build"
    Friend Const 启动进程之前 As String = "ext.process.before-start"
    Friend Const 进程退出之后 As String = "ext.process.after-exit"
    Friend Const 任务成功之后 As String = "ext.task.after-complete"
    Friend Const 任务失败之后 As String = "ext.task.after-failed"
    Friend Const 任务结束之后 As String = "ext.task.after-finish"
End Module

''' <summary>核心内部使用的稳定行为点 ID。</summary>
Friend Module Ext插件行为点_v2
    Friend Const 视频质量模式已变更 As String = "ext.parameters.video.quality.mode.changed"
End Module

''' <summary>核心与可选宿主之间传递的行为点上下文。</summary>
Public NotInheritable Class Ext插件行为上下文_v2
    Public Property BehaviorId As String = ""
    Public Property SurfaceId As String = ""
    Public ReadOnly Property Properties As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
End Class

''' <summary>
''' 可选 Ext Plugin API 的核心桥接层。核心只依赖本模块；只有同时检测到 SDK 与插件宿主时，
''' 才会通过反射进入引用 SDK 的实现程序集。
''' </summary>
Friend Module Ext插件扩展桥接_v2
    Private Const SDK文件名 As String = "FFmpegFreeUI.Ext.PluginSdk.dll"
    Private Const 宿主文件名 As String = "FFmpegFreeUI.Ext.PluginHost.dll"
    Private Const 宿主类型名 As String = "FFmpegFreeUI.Ext插件扩展宿主_v2"
    Private ReadOnly SDK最低版本 As New Version(2, 2, 0)
    Private ReadOnly 宿主最低版本 As New Version(2, 2, 0)

    Private ReadOnly 初始化锁 As New Object
    Private ReadOnly 方法缓存 As New Dictionary(Of String, MethodInfo)(StringComparer.Ordinal)
    Private 初始化完成 As Boolean
    Private 宿主类型 As Type

    Friend ReadOnly Property 可用 As Boolean
        Get
            确保初始化()
            Return 宿主类型 IsNot Nothing
        End Get
    End Property

    Friend Function 尝试加载Ext插件(程序集 As Assembly) As Boolean
        If 程序集 Is Nothing OrElse Not 可用 Then Return False
        Return CBool(调用宿主(NameOf(尝试加载Ext插件), 程序集))
    End Function

    Friend Sub 注册界面锚点(anchorId As String,
                         anchorControl As Control,
                         surface As Control,
                         position As Ext插件界面锚点位置_v2)
        If Not 可用 Then Exit Sub
        调用宿主(NameOf(注册界面锚点), anchorId, anchorControl, surface, position)
    End Sub

    Friend Sub 注册参数面板页面(pageId As String,
                            displayName As String,
                            pageRoot As Control,
                            surface As Control)
        If Not 可用 Then Exit Sub
        调用可选宿主(NameOf(注册参数面板页面), pageId, displayName, pageRoot, surface)
    End Sub

    Friend Sub 注册参数面板控件(controlId As String,
                            pageId As String,
                            controlPath As String,
                            controlName As String,
                            controlTypeName As String,
                            valuePropertyName As String,
                            control As Control,
                            surface As Control)
        If Not 可用 Then Exit Sub
        调用可选宿主(
            NameOf(注册参数面板控件),
            controlId,
            pageId,
            controlPath,
            controlName,
            controlTypeName,
            valuePropertyName,
            control,
            surface)
    End Sub

    Friend Sub 还原参数面板插件状态(surface As Control, values As IDictionary(Of String, String))
        If Not 可用 Then Exit Sub
        调用宿主(NameOf(还原参数面板插件状态), surface, values)
    End Sub

    Friend Function 捕获参数面板插件状态(surface As Control) As Dictionary(Of String, String)
        If Not 可用 Then Return New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Return DirectCast(调用宿主(NameOf(捕获参数面板插件状态), surface), Dictionary(Of String, String))
    End Function

    Friend Sub 执行同步阶段(stageId As String, context As Ext插件管线上下文_v2)
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        context.StageId = stageId
        If Not 可用 Then Exit Sub
        调用宿主(NameOf(执行同步阶段), stageId, context)
    End Sub

    Friend Sub 执行行为点(behaviorId As String,
                      context As Ext插件行为上下文_v2,
                      nativeAction As Action(Of Ext插件行为上下文_v2))
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        context.BehaviorId = behaviorId
        If Not 可用 Then
            nativeAction?.Invoke(context)
            Exit Sub
        End If
        调用宿主(NameOf(执行行为点), behaviorId, context, nativeAction)
    End Sub

    Friend Function 执行异步阶段Async(stageId As String,
                                 context As Ext插件管线上下文_v2,
                                 cancellationToken As CancellationToken) As Task
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        context.StageId = stageId
        If Not 可用 Then Return Task.CompletedTask
        Return DirectCast(调用宿主(NameOf(执行异步阶段Async), stageId, context, cancellationToken), Task)
    End Function

    Friend Function 解析插件命令参数(context As Ext插件命令解析上下文_v2) As List(Of Ext插件命令参数_v2)
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        If Not 可用 Then Return New List(Of Ext插件命令参数_v2)
        Return If(TryCast(调用可选宿主(NameOf(解析插件命令参数), context), List(Of Ext插件命令参数_v2)),
                  New List(Of Ext插件命令参数_v2))
    End Function

    Friend Function 解析插件命令步骤(context As Ext插件命令解析上下文_v2) As List(Of Ext插件命令步骤_v2)
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        If Not 可用 Then Return New List(Of Ext插件命令步骤_v2)
        Return If(TryCast(调用可选宿主(NameOf(解析插件命令步骤), context), List(Of Ext插件命令步骤_v2)),
                  New List(Of Ext插件命令步骤_v2))
    End Function

    Friend Function 创建预设管线上下文(stageId As String,
                                  preset As 预设数据_v6,
                                  Optional surface As Control = Nothing,
                                  Optional isPreview As Boolean = False) As Ext插件管线上下文_v2
        Return New Ext插件管线上下文_v2 With {
            .StageId = stageId,
            .PresetJson = 序列化预设(preset),
            .SurfaceId = 获取界面标识(surface),
            .IsPreview = isPreview
        }
    End Function

    Friend Function 创建任务管线上下文(stageId As String, task As 编码任务_v6) As Ext插件管线上下文_v2
        If task Is Nothing Then Throw New ArgumentNullException(NameOf(task))
        Dim result = New Ext插件管线上下文_v2 With {
            .StageId = stageId,
            .PresetJson = 序列化预设(task.预设数据),
            .InputPath = If(task.输入文件, ""),
            .OutputPath = If(task.输出文件, ""),
            .CommandLine = If(task.命令行, ""),
            .TaskId = If(task.ID, ""),
            .IsPreview = False,
            .TaskStatus = 获取任务状态(task),
            .进度回调 = Sub(message, fraction) 报告任务插件进度(task, stageId, message, fraction),
            .结果回调 = Sub(pluginId, key, value, displayName, unit)
                            task.记录插件结果(pluginId, key, value, displayName, unit)
                        End Sub
        }
        result.Properties("stepCount") = task.步骤.Count.ToString(CultureInfo.InvariantCulture)
        If task.当前步骤索引 >= 0 AndAlso task.当前步骤索引 < task.步骤.Count Then
            result.Properties("stepIndex") = task.当前步骤索引.ToString(CultureInfo.InvariantCulture)
            result.Properties("stepNumber") = (task.当前步骤索引 + 1).ToString(CultureInfo.InvariantCulture)
            result.Properties("isFinalStep") = If(task.当前步骤索引 = task.步骤.Count - 1, "true", "false")
        End If
        Return result
    End Function

    Friend Sub 应用任务管线上下文(task As 编码任务_v6, context As Ext插件管线上下文_v2)
        If task Is Nothing OrElse context Is Nothing Then Exit Sub
        If task.预设数据 IsNot Nothing OrElse Not String.IsNullOrWhiteSpace(context.PresetJson) Then
            task.预设数据 = 反序列化预设(context.PresetJson, task.预设数据)
        End If
        task.输入文件 = If(context.InputPath, "")
        task.输出文件 = If(context.OutputPath, "")
        task.命令行 = If(context.CommandLine, "")
    End Sub

    Friend Function 序列化预设(preset As 预设数据_v6) As String
        If preset Is Nothing Then Return ""
        预设管理_v6.初始化空集合(preset)
        Return JsonSerializer.Serialize(preset, JsonSO)
    End Function

    Friend Function 反序列化预设(json As String, fallback As 预设数据_v6) As 预设数据_v6
        If String.IsNullOrWhiteSpace(json) Then Return fallback
        Dim result = JsonSerializer.Deserialize(Of 预设数据_v6)(json, JsonSO)
        If result Is Nothing Then Throw New InvalidOperationException("插件处理后返回了空预设")
        预设管理_v6.初始化空集合(result)
        Return result
    End Function

    Private Function 获取界面标识(surface As Control) As String
        If surface Is Nothing OrElse Not 可用 Then Return ""
        Return CStr(调用宿主(NameOf(获取界面标识), surface))
    End Function

    Friend Function 获取参数面板标识(surface As Control) As String
        Return 获取界面标识(surface)
    End Function

    Private Sub 报告任务插件进度(task As 编码任务_v6, stageId As String, message As String, fraction As Double?)
        If task Is Nothing Then Exit Sub
        If fraction.HasValue Then
            task.进度.百分比 = Math.Min(Math.Max(fraction.Value, 0), 1)
            task.进度.进度文本 = $"{task.进度.百分比:P0}"
        End If
        If String.Equals(stageId, Ext插件处理阶段_v2.任务成功之后, StringComparison.Ordinal) Then
            task.进度.当前阶段 = "插件后处理"
        ElseIf stageId.StartsWith("ext.task.", StringComparison.Ordinal) Then
            task.进度.当前阶段 = "插件任务处理"
        Else
            task.进度.当前阶段 = "插件处理"
        End If
        If Not String.IsNullOrWhiteSpace(message) Then
            task.追加日志("[插件] " & message, 编码任务日志类别_v6.系统)
        Else
            编码队列_v6.通知任务更新(task)
        End If
    End Sub

    Private Function 获取任务状态(task As 编码任务_v6) As String
        If task Is Nothing Then Return "unknown"
        Select Case task.状态
            Case 编码任务状态_v6.未处理 : Return "pending"
            Case 编码任务状态_v6.正在处理 : Return "running"
            Case 编码任务状态_v6.已暂停 : Return "paused"
            Case 编码任务状态_v6.已完成 : Return "succeeded"
            Case 编码任务状态_v6.错误 : Return "failed"
            Case 编码任务状态_v6.已停止 : Return "canceled"
            Case Else : Return "unknown"
        End Select
    End Function

    Private Sub 确保初始化()
        If 初始化完成 Then Exit Sub
        SyncLock 初始化锁
            If 初始化完成 Then Exit Sub
            初始化完成 = True

            Dim sdkPath = Path.Combine(AppContext.BaseDirectory, SDK文件名)
            Dim hostPath = Path.Combine(AppContext.BaseDirectory, 宿主文件名)
            If Not File.Exists(sdkPath) Then
                Debug.WriteLine($"[FFmpegFreeUI Ext Plugin] 未检测到 {SDK文件名}，Ext Plugin API v2 已禁用。")
                Exit Sub
            End If
            If Not File.Exists(hostPath) Then
                Debug.WriteLine($"[FFmpegFreeUI Ext Plugin] 未检测到 {宿主文件名}，Ext Plugin API v2 已禁用。")
                Exit Sub
            End If

            Try
                Dim sdkAssembly = Assembly.LoadFrom(sdkPath)
                Dim apiType = sdkAssembly.GetType("FFmpegFreeUI.Ext.PluginSdk.ExtFFmpegFreeUIPluginApi", throwOnError:=True, ignoreCase:=False)
                Dim versionProperty = apiType.GetProperty("Version", BindingFlags.Public Or BindingFlags.Static)
                Dim sdkVersion = TryCast(versionProperty?.GetValue(Nothing), Version)
                If sdkVersion Is Nothing OrElse sdkVersion.Major <> SDK最低版本.Major OrElse sdkVersion < SDK最低版本 Then
                    Debug.WriteLine($"[FFmpegFreeUI Ext Plugin] {SDK文件名} 版本不兼容，需要 2.2.x 或更高的 2.x 版本。")
                    Exit Sub
                End If
                Dim hostAssembly = Assembly.LoadFrom(hostPath)
                Dim hostVersion = hostAssembly.GetName().Version
                If hostVersion Is Nothing OrElse hostVersion.Major <> 宿主最低版本.Major OrElse hostVersion < 宿主最低版本 Then
                    Debug.WriteLine($"[FFmpegFreeUI Ext Plugin] {宿主文件名} 版本不兼容，需要 2.2.x 或更高的 2.x 版本。")
                    Exit Sub
                End If
                If sdkVersion.Major <> hostVersion.Major OrElse sdkVersion.Minor <> hostVersion.Minor Then
                    Debug.WriteLine($"[FFmpegFreeUI Ext Plugin] SDK {sdkVersion} 与 Host {hostVersion} 能力版本不一致，Ext Plugin API v2 已禁用。")
                    Exit Sub
                End If
                宿主类型 = hostAssembly.GetType(宿主类型名, throwOnError:=True, ignoreCase:=False)
            Catch ex As Exception
                宿主类型 = Nothing
                Debug.WriteLine($"[FFmpegFreeUI Ext Plugin] Ext Plugin API v2 初始化失败，已安全禁用：{ex}")
            End Try
        End SyncLock
    End Sub

    Private Function 调用宿主(methodName As String, ParamArray arguments As Object()) As Object
        Return 调用宿主核心(methodName, True, arguments)
    End Function

    ''' <summary>
    ''' 调用 v2.3 新增的宿主入口。若用户误配了 v2.2 Host，则安全跳过新能力，
    ''' 但保留旧插件和旧处理链；当前发行包仍应始终配套部署同版本 SDK/Host。
    ''' </summary>
    Private Function 调用可选宿主(methodName As String, ParamArray arguments As Object()) As Object
        Return 调用宿主核心(methodName, False, arguments)
    End Function

    Private Function 调用宿主核心(methodName As String,
                               required As Boolean,
                               ParamArray arguments As Object()) As Object
        确保初始化()
        If 宿主类型 Is Nothing Then Return Nothing

        Dim method As MethodInfo = Nothing
        SyncLock 初始化锁
            If Not 方法缓存.TryGetValue(methodName, method) Then
                method = 宿主类型.GetMethod(
                    methodName,
                    BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static)
                If method Is Nothing Then
                    If required Then Throw New MissingMethodException(宿主类型.FullName, methodName)
                    Return Nothing
                End If
                方法缓存(methodName) = method
            End If
        End SyncLock

        Try
            Return method.Invoke(Nothing, arguments)
        Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            Throw
        End Try
    End Function
End Module
