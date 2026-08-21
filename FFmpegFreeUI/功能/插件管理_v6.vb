Imports System.Diagnostics
Imports System.IO
Imports System.Reflection
Imports System.Reflection.Metadata
Imports System.Reflection.PortableExecutable
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization

Public Enum 插件接口类型_v6
    未识别 = 0
    官方API = 1
    ExtAPI = 2
    官方与Ext = 3
End Enum

Public NotInheritable Class 插件信息_v6
    Public Property 插件键 As String = ""
    Public Property 文件名 As String = ""
    Public Property 文件路径 As String = ""
    Public Property 程序集名称 As String = ""
    Public Property 程序集版本 As String = ""
    Public Property 显示名称 As String = ""
    Public Property 插件版本 As String = ""
    Public Property 接口类型 As 插件接口类型_v6
    Public Property ExtSDK程序集版本 As String = ""
    Public Property ExtAPI最低版本 As String = ""
    Public Property Ext插件标识 As New List(Of String)
    Public Property 已启用 As Boolean = True
    Public Property 处理顺序 As Integer
    Public Property 启动时已启用 As Boolean
    Public Property 已加载 As Boolean
    Public Property 加载状态 As String = "未加载"
    Public Property 加载错误 As String = ""
    Public Property 元数据错误 As String = ""

    Public ReadOnly Property 等待重启 As Boolean
        Get
            Return 已启用 <> 启动时已启用
        End Get
    End Property

    Friend Function 创建副本() As 插件信息_v6
        Return New 插件信息_v6 With {
            .插件键 = 插件键,
            .文件名 = 文件名,
            .文件路径 = 文件路径,
            .程序集名称 = 程序集名称,
            .程序集版本 = 程序集版本,
            .显示名称 = 显示名称,
            .插件版本 = 插件版本,
            .接口类型 = 接口类型,
            .ExtSDK程序集版本 = ExtSDK程序集版本,
            .ExtAPI最低版本 = ExtAPI最低版本,
            .Ext插件标识 = Ext插件标识.ToList(),
            .已启用 = 已启用,
            .处理顺序 = 处理顺序,
            .启动时已启用 = 启动时已启用,
            .已加载 = 已加载,
            .加载状态 = 加载状态,
            .加载错误 = 加载错误,
            .元数据错误 = 元数据错误
        }
    End Function
End Class

Friend NotInheritable Class 插件管理配置项_v6
    Public Property Enabled As Boolean = True
    Public Property Order As Integer
End Class

Friend NotInheritable Class 插件管理配置文件_v6
    Public Property Version As Integer = 1
    Public Property OrderConfigured As Boolean
    Public Property Plugins As New Dictionary(Of String, 插件管理配置项_v6)(StringComparer.OrdinalIgnoreCase)
End Class

