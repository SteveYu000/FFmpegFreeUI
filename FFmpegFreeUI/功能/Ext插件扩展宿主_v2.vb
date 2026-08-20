Imports System.Diagnostics
Imports System.Drawing
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Text.Json
Imports System.Threading
Imports System.Windows.Forms
Imports FFmpegFreeUI.Ext.PluginSdk
Imports LakeUI

''' <summary>
''' FFmpegFreeUI Ext 插件 API v2 的内部宿主。公开给插件的对象均由插件 ID 限定作用域，插件不能冒充其他插件。
''' </summary>
Friend Module Ext插件扩展宿主_v2

    Private ReadOnly 支持API版本 As New Version(2, 3, 0)
    Private ReadOnly 同步锁 As New Object
    Private ReadOnly 界面扩展列表 As New List(Of 已注册界面扩展)
    Private ReadOnly 安全下拉项列表 As New List(Of 已注册安全下拉项)
    Private ReadOnly 处理器列表 As New List(Of 已注册处理器)
    Private ReadOnly 行为处理器列表 As New List(Of 已注册行为处理器)
    Private ReadOnly 资源声明列表 As New List(Of 已注册资源声明)
    Private ReadOnly 命令参数提供器列表 As New List(Of 已注册命令参数提供器)
    Private ReadOnly 命令步骤提供器列表 As New List(Of 已注册命令步骤提供器)
    Private ReadOnly 界面锚点列表 As New List(Of 已注册界面锚点)
    Private ReadOnly 参数页面目录 As New Dictionary(Of String, ExtPluginParameterPageDescriptor)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly 参数控件目录 As New Dictionary(Of String, ExtPluginParameterControlDescriptor)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly 参数面板状态表 As New ConditionalWeakTable(Of Form_v6_参数面板, 参数面板插件状态)
    Private ReadOnly 插件实例表 As New Dictionary(Of String, IExtFFmpegFreeUIPlugin)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly 插件宿主表 As New Dictionary(Of String, IExtFFmpegFreeUIHost)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>尝试从程序集发现并初始化 v2 插件；没有 v2 入口时返回 False。</summary>
    Friend Function 尝试加载Ext插件(程序集 As Assembly) As Boolean
        If 程序集 Is Nothing Then Throw New ArgumentNullException(NameOf(程序集))
        Dim pluginTypes = 获取可加载类型(程序集).
            Where(Function(type) GetType(IExtFFmpegFreeUIPlugin).IsAssignableFrom(type) AndAlso
                                 Not type.IsAbstract AndAlso
                                 Not type.IsInterface AndAlso
                                 type.GetConstructor(Type.EmptyTypes) IsNot Nothing).
            OrderBy(Function(type) type.FullName, StringComparer.Ordinal).
            ToList()
        If pluginTypes.Count = 0 Then Return False

        For Each pluginType In pluginTypes
            Dim plugin = DirectCast(Activator.CreateInstance(pluginType), IExtFFmpegFreeUIPlugin)
            Dim pluginId = If(plugin.Id, "").Trim()
            If pluginId = "" Then Throw New InvalidOperationException($"{pluginType.FullName} 的插件 ID 为空")
            If 插件实例表.ContainsKey(pluginId) Then Throw New InvalidOperationException($"插件 ID {pluginId} 已被占用")

            Dim host = 创建插件作用域(pluginId, plugin.DisplayName)
            Try
                plugin.Initialize(host)
            Catch
                TryCast(host, IDisposable)?.Dispose()
                Throw
            End Try
            插件实例表.Add(pluginId, plugin)
            插件宿主表.Add(pluginId, host)
        Next
        Return True
    End Function

    Private Function 获取可加载类型(程序集 As Assembly) As IEnumerable(Of Type)
        Try
            Return 程序集.GetTypes()
        Catch ex As ReflectionTypeLoadException
            Dim loaderMessages = ex.LoaderExceptions.
                Where(Function(item) item IsNot Nothing).
                Select(Function(item) item.Message)
            Throw New InvalidOperationException(
                $"读取插件类型失败：{String.Join("；", loaderMessages)}",
                ex)
        End Try
    End Function

    Friend Function 创建插件作用域(pluginId As String, displayName As String) As IExtFFmpegFreeUIHost
        Dim id = If(pluginId, "").Trim()
        If id = "" Then Throw New ArgumentException("插件 ID 不能为空", NameOf(pluginId))
        Return New 插件作用域宿主(id, If(displayName, "").Trim())
    End Function

    Friend Sub 注册界面锚点(anchorId As String,
                         anchorControl As Control,
                         surface As Form_v6_参数面板,
                         position As Ext插件界面锚点位置_v2)
        If String.IsNullOrWhiteSpace(anchorId) OrElse anchorControl Is Nothing OrElse surface Is Nothing Then Exit Sub
        Dim anchor As 已注册界面锚点
        Dim extensions As List(Of 已注册界面扩展)
        Dim choices As List(Of 已注册安全下拉项)

        SyncLock 同步锁
            anchor = 界面锚点列表.FirstOrDefault(
                Function(x) x.Surface Is surface AndAlso String.Equals(x.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase))
            If anchor IsNot Nothing Then Exit Sub

            anchor = New 已注册界面锚点 With {
                .AnchorId = anchorId.Trim(),
                .AnchorControl = anchorControl,
                .Surface = surface,
                .Position = position
            }
            界面锚点列表.Add(anchor)
            Dim legacyResourceId = 尝试获取传统锚点资源ID(anchor.AnchorId)
            If legacyResourceId <> "" Then
                For Each aliasAnchor In 界面锚点列表.Where(
                    Function(item) item.AnchorControl Is anchorControl AndAlso
                                   item.AnchorId.StartsWith(ExtFFmpegFreeUIParameterPanelIds.ControlAnchorPrefix, StringComparison.OrdinalIgnoreCase))
                    Dim descriptor As ExtPluginParameterControlDescriptor = Nothing
                    If 参数控件目录.TryGetValue(aliasAnchor.AnchorId, descriptor) AndAlso
                       Not String.Equals(descriptor.ResourceId, legacyResourceId, StringComparison.OrdinalIgnoreCase) Then
                        参数控件目录(aliasAnchor.AnchorId) = New ExtPluginParameterControlDescriptor(
                            descriptor.ControlId,
                            descriptor.PageId,
                            descriptor.ControlPath,
                            descriptor.ControlName,
                            descriptor.ControlTypeName,
                            descriptor.AnchorId,
                            legacyResourceId,
                            descriptor.ValuePropertyName)
                    End If
                Next
            End If
            extensions = 界面扩展列表.
                Where(Function(x) String.Equals(x.Extension.AnchorId, anchor.AnchorId, StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(x) x.Extension.Order).
                ThenBy(Function(x) x.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(x) x.Extension.Id, StringComparer.OrdinalIgnoreCase).
                ToList()
            choices = 安全下拉项列表.
                Where(Function(x) String.Equals(x.Extension.AnchorId, anchor.AnchorId, StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(x) x.Extension.Order).
                ThenBy(Function(x) x.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(x) x.Extension.Id, StringComparer.OrdinalIgnoreCase).
                ToList()
        End SyncLock

        AddHandler anchorControl.Disposed, Sub() 移除界面锚点(anchor)
        For Each extension In extensions
            应用界面扩展(anchor, extension)
        Next
        For Each choice In choices
            应用安全下拉项(anchor, choice)
        Next
    End Sub

    Friend Sub 注册参数面板页面(pageId As String,
                            displayName As String,
                            pageRoot As Control,
                            surface As Form_v6_参数面板)
        Dim id = If(pageId, "").Trim().ToLowerInvariant()
        If id = "" OrElse pageRoot Is Nothing OrElse surface Is Nothing Then Exit Sub
        Dim topAnchorId = ExtFFmpegFreeUIParameterPanelIds.PageTop(id)
        Dim bottomAnchorId = ExtFFmpegFreeUIParameterPanelIds.PageBottom(id)
        SyncLock 同步锁
            If Not 参数页面目录.ContainsKey(id) Then
                参数页面目录(id) = New ExtPluginParameterPageDescriptor(
                    id,
                    If(displayName, "").Trim(),
                    topAnchorId,
                    bottomAnchorId)
            End If
        End SyncLock
        注册界面锚点(topAnchorId, pageRoot, surface, Ext插件界面锚点位置_v2.容器顶部)
        注册界面锚点(bottomAnchorId, pageRoot, surface, Ext插件界面锚点位置_v2.容器底部)
    End Sub

    Friend Sub 注册参数面板控件(controlId As String,
                            pageId As String,
                            controlPath As String,
                            controlName As String,
                            controlTypeName As String,
                            valuePropertyName As String,
                            control As Control,
                            surface As Form_v6_参数面板)
        Dim id = If(controlId, "").Trim()
        If id = "" OrElse control Is Nothing OrElse surface Is Nothing Then Exit Sub
        Dim resourceId = ExtFFmpegFreeUIParameterPanelIds.ControlResource(id)
        Dim knownLegacyResourceId = 尝试获取参数控件传统资源ID(pageId, controlName)
        If knownLegacyResourceId <> "" Then resourceId = knownLegacyResourceId
        SyncLock 同步锁
            Dim legacyResourceId = 界面锚点列表.
                Where(Function(item) item.AnchorControl Is control).
                Select(Function(item) 尝试获取传统锚点资源ID(item.AnchorId)).
                FirstOrDefault(Function(item) item <> "")
            If legacyResourceId <> "" Then resourceId = legacyResourceId
            If Not 参数控件目录.ContainsKey(id) Then
                参数控件目录(id) = New ExtPluginParameterControlDescriptor(
                    id,
                    If(pageId, "").Trim().ToLowerInvariant(),
                    If(controlPath, "").Trim(),
                    If(controlName, "").Trim(),
                    If(controlTypeName, "").Trim(),
                    id,
                    resourceId,
                    If(valuePropertyName, "").Trim())
            End If
        End SyncLock
        注册界面锚点(id, control, surface, Ext插件界面锚点位置_v2.装饰目标控件)
    End Sub

    Friend Sub 还原参数面板插件状态(surface As Form_v6_参数面板, values As IDictionary(Of String, String))
        If surface Is Nothing Then Exit Sub
        Dim state = 获取参数面板状态(surface)
        Dim contexts As List(Of 插件界面上下文)
        Dim choiceContexts As List(Of 安全下拉项上下文)
        SyncLock 同步锁
            state.Values.Clear()
            If values IsNot Nothing Then
                For Each pair In values
                    If Not String.IsNullOrWhiteSpace(pair.Key) Then
                        state.Values(pair.Key.Trim()) = 规范化状态Json(pair.Value)
                    End If
                Next
            End If
            contexts = state.Contexts.ToList()
            choiceContexts = state.ChoiceContexts.ToList()
        End SyncLock

        For Each context In contexts
            context.通知状态已还原()
        Next
        For Each context In choiceContexts
            context.通知状态已还原()
        Next
    End Sub

    Friend Function 捕获参数面板插件状态(surface As Form_v6_参数面板) As Dictionary(Of String, String)
        Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        If surface Is Nothing Then Return result
        Dim state = 获取参数面板状态(surface)
        SyncLock 同步锁
            For Each pair In state.Values
                result(pair.Key) = 规范化状态Json(pair.Value)
            Next
        End SyncLock
        Return result
    End Function

    Friend Function 获取界面标识(surface As Form_v6_参数面板) As String
        If surface Is Nothing Then Return ""
        Return 获取参数面板状态(surface).SurfaceId
    End Function

    Friend Sub 执行同步阶段(stageId As String, context As Ext插件管线上下文_v2)
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        context.StageId = stageId
        For Each registration In 获取阶段处理器(stageId)
            Dim sdkContext = 转换到SDK上下文(context, registration.PluginId)
            Try
                Dim pending = registration.Handler.Callback.Invoke(sdkContext, CancellationToken.None)
                If Not pending.IsCompleted Then
                    Throw New InvalidOperationException($"阶段 {stageId} 是同步阶段，处理器不能执行异步等待")
                End If
                pending.GetAwaiter().GetResult()
            Catch ex As Exception
                Throw 包装处理器异常(registration, stageId, ex)
            End Try
            应用SDK上下文(sdkContext, context)
        Next
    End Sub

    Friend Async Function 执行异步阶段Async(stageId As String,
                                       context As Ext插件管线上下文_v2,
                                       cancellationToken As CancellationToken) As Task
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        context.StageId = stageId
        For Each registration In 获取阶段处理器(stageId)
            cancellationToken.ThrowIfCancellationRequested()
            Dim sdkContext = 转换到SDK上下文(context, registration.PluginId)
            Try
                Await registration.Handler.Callback.Invoke(sdkContext, cancellationToken).AsTask().ConfigureAwait(False)
            Catch ex As OperationCanceledException When cancellationToken.IsCancellationRequested
                Throw
            Catch ex As Exception
                Throw 包装处理器异常(registration, stageId, ex)
            End Try
            应用SDK上下文(sdkContext, context)
        Next
    End Function

    Friend Function 解析插件命令参数(source As Ext插件命令解析上下文_v2) As List(Of Ext插件命令参数_v2)
        If source Is Nothing Then Throw New ArgumentNullException(NameOf(source))
        Dim result As New List(Of Ext插件命令参数_v2)
        For Each registration In 获取命令参数提供器()
            Dim context = 创建插件命令上下文(source, registration.PluginId)
            Try
                registration.Provider.Callback.Invoke(context)
            Catch ex As Exception
                Throw New InvalidOperationException(
                    $"插件 {registration.PluginId} 的参数提供器 {registration.Provider.Id} 解析失败：{ex.Message}",
                    ex)
            End Try

            For Each argument In context.Arguments.
                Where(Function(item) item IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(item.Text)).
                OrderBy(Function(item) item.Order)
                result.Add(New Ext插件命令参数_v2 With {
                    .PluginId = registration.PluginId,
                    .ProviderId = registration.Provider.Id,
                    .Position = 转换命令参数位置(argument.Position),
                    .Text = argument.Text.Trim(),
                    .Order = argument.Order,
                    .Description = If(argument.Description, "").Trim()
                })
            Next
        Next
        Return result
    End Function

    Friend Function 解析插件命令步骤(source As Ext插件命令解析上下文_v2) As List(Of Ext插件命令步骤_v2)
        If source Is Nothing Then Throw New ArgumentNullException(NameOf(source))
        Dim result As New List(Of Ext插件命令步骤_v2)
        For Each registration In 获取命令步骤提供器()
            Dim context = 创建插件命令上下文(source, registration.PluginId)
            Try
                registration.Provider.Callback.Invoke(context)
            Catch ex As Exception
                Throw New InvalidOperationException(
                    $"插件 {registration.PluginId} 的命令步骤提供器 {registration.Provider.Id} 解析失败：{ex.Message}",
                    ex)
            End Try

            Dim duplicateIds = context.Steps.
                Where(Function(item) item IsNot Nothing).
                GroupBy(Function(item) If(item.Id, "").Trim(), StringComparer.OrdinalIgnoreCase).
                FirstOrDefault(Function(group) group.Key = "" OrElse group.Count() > 1)
            If duplicateIds IsNot Nothing Then
                Throw New InvalidOperationException(
                    $"插件 {registration.PluginId} 的命令步骤提供器 {registration.Provider.Id} 包含空或重复的步骤 ID")
            End If

            For Each stepItem In context.Steps.
                Where(Function(item) item IsNot Nothing AndAlso (Not source.IsPreview OrElse item.IncludeInPreview)).
                OrderBy(Function(item) item.Order)
                If String.IsNullOrWhiteSpace(stepItem.ProcessFileName) Then
                    Throw New InvalidOperationException(
                        $"插件 {registration.PluginId} 的命令步骤 {stepItem.Id} 没有指定 ProcessFileName")
                End If
                result.Add(New Ext插件命令步骤_v2 With {
                    .PluginId = registration.PluginId,
                    .ProviderId = registration.Provider.Id,
                    .StepId = stepItem.Id.Trim(),
                    .DisplayName = If(String.IsNullOrWhiteSpace(stepItem.DisplayName), stepItem.Id.Trim(), stepItem.DisplayName.Trim()),
                    .ProcessFileName = stepItem.ProcessFileName.Trim(),
                    .Arguments = If(stepItem.Arguments, "").Trim(),
                    .WorkingDirectory = If(stepItem.WorkingDirectory, "").Trim(),
                    .Placement = If(stepItem.Placement = ExtPluginCommandStepPlacement.BeforeNative,
                                    Ext插件命令步骤位置_v2.原生步骤之前,
                                    Ext插件命令步骤位置_v2.原生步骤之后),
                    .Order = stepItem.Order,
                    .ParseFFmpegProgress = stepItem.ParseFFmpegProgress
                })
            Next
        Next
        Return result
    End Function

    Private Function 创建插件命令上下文(source As Ext插件命令解析上下文_v2,
                                  pluginId As String) As ExtPluginCommandContext
        Dim result As New ExtPluginCommandContext With {
            .PluginId = pluginId,
            .PresetJson = If(source.PresetJson, ""),
            .PluginStateJson = 读取插件命令状态(source.PresetJson, pluginId),
            .InputPath = If(source.InputPath, ""),
            .OutputPath = If(source.OutputPath, ""),
            .TaskId = If(source.TaskId, ""),
            .PhaseName = If(source.PhaseName, ""),
            .IsPreview = source.IsPreview
        }
        For Each pair In source.Properties
            result.Properties(pair.Key) = pair.Value
        Next
        Return result
    End Function

    Private Function 读取插件命令状态(presetJson As String, pluginId As String) As String
        If String.IsNullOrWhiteSpace(presetJson) OrElse String.IsNullOrWhiteSpace(pluginId) Then Return "{}"
        Try
            Using document = JsonDocument.Parse(presetJson)
                Dim extensionData As JsonElement
                If Not document.RootElement.TryGetProperty("插件扩展数据", extensionData) OrElse
                   extensionData.ValueKind <> JsonValueKind.Object Then Return "{}"
                For Each propertyItem In extensionData.EnumerateObject()
                    If Not String.Equals(propertyItem.Name, pluginId, StringComparison.OrdinalIgnoreCase) Then Continue For
                    If propertyItem.Value.ValueKind = JsonValueKind.String Then
                        Return 规范化状态Json(propertyItem.Value.GetString())
                    End If
                    Return 规范化状态Json(propertyItem.Value.GetRawText())
                Next
            End Using
        Catch ex As JsonException
            Return "{}"
        End Try
        Return "{}"
    End Function

    Private Function 转换命令参数位置(position As ExtPluginCommandArgumentPosition) As Ext插件命令参数位置_v2
        Select Case position
            Case ExtPluginCommandArgumentPosition.Global : Return Ext插件命令参数位置_v2.全局
            Case ExtPluginCommandArgumentPosition.BeforeInput : Return Ext插件命令参数位置_v2.输入之前
            Case ExtPluginCommandArgumentPosition.AfterInput : Return Ext插件命令参数位置_v2.输入之后
            Case ExtPluginCommandArgumentPosition.BeforeOutput : Return Ext插件命令参数位置_v2.输出之前
            Case ExtPluginCommandArgumentPosition.AfterOutput : Return Ext插件命令参数位置_v2.输出之后
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(position), position, "未知的插件命令参数位置")
        End Select
    End Function

    Private Function 转换到SDK上下文(source As Ext插件管线上下文_v2, pluginId As String) As ExtPluginPipelineContext
        Dim result As New ExtPluginPipelineContext(
            Sub(progress) source.ReportProgress(progress.Message, progress.Fraction),
            Sub(taskResult) source.ReportResult(
                pluginId,
                taskResult.Key,
                taskResult.Value,
                taskResult.DisplayName,
                taskResult.Unit)) With {
            .StageId = source.StageId,
            .PresetJson = source.PresetJson,
            .InputPath = source.InputPath,
            .OutputPath = source.OutputPath,
            .CommandLine = source.CommandLine,
            .ProcessFileName = source.ProcessFileName,
            .TaskId = source.TaskId,
            .SurfaceId = source.SurfaceId,
            .PhaseName = source.PhaseName,
            .IsPreview = source.IsPreview,
            .ExitCode = source.ExitCode,
            .TaskStatus = 转换任务状态(source.TaskStatus)
        }
        For Each pair In source.Properties
            result.Properties(pair.Key) = pair.Value
        Next
        Return result
    End Function

    Private Sub 应用SDK上下文(source As ExtPluginPipelineContext, target As Ext插件管线上下文_v2)
        target.PresetJson = source.PresetJson
        target.InputPath = source.InputPath
        target.OutputPath = source.OutputPath
        target.CommandLine = source.CommandLine
        target.ProcessFileName = source.ProcessFileName
        target.TaskId = source.TaskId
        target.SurfaceId = source.SurfaceId
        target.PhaseName = source.PhaseName
        target.IsPreview = source.IsPreview
        target.ExitCode = source.ExitCode
        target.Properties.Clear()
        For Each pair In source.Properties
            target.Properties(pair.Key) = pair.Value
        Next
    End Sub

    Private Function 转换任务状态(value As String) As ExtPluginTaskStatus
        Select Case If(value, "").Trim().ToLowerInvariant()
            Case "pending" : Return ExtPluginTaskStatus.Pending
            Case "running" : Return ExtPluginTaskStatus.Running
            Case "paused" : Return ExtPluginTaskStatus.Paused
            Case "succeeded" : Return ExtPluginTaskStatus.Succeeded
            Case "failed" : Return ExtPluginTaskStatus.Failed
            Case "canceled" : Return ExtPluginTaskStatus.Canceled
            Case Else : Return ExtPluginTaskStatus.Unknown
        End Select
    End Function

    Private Function 注册界面扩展(pluginId As String, extension As ExtPluginUiExtension) As IDisposable
        If extension Is Nothing Then Throw New ArgumentNullException(NameOf(extension))
        If String.IsNullOrWhiteSpace(extension.Id) Then Throw New ArgumentException("界面扩展 ID 不能为空")
        If String.IsNullOrWhiteSpace(extension.AnchorId) Then Throw New ArgumentException("界面锚点 ID 不能为空")
        If extension.CreateControl Is Nothing Then Throw New ArgumentException("界面控件工厂不能为空")

        Dim resourceRegistration As IDisposable = Nothing
        If extension.Mode = ExtPluginUiExtensionMode.ReplaceAnchor Then
            If String.IsNullOrWhiteSpace(extension.ResourceId) OrElse extension.ResourceAccess <> ExtPluginResourceAccess.Exclusive Then
                Throw New InvalidOperationException("替换原生 UI 必须声明 ResourceId，并使用 Exclusive 资源访问")
            End If
            Dim expectedResourceId = 替换锚点资源ID(extension.AnchorId)
            If Not String.Equals(extension.ResourceId, expectedResourceId, StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException(
                    $"替换锚点 {extension.AnchorId} 必须独占资源 {expectedResourceId}，不能声明 {extension.ResourceId}")
            End If
            resourceRegistration = 注册资源声明(pluginId,
                New ExtPluginResourceClaim("ui:" & extension.Id, extension.ResourceId, ExtPluginResourceAccess.Exclusive) With {
                    .Purpose = $"替换 UI 锚点 {extension.AnchorId}"
                })
        ElseIf Not String.IsNullOrWhiteSpace(extension.ResourceId) Then
            resourceRegistration = 注册资源声明(pluginId,
                New ExtPluginResourceClaim("ui:" & extension.Id, extension.ResourceId, extension.ResourceAccess) With {
                    .Purpose = $"扩展 UI 锚点 {extension.AnchorId}"
                })
        End If

        Dim registration As 已注册界面扩展 = Nothing
        Try
            registration = New 已注册界面扩展 With {.PluginId = pluginId, .Extension = extension}
            Dim anchors As List(Of 已注册界面锚点)
            SyncLock 同步锁
                If 界面扩展列表.Any(Function(x) String.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                                                 String.Equals(x.Extension.Id, extension.Id, StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException($"插件 {pluginId} 已注册界面扩展 {extension.Id}")
                End If
                界面扩展列表.Add(registration)
                anchors = 界面锚点列表.
                    Where(Function(x) String.Equals(x.AnchorId, extension.AnchorId, StringComparison.OrdinalIgnoreCase)).
                    ToList()
            End SyncLock

            For Each anchor In anchors
                在控件线程执行(anchor.AnchorControl, Sub() 应用界面扩展(anchor, registration))
            Next
            Return New 注销句柄(
                Sub()
                    注销界面扩展(registration)
                    resourceRegistration?.Dispose()
                End Sub)
        Catch
            If registration IsNot Nothing Then 注销界面扩展(registration)
            resourceRegistration?.Dispose()
            Throw
        End Try
    End Function

    Private Function 替换锚点资源ID(anchorId As String) As String
        SyncLock 同步锁
            Dim descriptor = 参数控件目录.Values.FirstOrDefault(
                Function(item) String.Equals(item.AnchorId, anchorId, StringComparison.OrdinalIgnoreCase))
            If descriptor IsNot Nothing Then Return descriptor.ResourceId
        End SyncLock

        Dim legacyResourceId = 尝试获取传统锚点资源ID(anchorId)
        If legacyResourceId <> "" Then Return legacyResourceId
        Throw New InvalidOperationException(
            $"锚点 {anchorId} 是插入位置而不是可替换原生控件；请使用默认插入模式")
    End Function

    Private Function 尝试获取传统锚点资源ID(anchorId As String) As String
        Select Case anchorId
            Case ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode
                Return ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeControl
            Case ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityParameterName,
                 ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityValue
                Return ExtFFmpegFreeUIPluginResources.ParametersVideoQualityFields
            Case Else
                Return ""
        End Select
    End Function

    Private Function 尝试获取参数控件传统资源ID(pageId As String, controlName As String) As String
        If Not String.Equals(If(pageId, "").Trim(), "video-quality", StringComparison.OrdinalIgnoreCase) Then Return ""
        Select Case If(controlName, "").Trim()
            Case "MCB_全局质量控制方式"
                Return ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeControl
            Case "MCB_质量参数名称", "MTB_质量值"
                Return ExtFFmpegFreeUIPluginResources.ParametersVideoQualityFields
            Case Else
                Return ""
        End Select
    End Function

    Friend Sub 执行行为点(behaviorId As String,
                      context As Ext插件行为上下文_v2,
                      nativeAction As Action(Of Ext插件行为上下文_v2))
        If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
        context.BehaviorId = behaviorId
        Dim handlers = 获取行为处理器(behaviorId)

        For Each registration In handlers.Where(Function(x) x.Handler.Phase = ExtPluginBehaviorPhase.BeforeNative)
            执行行为处理器(registration, context)
        Next

        Dim replacement = handlers.SingleOrDefault(Function(x) x.Handler.Phase = ExtPluginBehaviorPhase.ReplaceNative)
        If replacement Is Nothing Then
            nativeAction?.Invoke(context)
        Else
            执行行为处理器(replacement, context)
        End If

        For Each registration In handlers.Where(Function(x) x.Handler.Phase = ExtPluginBehaviorPhase.AfterNative)
            执行行为处理器(registration, context)
        Next
    End Sub

    Private Sub 执行行为处理器(registration As 已注册行为处理器, context As Ext插件行为上下文_v2)
        Dim sdkContext As New ExtPluginBehaviorContext With {
            .BehaviorId = context.BehaviorId,
            .SurfaceId = context.SurfaceId
        }
        For Each pair In context.Properties
            sdkContext.Properties(pair.Key) = pair.Value
        Next

        Try
            registration.Handler.Callback.Invoke(sdkContext)
        Catch ex As Exception
            Throw New InvalidOperationException(
                $"插件 {registration.PluginId} 的行为处理器 {registration.Handler.Id} 在 {context.BehaviorId} 失败：{ex.Message}",
                ex)
        End Try

        context.SurfaceId = sdkContext.SurfaceId
        context.Properties.Clear()
        For Each pair In sdkContext.Properties
            context.Properties(pair.Key) = If(pair.Value, "")
        Next
    End Sub

    Private Function 注册安全下拉项(pluginId As String, extension As ExtPluginUiChoiceExtension) As IDisposable
        If extension Is Nothing Then Throw New ArgumentNullException(NameOf(extension))
        If String.IsNullOrWhiteSpace(extension.Id) Then Throw New ArgumentException("安全下拉项扩展 ID 不能为空")
        If String.IsNullOrWhiteSpace(extension.AnchorId) Then Throw New ArgumentException("下拉项锚点 ID 不能为空")
        If Not ExtFFmpegFreeUIUiChoiceAnchors.All.Contains(extension.AnchorId, StringComparer.OrdinalIgnoreCase) Then
            Throw New ArgumentException($"锚点 {extension.AnchorId} 不支持宿主管理下拉项")
        End If
        If String.IsNullOrWhiteSpace(extension.ChoiceId) Then Throw New ArgumentException("下拉项 ChoiceId 不能为空")
        If String.IsNullOrWhiteSpace(extension.DisplayText) Then Throw New ArgumentException("下拉项显示文本不能为空")
        If String.IsNullOrWhiteSpace(extension.NativeFallbackChoiceId) Then Throw New ArgumentException("原生回退 ChoiceId 不能为空")
        If ExtFFmpegFreeUIUiChoices.All.Contains(extension.ChoiceId, StringComparer.OrdinalIgnoreCase) Then
            Throw New ArgumentException($"下拉项 ChoiceId {extension.ChoiceId} 与原生选项冲突")
        End If
        If Not ExtFFmpegFreeUIUiChoices.All.Contains(extension.NativeFallbackChoiceId, StringComparer.OrdinalIgnoreCase) Then
            Throw New ArgumentException($"原生回退 ChoiceId {extension.NativeFallbackChoiceId} 不存在")
        End If

        Dim resourceRegistrations As New List(Of IDisposable)
        Dim registration As 已注册安全下拉项 = Nothing
        Try
            resourceRegistrations.Add(注册资源声明(
                pluginId,
                New ExtPluginResourceClaim(
                    "choice-items:" & extension.Id,
                    下拉项锚点资源ID(extension.AnchorId),
                    ExtPluginResourceAccess.OrderedTransform) With {
                    .Purpose = $"向 {extension.AnchorId} 添加宿主管理选项"
                }))
            If extension.ValueOverrides.Count > 0 OrElse extension.DisabledAnchors.Count > 0 Then
                resourceRegistrations.Add(注册资源声明(
                    pluginId,
                    New ExtPluginResourceClaim(
                        "choice-fields:" & extension.Id,
                        ExtFFmpegFreeUIPluginResources.ParametersVideoQualityFields,
                        ExtPluginResourceAccess.OrderedTransform) With {
                        .Purpose = $"应用安全选项 {extension.ChoiceId} 的字段状态"
                    }))
            End If

            registration = New 已注册安全下拉项 With {.PluginId = pluginId, .Extension = extension}
            Dim anchors As List(Of 已注册界面锚点)
            SyncLock 同步锁
                If 安全下拉项列表.Any(
                    Function(x) String.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(x.Extension.Id, extension.Id, StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException($"插件 {pluginId} 已注册安全下拉项扩展 {extension.Id}")
                End If
                If 安全下拉项列表.Any(
                    Function(x) String.Equals(x.Extension.AnchorId, extension.AnchorId, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(x.Extension.ChoiceId, extension.ChoiceId, StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException($"锚点 {extension.AnchorId} 的 ChoiceId {extension.ChoiceId} 已被占用")
                End If
                安全下拉项列表.Add(registration)
                anchors = 界面锚点列表.
                    Where(Function(x) String.Equals(x.AnchorId, extension.AnchorId, StringComparison.OrdinalIgnoreCase)).
                    ToList()
            End SyncLock

            For Each anchor In anchors
                在控件线程执行(anchor.AnchorControl, Sub() 应用安全下拉项(anchor, registration))
            Next
            Return New 注销句柄(
                Sub()
                    注销安全下拉项(registration)
                    For index = resourceRegistrations.Count - 1 To 0 Step -1
                        resourceRegistrations(index).Dispose()
                    Next
                End Sub)
        Catch
            If registration IsNot Nothing Then 注销安全下拉项(registration)
            For index = resourceRegistrations.Count - 1 To 0 Step -1
                resourceRegistrations(index).Dispose()
            Next
            Throw
        End Try
    End Function

    Private Function 下拉项锚点资源ID(anchorId As String) As String
        Select Case anchorId
            Case ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode
                Return ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeItems
            Case Else
                Throw New ArgumentException($"下拉项锚点 {anchorId} 没有对应的冲突资源")
        End Select
    End Function

    Private Function 注册命令参数提供器(pluginId As String,
                                  provider As ExtPluginCommandParameterProvider) As IDisposable
        If provider Is Nothing Then Throw New ArgumentNullException(NameOf(provider))
        If String.IsNullOrWhiteSpace(provider.Id) Then Throw New ArgumentException("命令参数提供器 ID 不能为空")
        If provider.Callback Is Nothing Then Throw New ArgumentException("命令参数提供器回调不能为空")
        Dim resourceRegistration = 注册资源声明(
            pluginId,
            New ExtPluginResourceClaim(
                "command-parameters:" & provider.Id,
                ExtFFmpegFreeUIPluginResources.CommandArguments,
                ExtPluginResourceAccess.OrderedTransform) With {
                .Purpose = "向 FFmpeg 命令的稳定位置贡献参数"
            })

        Try
            Dim registration As New 已注册命令参数提供器 With {.PluginId = pluginId, .Provider = provider}
            SyncLock 同步锁
                If 命令参数提供器列表.Any(
                    Function(item) String.Equals(item.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                                   String.Equals(item.Provider.Id, provider.Id, StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException($"插件 {pluginId} 已注册命令参数提供器 {provider.Id}")
                End If
                命令参数提供器列表.Add(registration)
            End SyncLock
            Return New 注销句柄(
                Sub()
                    SyncLock 同步锁
                        命令参数提供器列表.Remove(registration)
                    End SyncLock
                    resourceRegistration.Dispose()
                End Sub)
        Catch
            resourceRegistration.Dispose()
            Throw
        End Try
    End Function

    Private Function 注册命令步骤提供器(pluginId As String,
                                  provider As ExtPluginCommandStepProvider) As IDisposable
        If provider Is Nothing Then Throw New ArgumentNullException(NameOf(provider))
        If String.IsNullOrWhiteSpace(provider.Id) Then Throw New ArgumentException("命令步骤提供器 ID 不能为空")
        If provider.Callback Is Nothing Then Throw New ArgumentException("命令步骤提供器回调不能为空")
        Dim resourceRegistration = 注册资源声明(
            pluginId,
            New ExtPluginResourceClaim(
                "command-steps:" & provider.Id,
                ExtFFmpegFreeUIPluginResources.CommandPlan,
                ExtPluginResourceAccess.OrderedTransform) With {
                .Purpose = "向任务命令计划贡献可预览的外部进程步骤"
            })

        Try
            Dim registration As New 已注册命令步骤提供器 With {.PluginId = pluginId, .Provider = provider}
            SyncLock 同步锁
                If 命令步骤提供器列表.Any(
                    Function(item) String.Equals(item.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                                   String.Equals(item.Provider.Id, provider.Id, StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException($"插件 {pluginId} 已注册命令步骤提供器 {provider.Id}")
                End If
                命令步骤提供器列表.Add(registration)
            End SyncLock
            Return New 注销句柄(
                Sub()
                    SyncLock 同步锁
                        命令步骤提供器列表.Remove(registration)
                    End SyncLock
                    resourceRegistration.Dispose()
                End Sub)
        Catch
            resourceRegistration.Dispose()
            Throw
        End Try
    End Function

    Private Function 获取命令参数提供器() As List(Of 已注册命令参数提供器)
        SyncLock 同步锁
            Return 命令参数提供器列表.
                OrderBy(Function(item) item.Provider.Order).
                ThenBy(Function(item) item.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(item) item.Provider.Id, StringComparer.OrdinalIgnoreCase).
                ToList()
        End SyncLock
    End Function

    Private Function 获取命令步骤提供器() As List(Of 已注册命令步骤提供器)
        SyncLock 同步锁
            Return 命令步骤提供器列表.
                OrderBy(Function(item) item.Provider.Order).
                ThenBy(Function(item) item.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(item) item.Provider.Id, StringComparer.OrdinalIgnoreCase).
                ToList()
        End SyncLock
    End Function

    Private Function 获取可用界面锚点() As IReadOnlyCollection(Of String)
        SyncLock 同步锁
            Return ExtFFmpegFreeUIUiAnchors.All.
                Concat(界面锚点列表.Select(Function(item) item.AnchorId)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                OrderBy(Function(item) item, StringComparer.OrdinalIgnoreCase).
                ToList().
                AsReadOnly()
        End SyncLock
    End Function

    Private Function 获取参数页面目录() As IReadOnlyCollection(Of ExtPluginParameterPageDescriptor)
        SyncLock 同步锁
            Return 参数页面目录.Values.
                OrderBy(Function(item) item.PageId, StringComparer.OrdinalIgnoreCase).
                ToList().
                AsReadOnly()
        End SyncLock
    End Function

    Private Function 获取参数控件目录() As IReadOnlyCollection(Of ExtPluginParameterControlDescriptor)
        SyncLock 同步锁
            Return 参数控件目录.Values.
                OrderBy(Function(item) item.ControlId, StringComparer.OrdinalIgnoreCase).
                ToList().
                AsReadOnly()
        End SyncLock
    End Function

    Private Function 获取可用资源() As IReadOnlyCollection(Of String)
        SyncLock 同步锁
            Return ExtFFmpegFreeUIPluginResources.All.
                Concat(参数控件目录.Values.Select(Function(item) item.ResourceId)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                OrderBy(Function(item) item, StringComparer.OrdinalIgnoreCase).
                ToList().
                AsReadOnly()
        End SyncLock
    End Function

    Private Function 注册处理器(pluginId As String, handler As ExtPluginPipelineHandler) As IDisposable
        If handler Is Nothing Then Throw New ArgumentNullException(NameOf(handler))
        If String.IsNullOrWhiteSpace(handler.Id) Then Throw New ArgumentException("处理器 ID 不能为空")
        If String.IsNullOrWhiteSpace(handler.StageId) Then Throw New ArgumentException("处理阶段 ID 不能为空")
        If handler.Callback Is Nothing Then Throw New ArgumentException("处理器回调不能为空")
        If Not ExtFFmpegFreeUIPipelineStages.All.Contains(handler.StageId, StringComparer.OrdinalIgnoreCase) Then
            Throw New ArgumentException($"FFmpegFreeUI 不支持处理阶段 {handler.StageId}")
        End If

        Dim resourceRegistration As IDisposable = Nothing
        Dim handlerResourceId = If(String.IsNullOrWhiteSpace(handler.ResourceId),
                                   推断处理阶段资源ID(handler.StageId),
                                   handler.ResourceId.Trim())
        If handlerResourceId <> "" Then
            resourceRegistration = 注册资源声明(pluginId,
                New ExtPluginResourceClaim("pipeline:" & handler.Id, handlerResourceId, handler.ResourceAccess) With {
                    .Purpose = $"处理阶段 {handler.StageId}"
                })
        End If

        Try
            Dim registration As New 已注册处理器 With {.PluginId = pluginId, .Handler = handler}
            SyncLock 同步锁
                If 处理器列表.Any(Function(x) String.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                                               String.Equals(x.Handler.Id, handler.Id, StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException($"插件 {pluginId} 已注册处理器 {handler.Id}")
                End If
                处理器列表.Add(registration)
            End SyncLock
            Return New 注销句柄(
                Sub()
                    SyncLock 同步锁
                        处理器列表.Remove(registration)
                    End SyncLock
                    resourceRegistration?.Dispose()
                End Sub)
        Catch
            resourceRegistration?.Dispose()
            Throw
        End Try
    End Function

    Private Function 推断处理阶段资源ID(stageId As String) As String
        Select Case stageId
            Case ExtFFmpegFreeUIPipelineStages.PresetBeforeCapture,
                 ExtFFmpegFreeUIPipelineStages.PresetAfterCapture,
                 ExtFFmpegFreeUIPipelineStages.PresetBeforeApply,
                 ExtFFmpegFreeUIPipelineStages.PresetAfterApply,
                 ExtFFmpegFreeUIPipelineStages.QueueBeforeAdd,
                 ExtFFmpegFreeUIPipelineStages.TaskBeforePrepare,
                 ExtFFmpegFreeUIPipelineStages.TaskAfterPrepare
                Return ExtFFmpegFreeUIPluginResources.PresetDocument
            Case ExtFFmpegFreeUIPipelineStages.CommandBeforeBuild,
                 ExtFFmpegFreeUIPipelineStages.CommandAfterBuild
                Return ExtFFmpegFreeUIPluginResources.CommandLine
            Case ExtFFmpegFreeUIPipelineStages.TaskAfterComplete,
                 ExtFFmpegFreeUIPipelineStages.TaskAfterFailed,
                 ExtFFmpegFreeUIPipelineStages.TaskAfterFinish
                Return ExtFFmpegFreeUIPluginResources.TaskAfterProcessing
            Case Else
                Return ""
        End Select
    End Function

    Private Function 注册行为处理器(pluginId As String, handler As ExtPluginBehaviorHandler) As IDisposable
        If handler Is Nothing Then Throw New ArgumentNullException(NameOf(handler))
        If String.IsNullOrWhiteSpace(handler.Id) Then Throw New ArgumentException("行为处理器 ID 不能为空")
        If String.IsNullOrWhiteSpace(handler.BehaviorId) Then Throw New ArgumentException("行为点 ID 不能为空")
        If handler.Callback Is Nothing Then Throw New ArgumentException("行为处理器回调不能为空")
        If Not ExtFFmpegFreeUIBehaviors.All.Contains(handler.BehaviorId, StringComparer.OrdinalIgnoreCase) Then
            Throw New ArgumentException($"宿主未公开行为点 {handler.BehaviorId}")
        End If

        Dim access = If(handler.Phase = ExtPluginBehaviorPhase.ReplaceNative,
                        ExtPluginResourceAccess.Exclusive,
                        ExtPluginResourceAccess.OrderedTransform)
        Dim resourceRegistration = 注册资源声明(
            pluginId,
            New ExtPluginResourceClaim(
                "behavior:" & handler.Id,
                行为点资源ID(handler.BehaviorId),
                access) With {
                .Purpose = If(handler.Phase = ExtPluginBehaviorPhase.ReplaceNative,
                              $"替换原生行为 {handler.BehaviorId}",
                              $"在原生行为 {handler.BehaviorId} 的 {handler.Phase} 阶段变换上下文")
            })

        Try
            Dim registration As New 已注册行为处理器 With {.PluginId = pluginId, .Handler = handler}
            SyncLock 同步锁
                If 行为处理器列表.Any(
                    Function(x) String.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(x.Handler.Id, handler.Id, StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException($"插件 {pluginId} 已注册行为处理器 {handler.Id}")
                End If
                If handler.Phase = ExtPluginBehaviorPhase.ReplaceNative AndAlso
                   行为处理器列表.Any(
                       Function(x) String.Equals(x.Handler.BehaviorId, handler.BehaviorId, StringComparison.OrdinalIgnoreCase) AndAlso
                                   x.Handler.Phase = ExtPluginBehaviorPhase.ReplaceNative) Then
                    Throw New InvalidOperationException($"行为点 {handler.BehaviorId} 已有替换处理器")
                End If
                行为处理器列表.Add(registration)
            End SyncLock
            Return New 注销句柄(
                Sub()
                    SyncLock 同步锁
                        行为处理器列表.Remove(registration)
                    End SyncLock
                    resourceRegistration?.Dispose()
                End Sub)
        Catch
            resourceRegistration?.Dispose()
            Throw
        End Try
    End Function

    Private Function 获取行为处理器(behaviorId As String) As List(Of 已注册行为处理器)
        SyncLock 同步锁
            Return 行为处理器列表.
                Where(Function(x) String.Equals(x.Handler.BehaviorId, behaviorId, StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(x) x.Handler.Phase).
                ThenBy(Function(x) x.Handler.Order).
                ThenBy(Function(x) x.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(x) x.Handler.Id, StringComparer.OrdinalIgnoreCase).
                ToList()
        End SyncLock
    End Function

    Private Function 行为点资源ID(behaviorId As String) As String
        Select Case behaviorId
            Case ExtFFmpegFreeUIBehaviors.ParametersVideoQualityModeChanged
                Return ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeBehavior
            Case Else
                Throw New ArgumentException($"行为点 {behaviorId} 没有对应的冲突资源")
        End Select
    End Function

    Private Function 注册资源声明(pluginId As String, claim As ExtPluginResourceClaim) As IDisposable
        If claim Is Nothing Then Throw New ArgumentNullException(NameOf(claim))
        If String.IsNullOrWhiteSpace(claim.Id) Then Throw New ArgumentException("资源声明 ID 不能为空")
        If String.IsNullOrWhiteSpace(claim.ResourceId) Then Throw New ArgumentException("资源 ID 不能为空")
        If Not 是已公开资源(claim.ResourceId) Then
            Throw New ArgumentException($"宿主未公开资源 {claim.ResourceId}")
        End If

        Dim registration As New 已注册资源声明 With {.PluginId = pluginId, .Claim = claim}
        SyncLock 同步锁
            If 资源声明列表.Any(
                Function(x) String.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                            String.Equals(x.Claim.Id, claim.Id, StringComparison.OrdinalIgnoreCase)) Then
                Throw New InvalidOperationException($"插件 {pluginId} 已注册资源声明 {claim.Id}")
            End If

            Dim conflicting = 资源声明列表.FirstOrDefault(
                Function(x) 资源标识冲突(x.Claim.ResourceId, claim.ResourceId) AndAlso
                            Not String.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) AndAlso
                            资源访问冲突(x.Claim.Access, claim.Access))
            If conflicting IsNot Nothing Then
                Throw New InvalidOperationException(
                    $"资源 {claim.ResourceId} 冲突：插件 {pluginId}/{claim.Id} 请求 {claim.Access}，" &
                    $"但插件 {conflicting.PluginId}/{conflicting.Claim.Id} 已声明 {conflicting.Claim.Access}")
            End If
            资源声明列表.Add(registration)
        End SyncLock

        Return New 注销句柄(
            Sub()
                SyncLock 同步锁
                    资源声明列表.Remove(registration)
                End SyncLock
            End Sub)
    End Function

    Private Function 资源访问冲突(existingAccess As ExtPluginResourceAccess, requestedAccess As ExtPluginResourceAccess) As Boolean
        If existingAccess = ExtPluginResourceAccess.Observe OrElse requestedAccess = ExtPluginResourceAccess.Observe Then Return False
        Return existingAccess = ExtPluginResourceAccess.Exclusive OrElse requestedAccess = ExtPluginResourceAccess.Exclusive
    End Function

    Private Function 资源标识冲突(existingResourceId As String, requestedResourceId As String) As Boolean
        If String.Equals(existingResourceId, requestedResourceId, StringComparison.OrdinalIgnoreCase) Then Return True

        ' 原始整段命令行是声明式参数的父资源；独占接管整段命令时应排除参数提供器。
        If String.Equals(existingResourceId, ExtFFmpegFreeUIPluginResources.CommandLine, StringComparison.OrdinalIgnoreCase) AndAlso
           String.Equals(requestedResourceId, ExtFFmpegFreeUIPluginResources.CommandArguments, StringComparison.OrdinalIgnoreCase) Then Return True
        If String.Equals(requestedResourceId, ExtFFmpegFreeUIPluginResources.CommandLine, StringComparison.OrdinalIgnoreCase) AndAlso
           String.Equals(existingResourceId, ExtFFmpegFreeUIPluginResources.CommandArguments, StringComparison.OrdinalIgnoreCase) Then Return True

        ' 整个质量模式控件是选项集合和模式联动行为的父资源；接管父资源时必须排除两类子扩展。
        Dim qualityModeChildren = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeItems,
            ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeBehavior
        }
        If String.Equals(existingResourceId, ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeControl, StringComparison.OrdinalIgnoreCase) Then
            Return qualityModeChildren.Contains(requestedResourceId)
        End If
        If String.Equals(requestedResourceId, ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeControl, StringComparison.OrdinalIgnoreCase) Then
            Return qualityModeChildren.Contains(existingResourceId)
        End If
        Return False
    End Function

    Private Function 是已公开资源(resourceId As String) As Boolean
        If ExtFFmpegFreeUIPluginResources.All.Contains(resourceId, StringComparer.OrdinalIgnoreCase) Then Return True
        SyncLock 同步锁
            Return 参数控件目录.Values.Any(
                Function(item) String.Equals(item.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase))
        End SyncLock
    End Function

    Private Function 获取阶段处理器(stageId As String) As List(Of 已注册处理器)
        SyncLock 同步锁
            Return 处理器列表.
                Where(Function(x) String.Equals(x.Handler.StageId, stageId, StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(x) x.Handler.Order).
                ThenBy(Function(x) x.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(x) x.Handler.Id, StringComparer.OrdinalIgnoreCase).
                ToList()
        End SyncLock
    End Function

    Private Sub 应用界面扩展(anchor As 已注册界面锚点, registration As 已注册界面扩展)
        If anchor.AnchorControl.IsDisposed Then Exit Sub
        Dim key = registration.PluginId & ":" & registration.Extension.Id
        SyncLock 同步锁
            If anchor.Applied.ContainsKey(key) Then Exit Sub
        End SyncLock

        Dim container As Control = Nothing
        If anchor.Position <> Ext插件界面锚点位置_v2.装饰目标控件 Then
            container = 获取或创建插入槽(anchor)
        End If
        Dim context As New 插件界面上下文(registration.PluginId,
                                     registration.Extension.Id,
                                     anchor,
                                     container)
        Dim control = registration.Extension.CreateControl.Invoke(context)
        If registration.Extension.Mode = ExtPluginUiExtensionMode.ReplaceAnchor AndAlso control Is Nothing Then
            Throw New InvalidOperationException($"替换型界面扩展 {registration.Extension.Id} 必须返回控件")
        End If
        If registration.Extension.Mode = ExtPluginUiExtensionMode.Default AndAlso
           anchor.Position = Ext插件界面锚点位置_v2.装饰目标控件 AndAlso control IsNot Nothing Then
            control.Dispose()
            Throw New InvalidOperationException($"装饰型锚点 {anchor.AnchorId} 的控件工厂必须返回 null/Nothing")
        End If

        Dim applied As New 已应用界面扩展 With {
            .Registration = registration,
            .Context = context,
            .Control = control
        }
        SyncLock 同步锁
            anchor.Applied(key) = applied
            获取参数面板状态(anchor.Surface).Contexts.Add(context)
        End SyncLock

        If control IsNot Nothing Then
            If registration.Extension.Mode = ExtPluginUiExtensionMode.ReplaceAnchor Then
                替换原生锚点(anchor, applied)
            Else
                If control.Parent IsNot Nothing AndAlso control.Parent IsNot container Then control.Parent.Controls.Remove(control)
                control.Margin = New Padding(0)
                control.Dock = DockStyle.Top
                container.Controls.Add(control)
                AddHandler control.VisibleChanged, Sub() 重排插入槽(anchor)
                重排插入槽(anchor)
            End If
        End If
    End Sub

    Private Sub 替换原生锚点(anchor As 已注册界面锚点, applied As 已应用界面扩展)
        Dim replacement = applied.Control
        Dim parent = anchor.AnchorControl.Parent
        If replacement Is Nothing OrElse parent Is Nothing Then
            Throw New InvalidOperationException($"界面锚点 {anchor.AnchorId} 尚未加入父容器，无法替换")
        End If

        Dim targetIndex = parent.Controls.GetChildIndex(anchor.AnchorControl)
        replacement.Name = If(replacement.Name, "")
        replacement.Bounds = anchor.AnchorControl.Bounds
        replacement.Dock = anchor.AnchorControl.Dock
        replacement.Anchor = anchor.AnchorControl.Anchor
        replacement.Margin = anchor.AnchorControl.Margin
        parent.Controls.Add(replacement)
        parent.Controls.SetChildIndex(replacement, targetIndex)
        anchor.AnchorControl.Visible = False
        anchor.AnchorControl.Enabled = False
        applied.ReplacedAnchor = True
    End Sub

    Private Sub 应用安全下拉项(anchor As 已注册界面锚点, registration As 已注册安全下拉项)
        If anchor.AnchorControl.IsDisposed Then Exit Sub
        If Not TypeOf anchor.AnchorControl Is ModernComboBox Then
            Throw New InvalidOperationException($"锚点 {anchor.AnchorId} 不是宿主支持的下拉框")
        End If

        Dim key = registration.PluginId & ":" & registration.Extension.Id
        SyncLock 同步锁
            If anchor.AppliedChoices.ContainsKey(key) Then Exit Sub
        End SyncLock

        Dim context As New 安全下拉项上下文(registration, anchor)
        Dim applied As New 已应用安全下拉项 With {
            .Registration = registration,
            .Context = context
        }
        context.Applied = applied
        SyncLock 同步锁
            anchor.AppliedChoices(key) = applied
            获取参数面板状态(anchor.Surface).ChoiceContexts.Add(context)
        End SyncLock

        If Not anchor.ChoiceEventAttached Then
            AddHandler DirectCast(anchor.AnchorControl, ModernComboBox).SelectedIndexChanged,
                Sub() 处理安全下拉项选择变更(anchor)
            anchor.ChoiceEventAttached = True
        End If
        重建安全下拉项(anchor)
    End Sub

    Private Sub 重建安全下拉项(anchor As 已注册界面锚点)
        If anchor.AnchorControl.IsDisposed Then Exit Sub
        Dim qualityPage = anchor.Surface.私有界面_质量
        Dim ordered As List(Of 已应用安全下拉项)
        SyncLock 同步锁
            ordered = anchor.AppliedChoices.Values.
                OrderBy(Function(x) x.Registration.Extension.Order).
                ThenBy(Function(x) x.Registration.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(x) x.Registration.Extension.Id, StringComparer.OrdinalIgnoreCase).
                ToList()
        End SyncLock

        qualityPage.设置插件质量选项(
            ordered.Select(
                Function(x) New String() {
                    x.Registration.Extension.ChoiceId,
                    x.Registration.Extension.DisplayText,
                    x.Registration.Extension.NativeFallbackChoiceId
                }))
        处理安全下拉项选择变更(anchor)
    End Sub

    Private Sub 处理安全下拉项选择变更(anchor As 已注册界面锚点)
        If anchor.AnchorControl.IsDisposed Then Exit Sub
        Dim qualityPage = anchor.Surface.私有界面_质量
        If qualityPage.正在更新插件质量选项() Then Exit Sub
        Dim selectedId = qualityPage.获取当前质量选项ID()
        Dim appliedChoices As List(Of 已应用安全下拉项)
        SyncLock 同步锁
            appliedChoices = anchor.AppliedChoices.Values.ToList()
        End SyncLock

        ' 先撤销所有旧选项，再应用新选项；这样多个插件修改同一目标控件时不受注册顺序影响。
        For Each applied In appliedChoices.Where(Function(item) item.IsSelected)
            If Not String.Equals(
                applied.Registration.Extension.ChoiceId,
                selectedId,
                StringComparison.OrdinalIgnoreCase) Then
                更新安全下拉项状态(anchor, applied, False)
            End If
        Next
        For Each applied In appliedChoices.Where(
            Function(item) String.Equals(
                item.Registration.Extension.ChoiceId,
                selectedId,
                StringComparison.OrdinalIgnoreCase))
            更新安全下拉项状态(anchor, applied, True)
        Next
    End Sub

    Private Sub 更新安全下拉项状态(anchor As 已注册界面锚点,
                                applied As 已应用安全下拉项,
                                isSelected As Boolean)
        If applied.IsSelected = isSelected Then Exit Sub
        Dim extension = applied.Registration.Extension

        If isSelected Then
            For Each targetAnchorId In extension.DisabledAnchors.Distinct(StringComparer.OrdinalIgnoreCase)
                Dim target = 获取同界面锚点控件(anchor.Surface, targetAnchorId)
                If target Is Nothing Then Continue For
                applied.EnabledSnapshots(targetAnchorId) = target.Enabled
                target.Enabled = False
            Next
            For Each pair In extension.ValueOverrides
                Dim target = 获取同界面锚点控件(anchor.Surface, pair.Key)
                If target Is Nothing Then Continue For
                target.Text = If(pair.Value, "")
            Next
        Else
            For Each pair In applied.EnabledSnapshots
                Dim target = 获取同界面锚点控件(anchor.Surface, pair.Key)
                If target IsNot Nothing Then target.Enabled = pair.Value
            Next
            applied.EnabledSnapshots.Clear()
        End If

        applied.IsSelected = isSelected
        applied.Context.IsSelectedValue = isSelected
        extension.SelectionChanged?.Invoke(applied.Context, isSelected)
        applied.Context.RequestParameterRefresh()
    End Sub

    Private Sub 注销安全下拉项(registration As 已注册安全下拉项)
        Dim removals As New List(Of Tuple(Of 已注册界面锚点, 已应用安全下拉项))
        Dim key = registration.PluginId & ":" & registration.Extension.Id
        SyncLock 同步锁
            安全下拉项列表.Remove(registration)
            For Each anchor In 界面锚点列表
                Dim applied As 已应用安全下拉项 = Nothing
                If anchor.AppliedChoices.TryGetValue(key, applied) Then
                    anchor.AppliedChoices.Remove(key)
                    获取参数面板状态(anchor.Surface).ChoiceContexts.Remove(applied.Context)
                    removals.Add(Tuple.Create(anchor, applied))
                End If
            Next
        End SyncLock

        For Each removal In removals
            在控件线程执行(
                removal.Item1.AnchorControl,
                Sub()
                    更新安全下拉项状态(removal.Item1, removal.Item2, False)
                    重建安全下拉项(removal.Item1)
                End Sub)
        Next
    End Sub

    Private Function 获取或创建插入槽(anchor As 已注册界面锚点) As TableLayoutPanel
        If anchor.Container IsNot Nothing AndAlso Not anchor.Container.IsDisposed Then Return anchor.Container

        Dim isPageSlot = anchor.Position = Ext插件界面锚点位置_v2.容器顶部 OrElse
                         anchor.Position = Ext插件界面锚点位置_v2.容器底部
        Dim parent As Control
        Dim slotDock As DockStyle
        If isPageSlot Then
            parent = anchor.AnchorControl
            slotDock = If(anchor.Position = Ext插件界面锚点位置_v2.容器顶部,
                          DockStyle.Top,
                          DockStyle.Bottom)
        Else
            parent = anchor.AnchorControl.Parent
            If parent Is Nothing Then Throw New InvalidOperationException($"界面锚点 {anchor.AnchorId} 尚未加入父容器")
            If anchor.AnchorControl.Dock <> DockStyle.Top AndAlso anchor.AnchorControl.Dock <> DockStyle.Bottom Then
                Throw New InvalidOperationException($"插入型界面锚点 {anchor.AnchorId} 必须引用 DockStyle.Top 或 DockStyle.Bottom 控件")
            End If
            slotDock = anchor.AnchorControl.Dock
        End If

        Dim slot As New TableLayoutPanel With {
            .Name = "PluginSlot_" & anchor.AnchorId.Replace("."c, "_"c).Replace("/"c, "_"c),
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .BackColor = Color.Transparent,
            .ColumnCount = 1,
            .Dock = slotDock,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .RowCount = 0
        }
        slot.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        parent.Controls.Add(slot)
        If isPageSlot Then
            parent.Controls.SetChildIndex(slot, 0)
        Else
            Dim targetIndex = parent.Controls.GetChildIndex(anchor.AnchorControl)
            Dim desiredIndex = If(anchor.Position = Ext插件界面锚点位置_v2.在目标之前,
                                  targetIndex + 1,
                                  targetIndex)
            parent.Controls.SetChildIndex(slot, Math.Min(Math.Max(desiredIndex, 0), parent.Controls.Count - 1))
        End If
        anchor.Container = slot
        Return slot
    End Function

    Private Sub 重排插入槽(anchor As 已注册界面锚点)
        Dim slot = anchor.Container
        If slot Is Nothing OrElse slot.IsDisposed Then Exit Sub
        Dim ordered As List(Of Control)
        SyncLock 同步锁
            ordered = anchor.Applied.Values.
                Where(Function(x) x.Control IsNot Nothing).
                OrderBy(Function(x) x.Registration.Extension.Order).
                ThenBy(Function(x) x.Registration.PluginId, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(x) x.Registration.Extension.Id, StringComparer.OrdinalIgnoreCase).
                Select(Function(x) x.Control).
                ToList()
        End SyncLock
        slot.SuspendLayout()
        Try
            slot.RowCount = ordered.Count
            slot.RowStyles.Clear()
            For index = 0 To ordered.Count - 1
                Dim control = ordered(index)
                control.Dock = DockStyle.Top
                slot.SetCellPosition(control, New TableLayoutPanelCellPosition(0, index))
                slot.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            Next
        Finally
            slot.ResumeLayout(True)
        End Try
    End Sub

    Private Sub 注销界面扩展(registration As 已注册界面扩展)
        Dim removals As New List(Of Tuple(Of 已注册界面锚点, 已应用界面扩展))
        Dim key = registration.PluginId & ":" & registration.Extension.Id
        SyncLock 同步锁
            界面扩展列表.Remove(registration)
            For Each anchor In 界面锚点列表
                Dim applied As 已应用界面扩展 = Nothing
                If anchor.Applied.TryGetValue(key, applied) Then
                    anchor.Applied.Remove(key)
                    获取参数面板状态(anchor.Surface).Contexts.Remove(applied.Context)
                    removals.Add(Tuple.Create(anchor, applied))
                End If
            Next
        End SyncLock

        For Each removal In removals
            在控件线程执行(removal.Item1.AnchorControl,
                    Sub()
                        清理界面扩展(removal.Item2)
                        If removal.Item2.ReplacedAnchor Then
                            ' Visible/Enabled 的读取值会合并父容器状态，不能作为控件自身快照。
                            removal.Item1.AnchorControl.Visible = True
                            removal.Item1.AnchorControl.Enabled = True
                        End If
                        removal.Item2.Control?.Dispose()
                        重排插入槽(removal.Item1)
                    End Sub)
        Next
    End Sub

    Private Sub 移除界面锚点(anchor As 已注册界面锚点)
        Dim appliedItems As List(Of 已应用界面扩展)
        SyncLock 同步锁
            界面锚点列表.Remove(anchor)
            Dim state = 获取参数面板状态(anchor.Surface)
            For Each item In anchor.Applied.Values
                state.Contexts.Remove(item.Context)
            Next
            For Each item In anchor.AppliedChoices.Values
                state.ChoiceContexts.Remove(item.Context)
            Next
            appliedItems = anchor.Applied.Values.ToList()
            anchor.Applied.Clear()
            anchor.AppliedChoices.Clear()
        End SyncLock
        For Each item In appliedItems
            清理界面扩展(item)
        Next
        anchor.Container?.Dispose()
    End Sub

    Private Sub 清理界面扩展(applied As 已应用界面扩展)
        If applied Is Nothing Then Exit Sub
        SyncLock applied
            If applied.CleanupCalled Then Exit Sub
            applied.CleanupCalled = True
        End SyncLock
        Try
            applied.Registration.Extension.Cleanup?.Invoke(applied.Context)
        Catch ex As Exception
            Debug.WriteLine(
                $"[FFmpegFreeUI Plugin/Warning] {applied.Registration.PluginId}: " &
                $"清理界面扩展 {applied.Registration.Extension.Id} 失败：{ex}")
        End Try
    End Sub

    Private Function 获取参数面板状态(surface As Form_v6_参数面板) As 参数面板插件状态
        Return 参数面板状态表.GetValue(surface, Function(ignored) New 参数面板插件状态)
    End Function

    Private Function 读取界面状态(surface As Form_v6_参数面板, pluginId As String) As String
        Dim state = 获取参数面板状态(surface)
        SyncLock 同步锁
            Dim value As String = Nothing
            If state.Values.TryGetValue(pluginId, value) Then Return 规范化状态Json(value)
        End SyncLock
        Return "{}"
    End Function

    Private Function 获取同界面锚点控件(surface As Form_v6_参数面板, anchorId As String) As Control
        If surface Is Nothing OrElse String.IsNullOrWhiteSpace(anchorId) Then Return Nothing
        SyncLock 同步锁
            Dim anchor = 界面锚点列表.FirstOrDefault(
                Function(x) x.Surface Is surface AndAlso
                            String.Equals(x.AnchorId, anchorId.Trim(), StringComparison.OrdinalIgnoreCase))
            If anchor Is Nothing OrElse anchor.AnchorControl.IsDisposed Then Return Nothing
            Return anchor.AnchorControl
        End SyncLock
    End Function

    Private Sub 写入界面状态(surface As Form_v6_参数面板, pluginId As String, value As String)
        Dim state = 获取参数面板状态(surface)
        SyncLock 同步锁
            state.Values(pluginId) = 规范化状态Json(value)
        End SyncLock
    End Sub

    Private Function 规范化状态Json(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "{}"
        Using document = JsonDocument.Parse(value)
            Return document.RootElement.GetRawText()
        End Using
    End Function

    Private Function 包装处理器异常(registration As 已注册处理器, stageId As String, ex As Exception) As Exception
        Dim actual = If(TypeOf ex Is Reflection.TargetInvocationException AndAlso ex.InnerException IsNot Nothing, ex.InnerException, ex)
        Return New InvalidOperationException(
            $"插件 {registration.PluginId} 的处理器 {registration.Handler.Id} 在阶段 {stageId} 失败：{actual.Message}",
            actual)
    End Function

    Private Sub 在控件线程执行(control As Control, action As Action)
        If control Is Nothing OrElse control.IsDisposed OrElse action Is Nothing Then Exit Sub
        If control.IsHandleCreated AndAlso control.InvokeRequired Then
            control.Invoke(action)
        Else
            action()
        End If
    End Sub

    Private NotInheritable Class 插件作用域宿主
        Implements IExtFFmpegFreeUIHost, IDisposable

        Private ReadOnly _pluginId As String
        Private ReadOnly _displayName As String
        Private ReadOnly _ui As IExtPluginUiRegistry
        Private ReadOnly _pipeline As IExtPluginPipelineRegistry
        Private ReadOnly _behaviors As IExtPluginBehaviorRegistry
        Private ReadOnly _resources As IExtPluginResourceRegistry
        Private ReadOnly _parameterPanel As IExtPluginParameterPanelCatalog
        Private ReadOnly _commands As IExtPluginCommandRegistry
        Private ReadOnly _registrations As New List(Of IDisposable)
        Private ReadOnly _registrationLock As New Object
        Private _disposed As Boolean

        Public Sub New(pluginId As String, displayName As String)
            _pluginId = pluginId
            _displayName = displayName
            _ui = New 插件界面注册表(pluginId, AddressOf 跟踪注册)
            _pipeline = New 插件处理注册表(pluginId, AddressOf 跟踪注册)
            _behaviors = New 插件行为注册表(pluginId, AddressOf 跟踪注册)
            _resources = New 插件资源注册表(pluginId, AddressOf 跟踪注册)
            _parameterPanel = New 插件参数面板目录()
            _commands = New 插件命令注册表(pluginId, AddressOf 跟踪注册)
        End Sub

        Public ReadOnly Property ApiVersion As Version Implements IExtFFmpegFreeUIHost.ApiVersion
            Get
                ' 返回实际宿主能力，而不是动态读取 SDK 声明，避免版本错配时误报支持。
                Return 支持API版本
            End Get
        End Property

        Public ReadOnly Property HostVersion As String Implements IExtFFmpegFreeUIHost.HostVersion
            Get
                Return GetType(插件管理).Assembly.GetName().Version?.ToString()
            End Get
        End Property

        Public ReadOnly Property Ui As IExtPluginUiRegistry Implements IExtFFmpegFreeUIHost.Ui
            Get
                Return _ui
            End Get
        End Property

        Public ReadOnly Property Pipeline As IExtPluginPipelineRegistry Implements IExtFFmpegFreeUIHost.Pipeline
            Get
                Return _pipeline
            End Get
        End Property

        Public ReadOnly Property Resources As IExtPluginResourceRegistry Implements IExtFFmpegFreeUIHost.Resources
            Get
                Return _resources
            End Get
        End Property

        Public ReadOnly Property Behaviors As IExtPluginBehaviorRegistry Implements IExtFFmpegFreeUIHost.Behaviors
            Get
                Return _behaviors
            End Get
        End Property

        Public ReadOnly Property ParameterPanel As IExtPluginParameterPanelCatalog Implements IExtFFmpegFreeUIHost.ParameterPanel
            Get
                Return _parameterPanel
            End Get
        End Property

        Public ReadOnly Property Commands As IExtPluginCommandRegistry Implements IExtFFmpegFreeUIHost.Commands
            Get
                Return _commands
            End Get
        End Property

        Public Sub Log(level As ExtPluginLogLevel, message As String, Optional exception As Exception = Nothing) Implements IExtFFmpegFreeUIHost.Log
            Dim prefix = If(_displayName = "", _pluginId, _displayName)
            Debug.WriteLine($"[FFmpegFreeUI Plugin/{level}] {prefix}: {message}{If(exception Is Nothing, "", " " & exception.ToString())}")
        End Sub

        Private Sub 跟踪注册(registration As IDisposable)
            SyncLock _registrationLock
                If _disposed Then
                    registration.Dispose()
                    Throw New ObjectDisposedException(NameOf(插件作用域宿主))
                End If
                _registrations.Add(registration)
            End SyncLock
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dim registrations As List(Of IDisposable)
            SyncLock _registrationLock
                If _disposed Then Exit Sub
                _disposed = True
                registrations = _registrations.ToList()
                _registrations.Clear()
            End SyncLock
            For index = registrations.Count - 1 To 0 Step -1
                registrations(index).Dispose()
            Next
        End Sub
    End Class

    Private NotInheritable Class 插件界面注册表
        Implements IExtPluginUiRegistry

        Private ReadOnly _pluginId As String
        Private ReadOnly _track As Action(Of IDisposable)

        Public Sub New(pluginId As String, track As Action(Of IDisposable))
            _pluginId = pluginId
            _track = track
        End Sub

        Public ReadOnly Property AvailableAnchors As IReadOnlyCollection(Of String) Implements IExtPluginUiRegistry.AvailableAnchors
            Get
                Return 获取可用界面锚点()
            End Get
        End Property

        Public ReadOnly Property AvailableChoiceAnchors As IReadOnlyCollection(Of String) Implements IExtPluginUiRegistry.AvailableChoiceAnchors
            Get
                Return ExtFFmpegFreeUIUiChoiceAnchors.All
            End Get
        End Property

        Public Function Register(extension As ExtPluginUiExtension) As IDisposable Implements IExtPluginUiRegistry.Register
            Dim registration = 注册界面扩展(_pluginId, extension)
            _track.Invoke(registration)
            Return registration
        End Function

        Public Function RegisterChoice(extension As ExtPluginUiChoiceExtension) As IDisposable Implements IExtPluginUiRegistry.RegisterChoice
            Dim registration = 注册安全下拉项(_pluginId, extension)
            _track.Invoke(registration)
            Return registration
        End Function
    End Class

    Private NotInheritable Class 插件行为注册表
        Implements IExtPluginBehaviorRegistry

        Private ReadOnly _pluginId As String
        Private ReadOnly _track As Action(Of IDisposable)

        Public Sub New(pluginId As String, track As Action(Of IDisposable))
            _pluginId = pluginId
            _track = track
        End Sub

        Public ReadOnly Property AvailableBehaviors As IReadOnlyCollection(Of String) Implements IExtPluginBehaviorRegistry.AvailableBehaviors
            Get
                Return ExtFFmpegFreeUIBehaviors.All
            End Get
        End Property

        Public Function Register(handler As ExtPluginBehaviorHandler) As IDisposable Implements IExtPluginBehaviorRegistry.Register
            Dim registration = 注册行为处理器(_pluginId, handler)
            _track.Invoke(registration)
            Return registration
        End Function
    End Class

    Private NotInheritable Class 插件资源注册表
        Implements IExtPluginResourceRegistry

        Private ReadOnly _pluginId As String
        Private ReadOnly _track As Action(Of IDisposable)

        Public Sub New(pluginId As String, track As Action(Of IDisposable))
            _pluginId = pluginId
            _track = track
        End Sub

        Public ReadOnly Property AvailableResources As IReadOnlyCollection(Of String) Implements IExtPluginResourceRegistry.AvailableResources
            Get
                Return 获取可用资源()
            End Get
        End Property

        Public Function Claim(resourceClaim As ExtPluginResourceClaim) As IDisposable Implements IExtPluginResourceRegistry.Claim
            Dim registration = 注册资源声明(_pluginId, resourceClaim)
            _track.Invoke(registration)
            Return registration
        End Function
    End Class

    Private NotInheritable Class 插件参数面板目录
        Implements IExtPluginParameterPanelCatalog

        Public ReadOnly Property AvailablePages As IReadOnlyCollection(Of ExtPluginParameterPageDescriptor) Implements IExtPluginParameterPanelCatalog.AvailablePages
            Get
                Return 获取参数页面目录()
            End Get
        End Property

        Public ReadOnly Property AvailableControls As IReadOnlyCollection(Of ExtPluginParameterControlDescriptor) Implements IExtPluginParameterPanelCatalog.AvailableControls
            Get
                Return 获取参数控件目录()
            End Get
        End Property
    End Class

    Private NotInheritable Class 插件命令注册表
        Implements IExtPluginCommandRegistry

        Private ReadOnly _pluginId As String
        Private ReadOnly _track As Action(Of IDisposable)

        Public Sub New(pluginId As String, track As Action(Of IDisposable))
            _pluginId = pluginId
            _track = track
        End Sub

        Public Function RegisterParameterProvider(provider As ExtPluginCommandParameterProvider) As IDisposable Implements IExtPluginCommandRegistry.RegisterParameterProvider
            Dim registration = 注册命令参数提供器(_pluginId, provider)
            _track.Invoke(registration)
            Return registration
        End Function

        Public Function RegisterStepProvider(provider As ExtPluginCommandStepProvider) As IDisposable Implements IExtPluginCommandRegistry.RegisterStepProvider
            Dim registration = 注册命令步骤提供器(_pluginId, provider)
            _track.Invoke(registration)
            Return registration
        End Function
    End Class

    Private NotInheritable Class 插件处理注册表
        Implements IExtPluginPipelineRegistry

        Private ReadOnly _pluginId As String
        Private ReadOnly _track As Action(Of IDisposable)

        Public Sub New(pluginId As String, track As Action(Of IDisposable))
            _pluginId = pluginId
            _track = track
        End Sub

        Public ReadOnly Property AvailableStages As IReadOnlyCollection(Of String) Implements IExtPluginPipelineRegistry.AvailableStages
            Get
                Return ExtFFmpegFreeUIPipelineStages.All
            End Get
        End Property

        Public Function Register(handler As ExtPluginPipelineHandler) As IDisposable Implements IExtPluginPipelineRegistry.Register
            Dim registration = 注册处理器(_pluginId, handler)
            _track.Invoke(registration)
            Return registration
        End Function
    End Class

    Private NotInheritable Class 插件界面上下文
        Implements IExtPluginUiContext

        Private ReadOnly _anchor As 已注册界面锚点
        Private ReadOnly _container As Control

        Public Sub New(pluginId As String, extensionId As String, anchor As 已注册界面锚点, container As Control)
            Me.PluginId = pluginId
            Me.ExtensionId = extensionId
            Me.AnchorId = anchor.AnchorId
            Me.SurfaceId = 获取参数面板状态(anchor.Surface).SurfaceId
            _anchor = anchor
            _container = container
        End Sub

        Public ReadOnly Property PluginId As String Implements IExtPluginUiContext.PluginId
        Public ReadOnly Property ExtensionId As String Implements IExtPluginUiContext.ExtensionId
        Public ReadOnly Property AnchorId As String Implements IExtPluginUiContext.AnchorId
        Public ReadOnly Property SurfaceId As String Implements IExtPluginUiContext.SurfaceId

        Public ReadOnly Property AnchorControl As Control Implements IExtPluginUiContext.AnchorControl
            Get
                Return _anchor.AnchorControl
            End Get
        End Property

        Public ReadOnly Property ContainerControl As Control Implements IExtPluginUiContext.ContainerControl
            Get
                Return _container
            End Get
        End Property

        Public Function GetAnchorControl(anchorId As String) As Control Implements IExtPluginUiContext.GetAnchorControl
            Return 获取同界面锚点控件(_anchor.Surface, anchorId)
        End Function

        Public Property StateJson As String Implements IExtPluginUiContext.StateJson
            Get
                Return 读取界面状态(_anchor.Surface, PluginId)
            End Get
            Set(value As String)
                写入界面状态(_anchor.Surface, PluginId, value)
            End Set
        End Property

        Public Event StateRestored As EventHandler Implements IExtPluginUiContext.StateRestored

        Friend Sub 通知状态已还原()
            Dim notify As Action = Sub() RaiseEvent StateRestored(Me, EventArgs.Empty)
            在控件线程执行(_anchor.AnchorControl, notify)
        End Sub

        Public Sub RequestParameterRefresh() Implements IExtPluginUiContext.RequestParameterRefresh
            在控件线程执行(_anchor.AnchorControl, Sub() _anchor.Surface.请求刷新参数状态())
        End Sub
    End Class

    Private NotInheritable Class 安全下拉项上下文
        Implements IExtPluginUiChoiceContext

        Private ReadOnly _registration As 已注册安全下拉项
        Private ReadOnly _anchor As 已注册界面锚点

        Public Sub New(registration As 已注册安全下拉项, anchor As 已注册界面锚点)
            _registration = registration
            _anchor = anchor
            PluginId = registration.PluginId
            ExtensionId = registration.Extension.Id
            AnchorId = registration.Extension.AnchorId
            ChoiceId = registration.Extension.ChoiceId
            SurfaceId = 获取参数面板状态(anchor.Surface).SurfaceId
        End Sub

        Friend Property Applied As 已应用安全下拉项
        Friend Property IsSelectedValue As Boolean
        Public ReadOnly Property PluginId As String Implements IExtPluginUiChoiceContext.PluginId
        Public ReadOnly Property ExtensionId As String Implements IExtPluginUiChoiceContext.ExtensionId
        Public ReadOnly Property AnchorId As String Implements IExtPluginUiChoiceContext.AnchorId
        Public ReadOnly Property ChoiceId As String Implements IExtPluginUiChoiceContext.ChoiceId
        Public ReadOnly Property SurfaceId As String Implements IExtPluginUiChoiceContext.SurfaceId
        Public ReadOnly Property IsSelected As Boolean Implements IExtPluginUiChoiceContext.IsSelected
            Get
                Return IsSelectedValue
            End Get
        End Property

        Public Property StateJson As String Implements IExtPluginUiChoiceContext.StateJson
            Get
                Return 读取界面状态(_anchor.Surface, PluginId)
            End Get
            Set(value As String)
                写入界面状态(_anchor.Surface, PluginId, value)
            End Set
        End Property

        Public Sub RequestParameterRefresh() Implements IExtPluginUiChoiceContext.RequestParameterRefresh
            在控件线程执行(_anchor.AnchorControl, Sub() _anchor.Surface.请求刷新参数状态())
        End Sub

        Friend Sub 通知状态已还原()
            Dim restore = _registration.Extension.RestoreSelection
            If restore Is Nothing Then Exit Sub
            在控件线程执行(
                _anchor.AnchorControl,
                Sub()
                    If restore.Invoke(Me) Then
                        _anchor.Surface.私有界面_质量.选择质量选项(ChoiceId)
                    ElseIf IsSelectedValue Then
                        _anchor.Surface.私有界面_质量.选择质量选项(_registration.Extension.NativeFallbackChoiceId)
                    End If
                End Sub)
        End Sub
    End Class

    Private NotInheritable Class 注销句柄
        Implements IDisposable

        Private _disposeAction As Action

        Public Sub New(disposeAction As Action)
            _disposeAction = disposeAction
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Dim action = Interlocked.Exchange(_disposeAction, Nothing)
            action?.Invoke()
        End Sub
    End Class

    Private NotInheritable Class 参数面板插件状态
        Public ReadOnly SurfaceId As String = Guid.NewGuid().ToString("N")
        Public ReadOnly Values As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly Contexts As New List(Of 插件界面上下文)
        Public ReadOnly ChoiceContexts As New List(Of 安全下拉项上下文)
    End Class

    Private NotInheritable Class 已注册界面扩展
        Public Property PluginId As String
        Public Property Extension As ExtPluginUiExtension
    End Class

    Private NotInheritable Class 已注册安全下拉项
        Public Property PluginId As String
        Public Property Extension As ExtPluginUiChoiceExtension
    End Class

    Private NotInheritable Class 已注册处理器
        Public Property PluginId As String
        Public Property Handler As ExtPluginPipelineHandler
    End Class

    Private NotInheritable Class 已注册行为处理器
        Public Property PluginId As String
        Public Property Handler As ExtPluginBehaviorHandler
    End Class

    Private NotInheritable Class 已注册资源声明
        Public Property PluginId As String
        Public Property Claim As ExtPluginResourceClaim
    End Class

    Private NotInheritable Class 已注册命令参数提供器
        Public Property PluginId As String
        Public Property Provider As ExtPluginCommandParameterProvider
    End Class

    Private NotInheritable Class 已注册命令步骤提供器
        Public Property PluginId As String
        Public Property Provider As ExtPluginCommandStepProvider
    End Class

    Private NotInheritable Class 已注册界面锚点
        Public Property AnchorId As String
        Public Property AnchorControl As Control
        Public Property Surface As Form_v6_参数面板
        Public Property Position As Ext插件界面锚点位置_v2
        Public Property Container As TableLayoutPanel
        Public ReadOnly Applied As New Dictionary(Of String, 已应用界面扩展)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly AppliedChoices As New Dictionary(Of String, 已应用安全下拉项)(StringComparer.OrdinalIgnoreCase)
        Public Property ChoiceEventAttached As Boolean
    End Class

    Private NotInheritable Class 已应用界面扩展
        Public Property Registration As 已注册界面扩展
        Public Property Context As 插件界面上下文
        Public Property Control As Control
        Public Property ReplacedAnchor As Boolean
        Public Property CleanupCalled As Boolean
    End Class

    Private NotInheritable Class 已应用安全下拉项
        Public Property Registration As 已注册安全下拉项
        Public Property Context As 安全下拉项上下文
        Public Property IsSelected As Boolean
        Public ReadOnly EnabledSnapshots As New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)
    End Class

End Module