Public Class 插件管理

    Public Shared ReadOnly Property 由插件加载的自定义界面 As New Dictionary(Of String, Control)(StringComparer.CurrentCultureIgnoreCase)
    Public Shared Event 插件列表已变化 As EventHandler

    Private Shared ReadOnly 插件状态锁 As New Object
    Private Shared ReadOnly 插件目录 As New Dictionary(Of String, 插件信息_v6)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly Ext插件标识到文件键 As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private Shared 当前配置 As New 插件管理配置文件_v6
    Private Shared 配置已读取 As Boolean
    Private Shared 目录已扫描 As Boolean
    Private Shared 启动加载已执行 As Boolean
    Private Shared 配置错误 As String = ""

    Private Shared ReadOnly 编码队列事件订阅列表 As New List(Of 编码队列事件订阅)
    Private Shared ReadOnly 编码队列事件锁 As New Object
    Private Shared 编码队列事件注册序号 As Long
    Private Shared ReadOnly 编码队列事件序列化选项 As New JsonSerializerOptions With {
        .PropertyNamingPolicy = Nothing,
        .DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        .WriteIndented = False
    }
    Private Shared 编码队列事件已连接 As Boolean

    Private Shared ReadOnly 配置序列化选项 As New JsonSerializerOptions With {
        .PropertyNameCaseInsensitive = True,
        .WriteIndented = True
    }

    Public Shared ReadOnly Property 插件文件夹路径 As String
        Get
            Return Path.Combine(Application.StartupPath, "Plugin")
        End Get
    End Property

    Private Shared ReadOnly Property 配置文件路径 As String
        Get
            Return Path.Combine(插件文件夹路径, "ExtPluginManager.json")
        End Get
    End Property

    Public Shared Sub 启动时加载插件()
        SyncLock 插件状态锁
            If 启动加载已执行 Then Exit Sub
            启动加载已执行 = True
        End SyncLock

        If Not 编码队列事件已连接 Then
            AddHandler 编码队列_v6.插件事件已触发, AddressOf 编码队列事件处理
            编码队列事件已连接 = True
        End If

        扫描插件目录(作为启动扫描:=True)
        Dim 要加载的插件 = 获取插件列表().Where(Function(item) item.已启用).ToList()
        For Each plugin In 要加载的插件
            Try
                Dim result = 加载单个插件(plugin)
                SyncLock 插件状态锁
                    Dim live As 插件信息_v6 = Nothing
                    If 插件目录.TryGetValue(plugin.插件键, live) Then
                        live.已加载 = result.已加载
                        live.加载状态 = result.状态
                        live.加载错误 = result.错误
                    End If
                End SyncLock
            Catch ex As Exception
                SyncLock 插件状态锁
                    Dim live As 插件信息_v6 = Nothing
                    If 插件目录.TryGetValue(plugin.插件键, live) Then
                        live.已加载 = False
                        live.加载状态 = "加载失败"
                        live.加载错误 = 获取异常消息(ex)
                    End If
                End SyncLock
            End Try
        Next
        通知插件列表变化()
    End Sub

    Public Shared Sub 刷新插件目录()
        扫描插件目录(作为启动扫描:=False)
        通知插件列表变化()
    End Sub

    Public Shared Function 获取插件列表() As List(Of 插件信息_v6)
        Dim shouldScan As Boolean
        SyncLock 插件状态锁
            shouldScan = Not 目录已扫描
        End SyncLock
        If shouldScan Then 扫描插件目录(作为启动扫描:=False)

        SyncLock 插件状态锁
            Return 插件目录.Values.
                OrderBy(Function(item) item.处理顺序).
                ThenBy(Function(item) item.文件名, StringComparer.OrdinalIgnoreCase).
                Select(Function(item) item.创建副本()).
                ToList()
        End SyncLock
    End Function

    Public Shared Function 获取配置错误() As String
        SyncLock 插件状态锁
            Return 配置错误
        End SyncLock
    End Function

    Public Shared Sub 设置插件启用状态(插件键 As String, enabled As Boolean)
        Dim key = If(插件键, "").Trim()
        If key = "" Then Throw New ArgumentException("插件键不能为空", NameOf(插件键))
        确保读取配置()

        SyncLock 插件状态锁
            Dim info As 插件信息_v6 = Nothing
            If Not 插件目录.TryGetValue(key, info) Then Throw New FileNotFoundException("插件文件已经不存在", key)
            info.已启用 = enabled

            Dim setting As 插件管理配置项_v6 = Nothing
            If Not 当前配置.Plugins.TryGetValue(key, setting) Then
                setting = New 插件管理配置项_v6 With {.Order = info.处理顺序}
                当前配置.Plugins(key) = setting
            End If
            setting.Enabled = enabled
            更新插件状态文本(info)
        End SyncLock

        保存配置()
        通知插件列表变化()
    End Sub

    Public Shared Sub 保存插件处理顺序(按顺序排列的插件键 As IEnumerable(Of String))
        If 按顺序排列的插件键 Is Nothing Then Throw New ArgumentNullException(NameOf(按顺序排列的插件键))
        确保读取配置()

        Dim requested = 按顺序排列的插件键.
            Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
            Select(Function(item) item.Trim()).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()

        SyncLock 插件状态锁
            当前配置.OrderConfigured = True
            Dim ordered As New List(Of String)
            For Each key In requested
                If 插件目录.ContainsKey(key) Then ordered.Add(key)
            Next
            For Each info In 插件目录.Values.OrderBy(Function(item) item.处理顺序).ThenBy(Function(item) item.文件名, StringComparer.OrdinalIgnoreCase)
                If Not ordered.Contains(info.插件键, StringComparer.OrdinalIgnoreCase) Then ordered.Add(info.插件键)
            Next

            For index = 0 To ordered.Count - 1
                Dim key = ordered(index)
                Dim orderValue = index * 100
                插件目录(key).处理顺序 = orderValue
                Dim setting As 插件管理配置项_v6 = Nothing
                If Not 当前配置.Plugins.TryGetValue(key, setting) Then
                    setting = New 插件管理配置项_v6 With {.Enabled = 插件目录(key).已启用}
                    当前配置.Plugins(key) = setting
                End If
                setting.Order = orderValue
            Next
        End SyncLock

        保存配置()
        通知插件列表变化()
    End Sub

    Public Shared Function 获取插件处理顺序(pluginId As String) As Integer
        Dim id = If(pluginId, "").Trim()
        SyncLock 插件状态锁
            Dim key As String = Nothing
            If id <> "" AndAlso Ext插件标识到文件键.TryGetValue(id, key) AndAlso 插件目录.ContainsKey(key) Then
                Return 插件目录(key).处理顺序
            End If
            Dim matched = 插件目录.Values.FirstOrDefault(
                Function(item) item.Ext插件标识.Any(Function(value) String.Equals(value, id, StringComparison.OrdinalIgnoreCase)))
            If matched IsNot Nothing Then Return matched.处理顺序
        End SyncLock
        Return Integer.MaxValue \ 2
    End Function

    Public Shared Sub 注册Ext插件信息(pluginId As String, displayName As String, assemblyPath As String)
        Dim id = If(pluginId, "").Trim()
        If id = "" Then Exit Sub
        Dim key = Path.GetFileName(If(assemblyPath, ""))
        If String.IsNullOrWhiteSpace(key) Then Exit Sub

        SyncLock 插件状态锁
            Ext插件标识到文件键(id) = key
            Dim info As 插件信息_v6 = Nothing
            If 插件目录.TryGetValue(key, info) Then
                If Not info.Ext插件标识.Contains(id, StringComparer.OrdinalIgnoreCase) Then info.Ext插件标识.Add(id)
                If Not String.IsNullOrWhiteSpace(displayName) Then info.显示名称 = displayName.Trim()
            End If
        End SyncLock
    End Sub

    Private Shared Sub 扫描插件目录(作为启动扫描 As Boolean)
        确保读取配置()

        Dim files As New List(Of String)
        If Directory.Exists(插件文件夹路径) Then
            Try
                files = Directory.GetFiles(插件文件夹路径, "*.3fui.dll", SearchOption.TopDirectoryOnly).
                    OrderBy(Function(item) Path.GetFileName(item), StringComparer.OrdinalIgnoreCase).
                    ToList()
            Catch ex As Exception
                SyncLock 插件状态锁
                    配置错误 = $"读取插件目录失败：{ex.Message}"
                End SyncLock
            End Try
        End If

        Dim scanned = files.Select(Function(file) 读取插件元数据(file)).ToList()
        Dim configChanged As Boolean
        SyncLock 插件状态锁
            Dim nextOrder = If(当前配置.Plugins.Count = 0, 0, 当前配置.Plugins.Values.Max(Function(item) item.Order) + 100)
            Dim merged As New Dictionary(Of String, 插件信息_v6)(StringComparer.OrdinalIgnoreCase)

            For Each info In scanned
                Dim setting As 插件管理配置项_v6 = Nothing
                If Not 当前配置.Plugins.TryGetValue(info.插件键, setting) Then
                    setting = New 插件管理配置项_v6 With {
                        .Enabled = True,
                        .Order = If(当前配置.OrderConfigured, nextOrder, 0)
                    }
                    If 当前配置.OrderConfigured Then nextOrder += 100
                    当前配置.Plugins(info.插件键) = setting
                    configChanged = True
                End If
                info.已启用 = setting.Enabled
                info.处理顺序 = setting.Order

                Dim previous As 插件信息_v6 = Nothing
                If 插件目录.TryGetValue(info.插件键, previous) Then
                    info.启动时已启用 = previous.启动时已启用
                    info.已加载 = previous.已加载
                    info.加载状态 = previous.加载状态
                    info.加载错误 = previous.加载错误
                    info.Ext插件标识 = previous.Ext插件标识.ToList()
                    If previous.Ext插件标识.Count > 0 AndAlso Not String.IsNullOrWhiteSpace(previous.显示名称) Then info.显示名称 = previous.显示名称
                Else
                    info.启动时已启用 = If(作为启动扫描, info.已启用, False)
                    info.加载状态 = If(info.已启用, "等待重启后加载", "已禁用")
                End If

                If 作为启动扫描 Then
                    info.启动时已启用 = info.已启用
                    info.已加载 = False
                    info.加载状态 = If(info.已启用, "等待加载", "已禁用")
                    info.加载错误 = ""
                Else
                    更新插件状态文本(info)
                End If
                merged(info.插件键) = info
            Next

            插件目录.Clear()
            For Each pair In merged
                插件目录(pair.Key) = pair.Value
            Next
            Dim activeKeys = New HashSet(Of String)(插件目录.Keys, StringComparer.OrdinalIgnoreCase)
            For Each pluginId In Ext插件标识到文件键.Where(Function(pair) Not activeKeys.Contains(pair.Value)).Select(Function(pair) pair.Key).ToList()
                Ext插件标识到文件键.Remove(pluginId)
            Next
            目录已扫描 = True
        End SyncLock

        If configChanged Then
            Try
                保存配置()
            Catch ex As Exception
                SyncLock 插件状态锁
                    配置错误 = $"保存插件管理配置失败：{ex.Message}"
                End SyncLock
            End Try
        End If
    End Sub

    Private Shared Sub 更新插件状态文本(info As 插件信息_v6)
        If info Is Nothing Then Exit Sub
        If info.等待重启 Then
            info.加载状态 = If(info.已启用, "等待重启后启用", "等待重启后禁用")
        ElseIf Not info.已启用 Then
            info.加载状态 = "已禁用"
        ElseIf info.已加载 AndAlso info.加载状态 <> "部分加载" Then
            info.加载状态 = "已加载"
        ElseIf Not info.已加载 AndAlso String.IsNullOrWhiteSpace(info.加载错误) Then
            info.加载状态 = "等待重启后加载"
        End If
    End Sub

    Private Shared Sub 确保读取配置()
        SyncLock 插件状态锁
            If 配置已读取 Then Exit Sub
            配置已读取 = True
        End SyncLock

        Dim loaded As New 插件管理配置文件_v6
        Dim loadError = ""
        If File.Exists(配置文件路径) Then
            Try
                loaded = JsonSerializer.Deserialize(Of 插件管理配置文件_v6)(File.ReadAllText(配置文件路径, Encoding.UTF8), 配置序列化选项)
                If loaded Is Nothing Then loaded = New 插件管理配置文件_v6
            Catch ex As Exception
                loaded = New 插件管理配置文件_v6
                Try
                    Dim backupPath = 配置文件路径 & $".invalid-{DateTime.Now:yyyyMMdd-HHmmss}"
                    File.Copy(配置文件路径, backupPath, overwrite:=False)
                    loadError = $"读取 ExtPluginManager.json 失败，原文件已备份为 {Path.GetFileName(backupPath)}：{ex.Message}"
                Catch
                    loadError = $"读取 ExtPluginManager.json 失败，已暂时使用默认配置：{ex.Message}"
                End Try
            End Try
        End If

        Dim normalized As New Dictionary(Of String, 插件管理配置项_v6)(StringComparer.OrdinalIgnoreCase)
        If loaded.Plugins IsNot Nothing Then
            For Each pair In loaded.Plugins
                If Not String.IsNullOrWhiteSpace(pair.Key) AndAlso pair.Value IsNot Nothing Then normalized(pair.Key.Trim()) = pair.Value
            Next
        End If
        loaded.Version = 1
        loaded.Plugins = normalized

        SyncLock 插件状态锁
            当前配置 = loaded
            配置错误 = loadError
        End SyncLock
    End Sub

    Private Shared Sub 保存配置()
        Dim json As String
        SyncLock 插件状态锁
            json = JsonSerializer.Serialize(当前配置, 配置序列化选项)
        End SyncLock

        Directory.CreateDirectory(插件文件夹路径)
        Dim temporaryPath = 配置文件路径 & ".tmp"
        File.WriteAllText(temporaryPath, json, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
        File.Move(temporaryPath, 配置文件路径, overwrite:=True)
        SyncLock 插件状态锁
            配置错误 = ""
        End SyncLock
    End Sub

    Private Shared Function 读取插件元数据(插件文件 As String) As 插件信息_v6
        Dim info As New 插件信息_v6 With {
            .插件键 = Path.GetFileName(插件文件),
            .文件名 = Path.GetFileName(插件文件),
            .文件路径 = Path.GetFullPath(插件文件),
            .显示名称 = Path.GetFileNameWithoutExtension(插件文件)
        }

        Try
            Dim versionInfo = FileVersionInfo.GetVersionInfo(插件文件)
            If Not String.IsNullOrWhiteSpace(versionInfo.ProductName) Then info.显示名称 = versionInfo.ProductName.Trim()
            If Not String.IsNullOrWhiteSpace(versionInfo.ProductVersion) Then
                info.插件版本 = versionInfo.ProductVersion.Trim()
            ElseIf Not String.IsNullOrWhiteSpace(versionInfo.FileVersion) Then
                info.插件版本 = versionInfo.FileVersion.Trim()
            End If
        Catch
        End Try

        Try
            Using stream = File.OpenRead(插件文件)
                Using reader As New PEReader(stream)
                    If Not reader.HasMetadata Then Throw New BadImageFormatException("文件不包含 .NET 元数据")
                    Dim metadata = reader.GetMetadataReader()
                    If metadata.IsAssembly Then
                        Dim definition = metadata.GetAssemblyDefinition()
                        info.程序集名称 = metadata.GetString(definition.Name)
                        info.程序集版本 = definition.Version.ToString()
                        If String.IsNullOrWhiteSpace(info.插件版本) Then info.插件版本 = info.程序集版本
                        If String.IsNullOrWhiteSpace(info.显示名称) Then info.显示名称 = info.程序集名称
                    End If

                    Dim hasOfficialEntry As Boolean
                    For Each handle In metadata.TypeDefinitions
                        Dim definition = metadata.GetTypeDefinition(handle)
                        If String.Equals(metadata.GetString(definition.Name), "Entry", StringComparison.Ordinal) Then
                            Dim visibility = definition.Attributes And TypeAttributes.VisibilityMask
                            If visibility = TypeAttributes.Public OrElse visibility = TypeAttributes.NotPublic Then
                                hasOfficialEntry = True
                                Exit For
                            End If
                        End If
                    Next

                    Dim hasExtReference As Boolean
                    Dim requiresV23 As Boolean
                    For Each handle In metadata.AssemblyReferences
                        Dim reference = metadata.GetAssemblyReference(handle)
                        If String.Equals(metadata.GetString(reference.Name), "FFmpegFreeUI.Ext.PluginSdk", StringComparison.OrdinalIgnoreCase) Then
                            hasExtReference = True
                            info.ExtSDK程序集版本 = reference.Version.ToString()
                        End If
                    Next

                    Dim v23Types = New HashSet(Of String)(StringComparer.Ordinal) From {
                        "IExtPluginParameterPanelCatalog", "ExtPluginParameterPageDescriptor", "ExtPluginParameterControlDescriptor",
                        "ExtFFmpegFreeUIParameterPanelIds", "IExtPluginCommandRegistry", "ExtPluginCommandParameterProvider",
                        "ExtPluginCommandStepProvider", "ExtPluginCommandContext", "ExtPluginCommandArgumentPosition",
                        "ExtFFmpegFreeUICommandPlaceholders", "ExtPluginCommandArgument", "ExtPluginCommandStepPlacement", "ExtPluginCommandStep"
                    }
                    For Each handle In metadata.TypeReferences
                        Dim reference = metadata.GetTypeReference(handle)
                        If v23Types.Contains(metadata.GetString(reference.Name)) Then
                            requiresV23 = True
                            Exit For
                        End If
                    Next
                    If Not requiresV23 Then
                        For Each handle In metadata.MemberReferences
                            Dim member = metadata.GetMemberReference(handle)
                            Dim memberName = metadata.GetString(member.Name)
                            If String.Equals(memberName, "ParameterPanel", StringComparison.Ordinal) OrElse
                               String.Equals(memberName, "Commands", StringComparison.Ordinal) Then
                                requiresV23 = True
                                Exit For
                            End If
                        Next
                    End If

                    If hasExtReference Then info.ExtAPI最低版本 = If(requiresV23, "v2.3（推断）", "v2.2（推断）")
                    If hasOfficialEntry AndAlso hasExtReference Then
                        info.接口类型 = 插件接口类型_v6.官方与Ext
                    ElseIf hasOfficialEntry Then
                        info.接口类型 = 插件接口类型_v6.官方API
                    ElseIf hasExtReference Then
                        info.接口类型 = 插件接口类型_v6.ExtAPI
                    Else
                        info.接口类型 = 插件接口类型_v6.未识别
                    End If
                End Using
            End Using
        Catch ex As Exception
            info.元数据错误 = ex.Message
            info.接口类型 = 插件接口类型_v6.未识别
        End Try
        Return info
    End Function

    Private Shared Function 加载单个插件(plugin As 插件信息_v6) As 插件加载结果
        If plugin Is Nothing Then Throw New ArgumentNullException(NameOf(plugin))
        If Not Ext插件扩展桥接_v2.可用 AndAlso
           (plugin.接口类型 = 插件接口类型_v6.ExtAPI OrElse plugin.接口类型 = 插件接口类型_v6.官方与Ext) Then
            Throw New InvalidOperationException("插件需要 Ext SDK 与 Ext PluginHost；请使用包含两个 Ext DLL 的发行包")
        End If

        Dim 程序集 = Assembly.LoadFrom(plugin.文件路径)
        Dim Entry类 As Type = 查找官方Entry类(程序集)
        Dim Entry类的实例 As Object = Nothing
        If Entry类 IsNot Nothing Then
            If Entry类.GetConstructor(Type.EmptyTypes) IsNot Nothing Then Entry类的实例 = Activator.CreateInstance(Entry类)
            注入宿主回调(Entry类, Entry类的实例, "SetHost_AddCustomWinformPanel", New Action(Of String, Control)(AddressOf 添加自定义Winform面板))
            注入宿主回调(Entry类, Entry类的实例, "SetHost_AddCustomWpfPanel", New Action(Of String, System.Windows.UIElement)(AddressOf 添加自定义Wpf面板))
            注入宿主回调(Entry类, Entry类的实例, "SetHost_AddMissionToQueueWithArgs", New Action(Of String, String, String, String)(AddressOf 使用命令行添加任务到编码队列))
            注入宿主回调(Entry类, Entry类的实例, "SetHost_AddMissionToQueueWith3fuiFile", New Action(Of String, String, String, String)(AddressOf 使用预设文件添加任务到编码队列))
            注入宿主回调(Entry类, Entry类的实例, "SetHost_MediaStreamVisualSelector", New Action(Of String, Object, Object, Object, String, String, String, String)(AddressOf 打开媒体流可视化选择器))
            注入宿主回调(Entry类, Entry类的实例, "SetHost_SubscribeQueueEvents", New Action(Of String, Object)(Sub(eventName, callback) 注册编码队列事件(plugin.插件键, eventName, callback)))
        End If

        Dim 已加载Ext插件 As Boolean
        If Ext插件扩展桥接_v2.可用 Then 已加载Ext插件 = Ext插件扩展桥接_v2.尝试加载Ext插件(程序集)
        If Entry类 Is Nothing Then
            If 已加载Ext插件 Then Return New 插件加载结果 With {.已加载 = True, .状态 = "已加载"}
            Throw New InvalidOperationException("程序集既不包含官方 Entry 类，也不包含 Ext 插件入口")
        End If

        Dim Entry方法 = Entry类.GetMethod("Entry", BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static)
        If Entry方法 Is Nothing Then
            If 已加载Ext插件 Then
                Return New 插件加载结果 With {.已加载 = True, .状态 = "部分加载", .错误 = "Ext 入口已加载，但官方 Entry 类缺少共享/静态 Entry 方法"}
            End If
            Throw New MissingMethodException("Entry 类缺少共享/静态 Entry 方法")
        End If

        Try
            Entry方法.Invoke(Nothing, Nothing)
        Catch ex As Exception When 已加载Ext插件
            Return New 插件加载结果 With {.已加载 = True, .状态 = "部分加载", .错误 = $"Ext 入口已加载，但官方 Entry 初始化失败：{获取异常消息(ex)}"}
        End Try
        Return New 插件加载结果 With {.已加载 = True, .状态 = "已加载"}
    End Function

    Private Shared Function 查找官方Entry类(程序集 As Assembly) As Type
        Dim 约定类型名 = 程序集.GetName.Name & ".Entry"
        Dim Entry类 = 程序集.GetType(约定类型名, throwOnError:=False, ignoreCase:=False)
        If Entry类 IsNot Nothing Then Return Entry类
        Dim 可加载类型 As IEnumerable(Of Type)
        Try
            可加载类型 = 程序集.GetTypes()
        Catch ex As ReflectionTypeLoadException
            可加载类型 = ex.Types.Where(Function(type) type IsNot Nothing).Cast(Of Type)()
        End Try
        Dim 候选类型 = 可加载类型.Where(Function(type) Not type.IsNested AndAlso String.Equals(type.Name, "Entry", StringComparison.Ordinal)).OrderBy(Function(type) type.FullName, StringComparer.Ordinal).ToList()
        If 候选类型.Count > 1 Then Throw New InvalidOperationException($"程序集 {程序集.GetName.Name} 包含多个 Entry 类：{String.Join("、", 候选类型.Select(Function(type) type.FullName))}")
        Return 候选类型.FirstOrDefault()
    End Function

    Private Shared Sub 注册编码队列事件(插件键 As String, 事件名称 As String, callback As Object)
        Dim action = TryCast(callback, Action(Of String, String))
        If action Is Nothing Then Exit Sub
        SyncLock 编码队列事件锁
            编码队列事件注册序号 += 1
            编码队列事件订阅列表.Add(New 编码队列事件订阅 With {
                .插件键 = 插件键,
                .筛选事件 = If(事件名称, "").Trim(),
                .注册序号 = 编码队列事件注册序号,
                .回调 = action
            })
        End SyncLock
    End Sub

    <CodeAnalysis.SuppressMessage("Style", "IDE0037:使用推断的成员名称", Justification:="<挂起>")>
    Private Shared Sub 编码队列事件处理(事件名称 As String, task As 编码任务_v6, log As 编码任务日志条目_v6)
        Dim data As String
        Try
            data = JsonSerializer.Serialize(New With {
                .eventName = 事件名称,
                .timestamp = DateTime.Now,
                .task = New With {
                    .id = task.ID, .name = task.任务名称, .inputPath = task.输入文件, .outputPath = task.输出文件,
                    .status = task.状态.ToString(), .statusCode = CInt(task.状态), .progress = task.进度?.百分比,
                    .progressText = task.进度?.进度文本, .currentStage = task.当前步骤?.显示名称,
                    .realtimeOutput = task.实时输出, .nonProgressOutput = task.非进度输出列表?.ToArray()
                },
                .log = If(log Is Nothing, Nothing, New With {
                    .sequence = log.序号, .time = log.时间, .text = log.文本, .category = log.类别.ToString(),
                    .categoryCode = CInt(log.类别), .isError = log.是否错误, .stage = log.阶段名
                })
            }, 编码队列事件序列化选项)
        Catch
            Exit Sub
        End Try

        Dim callbacks As List(Of 编码队列事件订阅)
        SyncLock 编码队列事件锁
            callbacks = 编码队列事件订阅列表.OrderBy(Function(item) 获取文件处理顺序(item.插件键)).ThenBy(Function(item) item.注册序号).ToList()
        End SyncLock
        For Each registration In callbacks
            If registration.筛选事件 <> "" AndAlso registration.筛选事件 <> "*" AndAlso Not String.Equals(registration.筛选事件, 事件名称, StringComparison.OrdinalIgnoreCase) Then Continue For
            Try
                registration.回调.Invoke(事件名称, data)
            Catch
                ' 插件回调异常不得中断后续插件或编码队列。
            End Try
        Next
    End Sub

    Private Shared Function 获取文件处理顺序(插件键 As String) As Integer
        SyncLock 插件状态锁
            Dim info As 插件信息_v6 = Nothing
            If 插件目录.TryGetValue(If(插件键, ""), info) Then Return info.处理顺序
        End SyncLock
        Return Integer.MaxValue \ 2
    End Function

    Private Shared Sub 注入宿主回调(entryType As Type, entryInstance As Object, methodName As String, callback As Object)
        Dim method = entryType.GetMethod(methodName, BindingFlags.Public Or BindingFlags.NonPublic Or BindingFlags.Static Or BindingFlags.Instance)
        If method Is Nothing Then Exit Sub
        Dim target = If(method.IsStatic, Nothing, entryInstance)
        If target Is Nothing AndAlso Not method.IsStatic Then Exit Sub
        method.Invoke(target, New Object() {callback})
    End Sub

    Public Shared Sub 添加自定义Winform面板(Name As String, C As Control)
        If String.IsNullOrWhiteSpace(Name) OrElse C Is Nothing Then Exit Sub
        UI线程执行(Sub()
                   Dim 标题 = Name.Trim()
                   由插件加载的自定义界面(标题) = C
                   FormMain_v6.添加插件选项卡(标题, C)
               End Sub)
    End Sub

    Public Shared Sub 添加自定义Wpf面板(Name As String, UIE As System.Windows.UIElement)
        If String.IsNullOrWhiteSpace(Name) OrElse UIE Is Nothing Then Exit Sub
        UI线程执行(Sub()
                   Dim 标题 = Name.Trim()
                   Dim host As New Integration.ElementHost With {.Child = UIE, .Dock = DockStyle.Fill}
                   由插件加载的自定义界面(标题) = host
                   FormMain_v6.添加插件选项卡(标题, host)
               End Sub)
    End Sub

    Public Shared Sub 使用命令行添加任务到编码队列(FFmpegArg As String, FileName As String, OutputPath As String, Optional InputPath As String = "")
        Dim taskName = If(String.IsNullOrWhiteSpace(FileName), $"插件命令行任务 {Now:HHmmss}", FileName)
        编码队列_v6.添加命令行任务(If(FFmpegArg, ""), taskName, If(OutputPath, ""), If(InputPath, ""))
    End Sub

    Public Shared Sub 使用预设文件添加任务到编码队列(File_3FUI_JsonPath As String, FileName As String, OutputPath As String, Optional InputPath As String = "")
        Dim preset = 启动参数响应_v6.读取预设数据(File_3FUI_JsonPath)
        If preset Is Nothing Then
            MsgBox("指定的 v6 预设文件不存在或无法读取", MsgBoxStyle.Critical)
            Exit Sub
        End If
        Dim input = If(InputPath, "")
        If input = "" AndAlso File.Exists(FileName) Then input = FileName
        Dim taskName = If(String.IsNullOrWhiteSpace(FileName), If(input <> "", Path.GetFileName(input), $"插件预设任务 {Now:HHmmss}"), FileName)
        编码队列_v6.添加预设任务(input, preset, taskName, If(OutputPath, ""))
    End Sub

    Public Shared Sub 打开媒体流可视化选择器(FilePath As String, VideoStreamTargetObject As Object, AudioStreamTargetObject As Object, SubtitleStreamTargetObject As Object, InputFileIndex As String, VideoStreamSelected As String, AudioStreamSelected As String, SubtitleStreamSelected As String)
        UI线程执行(Sub()
                   显示窗体(New Form_v6_媒体流选择器(要读取的媒体文件:=FilePath,
                    视频流文本目标对象:=VideoStreamTargetObject, 音频流文本目标对象:=AudioStreamTargetObject,
                    字幕流文本目标对象:=SubtitleStreamTargetObject, 文件索引:=InputFileIndex,
                    视频流已选:=VideoStreamSelected, 音频流已选:=AudioStreamSelected, 字幕流已选:=SubtitleStreamSelected), FormMain_v6)
               End Sub)
    End Sub

    Private Shared Function 获取异常消息(ex As Exception) As String
        Dim current = ex
        While TypeOf current Is TargetInvocationException AndAlso current.InnerException IsNot Nothing
            current = current.InnerException
        End While
        Return If(current.Message, current.GetType().Name)
    End Function

    Private Shared Sub 通知插件列表变化()
        Try
            RaiseEvent 插件列表已变化(Nothing, EventArgs.Empty)
        Catch
        End Try
    End Sub

    Private Shared Sub UI线程执行(action As Action)
        If action Is Nothing Then Exit Sub
        If FormMain_v6 IsNot Nothing AndAlso FormMain_v6.IsHandleCreated AndAlso Not FormMain_v6.IsDisposed Then
            If FormMain_v6.InvokeRequired Then
                FormMain_v6.BeginInvoke(action)
            Else
                action()
            End If
        Else
            action()
        End If
    End Sub

    Private NotInheritable Class 编码队列事件订阅
        Public Property 插件键 As String = ""
        Public Property 筛选事件 As String = ""
        Public Property 注册序号 As Long
        Public Property 回调 As Action(Of String, String)
    End Class

    Private NotInheritable Class 插件加载结果
        Public Property 已加载 As Boolean
        Public Property 状态 As String = "未加载"
        Public Property 错误 As String = ""
    End Class
End Class
