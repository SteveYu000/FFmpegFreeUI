using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace FFmpegFreeUI.Ext.PluginSdk;

/// <summary>FFmpegFreeUI v2 插件入口。每个插件程序集可以包含一个实现。</summary>
public interface IExtFFmpegFreeUIPlugin
{
    string Id { get; }
    string DisplayName { get; }
    void Initialize(IExtFFmpegFreeUIHost host);
}

/// <summary>FFmpegFreeUI 向插件开放的宿主能力。</summary>
public interface IExtFFmpegFreeUIHost
{
    Version ApiVersion { get; }
    string HostVersion { get; }
    IExtPluginUiRegistry Ui { get; }
    IExtPluginPipelineRegistry Pipeline { get; }
    IExtPluginBehaviorRegistry Behaviors { get; }
    IExtPluginResourceRegistry Resources { get; }
    /// <summary>v2.3：参数页、原生控件及稳定 UI 锚点目录。</summary>
    IExtPluginParameterPanelCatalog ParameterPanel { get; }
    /// <summary>v2.3：声明式 FFmpeg 参数和外部命令步骤注册表。</summary>
    IExtPluginCommandRegistry Commands { get; }
    void Log(ExtPluginLogLevel level, string message, Exception? exception = null);
}

/// <summary>参数面板当前公开的页面、原生控件及其稳定锚点目录。</summary>
public interface IExtPluginParameterPanelCatalog
{
    IReadOnlyCollection<ExtPluginParameterPageDescriptor> AvailablePages { get; }
    IReadOnlyCollection<ExtPluginParameterControlDescriptor> AvailableControls { get; }
}

public sealed class ExtPluginParameterPageDescriptor
{
    public ExtPluginParameterPageDescriptor(
        string pageId,
        string displayName,
        string topAnchorId,
        string bottomAnchorId)
    {
        PageId = pageId;
        DisplayName = displayName;
        TopAnchorId = topAnchorId;
        BottomAnchorId = bottomAnchorId;
    }

    public string PageId { get; }
    public string DisplayName { get; }
    public string TopAnchorId { get; }
    public string BottomAnchorId { get; }
}

public sealed class ExtPluginParameterControlDescriptor
{
    public ExtPluginParameterControlDescriptor(
        string controlId,
        string pageId,
        string controlPath,
        string controlName,
        string controlTypeName,
        string anchorId,
        string resourceId,
        string valuePropertyName)
    {
        ControlId = controlId;
        PageId = pageId;
        ControlPath = controlPath;
        ControlName = controlName;
        ControlTypeName = controlTypeName;
        AnchorId = anchorId;
        ResourceId = resourceId;
        ValuePropertyName = valuePropertyName;
    }

    public string ControlId { get; }
    public string PageId { get; }
    public string ControlPath { get; }
    public string ControlName { get; }
    public string ControlTypeName { get; }
    public string AnchorId { get; }
    public string ResourceId { get; }
    public string ValuePropertyName { get; }
}

/// <summary>参数页面和动态控件锚点的稳定 ID 规则。</summary>
public static class ExtFFmpegFreeUIParameterPanelIds
{
    public const string PageAnchorPrefix = "ext.parameters.page.";
    public const string ControlAnchorPrefix = "ext.parameters.control.";
    public const string ControlResourcePrefix = "ext.parameters.control-resource.";

    public static string PageTop(string pageId) =>
        PageAnchorPrefix + NormalizePageId(pageId) + ".top";

    public static string PageBottom(string pageId) =>
        PageAnchorPrefix + NormalizePageId(pageId) + ".bottom";

    public static string Control(string pageId, string escapedControlPath) =>
        ControlAnchorPrefix + NormalizePageId(pageId) + "." + RequireValue(escapedControlPath, nameof(escapedControlPath));

    public static string ControlResource(string controlIdOrAnchorId)
    {
        var value = RequireValue(controlIdOrAnchorId, nameof(controlIdOrAnchorId));
        if (value.StartsWith(ControlAnchorPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = value[ControlAnchorPrefix.Length..];
        }
        return ControlResourcePrefix + value;
    }

    private static string NormalizePageId(string pageId) =>
        RequireValue(pageId, nameof(pageId)).ToLowerInvariant();

    private static string RequireValue(string? value, string parameterName)
    {
        var result = (value ?? string.Empty).Trim();
        return result.Length == 0
            ? throw new ArgumentException("ID 不能为空", parameterName)
            : result;
    }
}

/// <summary>
/// 不引用 LakeUI 也能读写原生控件公共属性的兼容辅助器。调用方必须位于控件所属 UI 线程；
/// 宿主不承诺具体控件类型，只承诺目录中的控件 ID 和公开属性访问约定。
/// </summary>
public static class ExtPluginControlAccess
{
    public static string GetDefaultValuePropertyName(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        var type = control.GetType();
        if (FindProperty(type, "Checked", requireWrite: false)?.PropertyType == typeof(bool))
        {
            return "Checked";
        }
        if (type.Name.Contains("TrackBar", StringComparison.OrdinalIgnoreCase) &&
            FindProperty(type, "Value", requireWrite: false) is not null)
        {
            return "Value";
        }
        if (FindProperty(type, "Text", requireWrite: false) is not null)
        {
            return "Text";
        }
        if (FindProperty(type, "Value", requireWrite: false) is not null)
        {
            return "Value";
        }
        if (FindProperty(type, "SelectedIndex", requireWrite: false) is not null)
        {
            return "SelectedIndex";
        }
        return string.Empty;
    }

    public static bool TryGetValue(Control control, out object? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        var propertyName = GetDefaultValuePropertyName(control);
        return TryGetProperty(control, propertyName, out value);
    }

    public static bool TrySetValue(Control control, object? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        var propertyName = GetDefaultValuePropertyName(control);
        return TrySetProperty(control, propertyName, value);
    }

    public static bool TryGetProperty(Control control, string propertyName, out object? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        value = null;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var property = FindProperty(control.GetType(), propertyName, requireWrite: false);
        if (property is null || !property.CanRead)
        {
            return false;
        }

        try
        {
            value = property.GetValue(control);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    public static bool TrySetProperty(Control control, string propertyName, object? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var property = FindProperty(control.GetType(), propertyName, requireWrite: true);
        if (property is null)
        {
            return false;
        }

        try
        {
            property.SetValue(control, ConvertValue(value, property.PropertyType));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PropertyInfo? FindProperty(Type type, string propertyName, bool requireWrite)
    {
        var property = type.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property is null || property.GetIndexParameters().Length != 0 || !property.CanRead)
        {
            return null;
        }
        return requireWrite && !property.CanWrite ? null : property;
    }

    private static object? ConvertValue(object? value, Type destinationType)
    {
        var nullableType = Nullable.GetUnderlyingType(destinationType);
        var targetType = nullableType ?? destinationType;
        if (value is null)
        {
            if (destinationType.IsValueType && nullableType is null)
            {
                throw new InvalidCastException($"{destinationType.FullName} 不接受 null");
            }
            return null;
        }
        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }
        if (value is string text)
        {
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, text, ignoreCase: true);
            }
            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(typeof(string)))
            {
                return converter.ConvertFrom(null, CultureInfo.InvariantCulture, text);
            }
        }
        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }
}

public enum ExtPluginLogLevel
{
    Trace,
    Information,
    Warning,
    Error
}

/// <summary>在宿主定义的稳定 UI 锚点上注册控件或装饰逻辑。</summary>
public interface IExtPluginUiRegistry
{
    IReadOnlyCollection<string> AvailableAnchors { get; }
    IReadOnlyCollection<string> AvailableChoiceAnchors { get; }
    IDisposable Register(ExtPluginUiExtension extension);
    IDisposable RegisterChoice(ExtPluginUiChoiceExtension extension);
}

/// <summary>
/// 描述一项 UI 扩展。若要向锚点插槽插入界面，应为每个上下文返回新的 Control；
/// 若扩展只装饰 Context.AnchorControl，则返回 null。
/// </summary>
public sealed class ExtPluginUiExtension
{
    public ExtPluginUiExtension(string id, string anchorId, Func<IExtPluginUiContext, Control?> createControl)
    {
        Id = id;
        AnchorId = anchorId;
        CreateControl = createControl;
    }

    public string Id { get; set; }
    public string AnchorId { get; set; }
    public int Order { get; set; }
    public Func<IExtPluginUiContext, Control?> CreateControl { get; set; }

    /// <summary>
    /// 扩展注销或所属界面销毁时调用。装饰原生控件时应在这里移除事件处理器并恢复主动修改的属性；
    /// v2.2 插件未设置此成员时保持原有行为。
    /// </summary>
    public Action<IExtPluginUiContext>? Cleanup { get; set; }

    /// <summary>
    /// 默认值只插入或装饰；ReplaceAnchor 会隐藏原生控件并在同一布局位置放入插件控件。
    /// 替换模式必须同时声明 ResourceId 和 Exclusive 访问。
    /// </summary>
    public ExtPluginUiExtensionMode Mode { get; set; }
    public string? ResourceId { get; set; }
    public ExtPluginResourceAccess ResourceAccess { get; set; } = ExtPluginResourceAccess.Observe;
}

public enum ExtPluginUiExtensionMode
{
    Default,
    ReplaceAnchor
}

/// <summary>某个插件扩展在一个原生 UI 界面实例中的独立上下文。</summary>
public interface IExtPluginUiContext
{
    string PluginId { get; }
    string ExtensionId { get; }
    string AnchorId { get; }
    string SurfaceId { get; }

    /// <summary>
    /// 锚点所标识的原生控件。这是为兼容旧插件保留的高级逃生口；直接修改原生控件前，
    /// 插件应声明对应的资源租约，并负责在注销时完整还原修改。
    /// 新插件应优先使用 RegisterChoice 或插入型锚点。
    /// </summary>
    Control AnchorControl { get; }

    /// <summary>宿主创建的插入容器；仅支持装饰的锚点为 null。</summary>
    Control? ContainerControl { get; }

    /// <summary>
    /// 返回同一 UI 界面中另一个已注册锚点的原生控件；该锚点不可用时返回 null。
    /// 扩展可借此协调相邻控件，无需反射或依赖宿主的私有控件层级。
    /// </summary>
    Control? GetAnchorControl(string anchorId);

    /// <summary>随当前 v6 预设持久化、由插件自行解释的 JSON。</summary>
    string StateJson { get; set; }

    /// <summary>将另一个预设的插件状态恢复到当前界面后触发。</summary>
    event EventHandler? StateRestored;

    void RequestParameterRefresh();
}

/// <summary>
/// 由宿主管理的原生下拉框扩展项。宿主负责稳定排序、预设回退、状态恢复和注销清理，
/// 插件不应再直接修改原生 Items 集合。
/// </summary>
public sealed class ExtPluginUiChoiceExtension
{
    public ExtPluginUiChoiceExtension(
        string id,
        string anchorId,
        string choiceId,
        string displayText,
        string nativeFallbackChoiceId)
    {
        Id = id;
        AnchorId = anchorId;
        ChoiceId = choiceId;
        DisplayText = displayText;
        NativeFallbackChoiceId = nativeFallbackChoiceId;
    }

    public string Id { get; set; }
    public string AnchorId { get; set; }
    public string ChoiceId { get; set; }
    public string DisplayText { get; set; }

    /// <summary>
    /// 此扩展项在原生预设和原生联动逻辑中对应的选项 ID。
    /// 例如自动计算 CRF 的扩展项可回退到 VideoQualityCrf。
    /// </summary>
    public string NativeFallbackChoiceId { get; set; }

    public int Order { get; set; }

    /// <summary>选中扩展项后，由宿主写入其他稳定锚点 Text 属性的值。</summary>
    public IDictionary<string, string?> ValueOverrides { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>选中扩展项期间由宿主临时禁用的稳定锚点；离开后恢复原启用状态。</summary>
    public ICollection<string> DisabledAnchors { get; } = new List<string>();

    /// <summary>恢复预设插件状态时，返回 true 表示应重新选中此扩展项。</summary>
    public Func<IExtPluginUiChoiceContext, bool>? RestoreSelection { get; set; }

    /// <summary>用户选择或离开此扩展项时触发，适合保存插件自己的启用状态。</summary>
    public Action<IExtPluginUiChoiceContext, bool>? SelectionChanged { get; set; }
}

/// <summary>安全下拉项的上下文；不暴露原生控件。</summary>
public interface IExtPluginUiChoiceContext
{
    string PluginId { get; }
    string ExtensionId { get; }
    string AnchorId { get; }
    string ChoiceId { get; }
    string SurfaceId { get; }
    bool IsSelected { get; }
    string StateJson { get; set; }
    void RequestParameterRefresh();
}

/// <summary>在 FFmpegFreeUI 参数处理管线的稳定阶段注册有序处理器。</summary>
public interface IExtPluginPipelineRegistry
{
    IReadOnlyCollection<string> AvailableStages { get; }
    IDisposable Register(ExtPluginPipelineHandler handler);
}

/// <summary>
/// 声明式命令注册表。参数提供器向原生 FFmpeg 命令的稳定位置贡献参数；
/// 步骤提供器向同一命令计划贡献可预览、可取消并由队列统一执行的外部进程步骤。
/// </summary>
public interface IExtPluginCommandRegistry
{
    IDisposable RegisterParameterProvider(ExtPluginCommandParameterProvider provider);
    IDisposable RegisterStepProvider(ExtPluginCommandStepProvider provider);
}

public delegate void ExtPluginCommandParameterCallback(ExtPluginCommandContext context);
public delegate void ExtPluginCommandStepCallback(ExtPluginCommandContext context);

public sealed class ExtPluginCommandParameterProvider
{
    public ExtPluginCommandParameterProvider(string id, ExtPluginCommandParameterCallback callback)
    {
        Id = id;
        Callback = callback;
    }

    public string Id { get; set; }
    public int Order { get; set; }
    public ExtPluginCommandParameterCallback Callback { get; set; }
}

public sealed class ExtPluginCommandStepProvider
{
    public ExtPluginCommandStepProvider(string id, ExtPluginCommandStepCallback callback)
    {
        Id = id;
        Callback = callback;
    }

    public string Id { get; set; }
    public int Order { get; set; }
    public ExtPluginCommandStepCallback Callback { get; set; }
}

/// <summary>命令提供器每次解析时得到的只读输入和输出集合。</summary>
public sealed class ExtPluginCommandContext
{
    public string PluginId { get; set; } = string.Empty;
    public string PresetJson { get; set; } = string.Empty;
    public string PluginStateJson { get; set; } = "{}";
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public bool IsPreview { get; set; }
    public IDictionary<string, string?> Properties { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public IList<ExtPluginCommandArgument> Arguments { get; } = new List<ExtPluginCommandArgument>();
    public IList<ExtPluginCommandStep> Steps { get; } = new List<ExtPluginCommandStep>();
}

/// <summary>插件参数相对于 FFmpeg 原生命令结构的插入位置。</summary>
public enum ExtPluginCommandArgumentPosition
{
    /// <summary>位于 -hide_banner 之前；只适合 FFmpeg 全局选项。</summary>
    Global,
    /// <summary>位于原生输入选项之后、主输入 -i 之前。</summary>
    BeforeInput,
    /// <summary>位于全部输入之后、原生输出选项之前。</summary>
    AfterInput,
    /// <summary>位于原生输出选项之后、输出目标之前。</summary>
    BeforeOutput,
    /// <summary>位于输出目标之后。</summary>
    AfterOutput
}

/// <summary>
/// “完全自己写”命令模板中的可选插槽。结构化命令不需要这些标记；模板未写标记时，
/// 宿主会为兼容旧预设按可推断位置插入，但新模板应显式保留需要的位置。
/// </summary>
public static class ExtFFmpegFreeUICommandPlaceholders
{
    public const string Global = "<ext:global>";
    public const string BeforeInput = "<ext:before-input>";
    public const string AfterInput = "<ext:after-input>";
    public const string BeforeOutput = "<ext:before-output>";
    public const string AfterOutput = "<ext:after-output>";
}

public sealed class ExtPluginCommandArgument
{
    public ExtPluginCommandArgument(ExtPluginCommandArgumentPosition position, string text)
    {
        Position = position;
        Text = text;
    }

    public ExtPluginCommandArgumentPosition Position { get; set; }
    public string Text { get; set; }
    public int Order { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum ExtPluginCommandStepPlacement
{
    BeforeNative,
    AfterNative
}

/// <summary>
/// 插件贡献的独立进程步骤。宿主使用 UseShellExecute=false 启动，不经过 FFmpeg 的全局参数包装；
/// 标准输出、标准错误、非零退出码、暂停/停止和取消均由编码队列统一管理。
/// </summary>
public sealed class ExtPluginCommandStep
{
    public ExtPluginCommandStep(string id, string displayName, string processFileName, string arguments)
    {
        Id = id;
        DisplayName = displayName;
        ProcessFileName = processFileName;
        Arguments = arguments;
    }

    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string ProcessFileName { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
    public ExtPluginCommandStepPlacement Placement { get; set; }
    public int Order { get; set; }
    public bool IncludeInPreview { get; set; } = true;
    public bool ParseFFmpegProgress { get; set; }
}

/// <summary>注册原生稳定行为点前后或替换处理器。</summary>
public interface IExtPluginBehaviorRegistry
{
    IReadOnlyCollection<string> AvailableBehaviors { get; }
    IDisposable Register(ExtPluginBehaviorHandler handler);
}

public enum ExtPluginBehaviorPhase
{
    BeforeNative,
    AfterNative,
    ReplaceNative
}

public delegate void ExtPluginBehaviorCallback(ExtPluginBehaviorContext context);

public sealed class ExtPluginBehaviorHandler
{
    public ExtPluginBehaviorHandler(
        string id,
        string behaviorId,
        ExtPluginBehaviorPhase phase,
        ExtPluginBehaviorCallback callback)
    {
        Id = id;
        BehaviorId = behaviorId;
        Phase = phase;
        Callback = callback;
    }

    public string Id { get; set; }
    public string BehaviorId { get; set; }
    public ExtPluginBehaviorPhase Phase { get; set; }
    public int Order { get; set; }
    public ExtPluginBehaviorCallback Callback { get; set; }
}

/// <summary>行为点的可变键值上下文；每个行为点在文档中定义自己的稳定键。</summary>
public sealed class ExtPluginBehaviorContext
{
    public string BehaviorId { get; set; } = string.Empty;
    public string SurfaceId { get; set; } = string.Empty;
    public IDictionary<string, string?> Properties { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 对宿主稳定资源的修改声明。它不代替 UI 或处理器注册，而是在插件加载时协调深度修改冲突。
/// </summary>
public interface IExtPluginResourceRegistry
{
    IReadOnlyCollection<string> AvailableResources { get; }
    IDisposable Claim(ExtPluginResourceClaim claim);
}

public enum ExtPluginResourceAccess
{
    /// <summary>只读取或监听；可与其他声明共存。</summary>
    Observe,
    /// <summary>按 UI 扩展或处理器的 Order 依次变换；可与其他有序变换共存。</summary>
    OrderedTransform,
    /// <summary>独占接管；同一资源上不能再有其他有序变换或独占接管。</summary>
    Exclusive
}

public sealed class ExtPluginResourceClaim
{
    public ExtPluginResourceClaim(string id, string resourceId, ExtPluginResourceAccess access)
    {
        Id = id;
        ResourceId = resourceId;
        Access = access;
    }

    public string Id { get; set; }
    public string ResourceId { get; set; }
    public ExtPluginResourceAccess Access { get; set; }
    public string Purpose { get; set; } = string.Empty;
}

public delegate ValueTask ExtPluginPipelineCallback(
    ExtPluginPipelineContext context,
    CancellationToken cancellationToken);

public sealed class ExtPluginPipelineHandler
{
    public ExtPluginPipelineHandler(string id, string stageId, ExtPluginPipelineCallback callback)
    {
        Id = id;
        StageId = stageId;
        Callback = callback;
    }

    public string Id { get; set; }
    public string StageId { get; set; }
    public int Order { get; set; }
    public ExtPluginPipelineCallback Callback { get; set; }

    /// <summary>
    /// 可选的冲突资源声明。普通可组合处理器可留空；会改写共享数据或要求独占语义时应填写。
    /// </summary>
    public string? ResourceId { get; set; }
    public ExtPluginResourceAccess ResourceAccess { get; set; } = ExtPluginResourceAccess.OrderedTransform;
}

/// <summary>
/// 在管线阶段间传递的可变数据。PresetJson 是完整的 v6 预设；插件替换它时，
/// 应保留不属于本插件的字段。
/// </summary>
public sealed class ExtPluginPipelineContext
{
    private readonly Action<ExtPluginPipelineProgress>? _progressSink;
    private readonly Action<ExtPluginTaskResult>? _resultSink;

    public ExtPluginPipelineContext(Action<ExtPluginPipelineProgress>? progressSink = null)
        : this(progressSink, null)
    {
    }

    /// <summary>
    /// 供宿主创建带进度和结构化结果接收器的上下文。保留单参数构造函数以兼容既有插件。
    /// </summary>
    public ExtPluginPipelineContext(
        Action<ExtPluginPipelineProgress>? progressSink,
        Action<ExtPluginTaskResult>? resultSink)
    {
        _progressSink = progressSink;
        _resultSink = resultSink;
    }

    public string StageId { get; set; } = string.Empty;
    public string PresetJson { get; set; } = string.Empty;
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string CommandLine { get; set; } = string.Empty;
    public string ProcessFileName { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string SurfaceId { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public bool IsPreview { get; set; }
    public int? ExitCode { get; set; }
    /// <summary>宿主提供的当前任务状态。插件赋值不会改变 FFmpegFreeUI 的真实任务状态。</summary>
    public ExtPluginTaskStatus TaskStatus { get; set; }
    public IDictionary<string, string?> Properties { get; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    public void ReportProgress(string message, double? fraction = null) =>
        _progressSink?.Invoke(new ExtPluginPipelineProgress(message, fraction));

    /// <summary>
    /// 为当前编码任务发布一个可覆盖的结构化结果。同一插件使用相同 key 再次上报时更新原结果。
    /// 结果会写入任务日志；没有实际任务接收器的预览或预设阶段会忽略本次上报。
    /// </summary>
    public void ReportResult(
        string key,
        string value,
        string? displayName = null,
        string? unit = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("结果 key 不能为空", nameof(key));
        }

        _resultSink?.Invoke(new ExtPluginTaskResult(
            key.Trim(),
            value ?? string.Empty,
            string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            string.IsNullOrWhiteSpace(unit) ? null : unit.Trim()));
    }
}

public sealed record ExtPluginPipelineProgress(string Message, double? Fraction = null);

/// <summary>插件向当前编码任务发布的结构化结果。</summary>
public sealed record ExtPluginTaskResult(
    string Key,
    string Value,
    string? DisplayName = null,
    string? Unit = null);

/// <summary>当前任务在插件处理链中的生命周期状态。</summary>
public enum ExtPluginTaskStatus
{
    Unknown,
    Pending,
    Running,
    Paused,
    Succeeded,
    Failed,
    Canceled
}

/// <summary>稳定的 API 与发现机制常量。</summary>
public static class ExtFFmpegFreeUIPluginApi
{
    public static Version Version { get; } = new(2, 3, 0);
}

/// <summary>宿主当前提供的 UI 锚点 ID。</summary>
public static class ExtFFmpegFreeUIUiAnchors
{
    public const string ParametersVideoQualityMode = "ext.parameters.video.quality.mode";
    public const string ParametersVideoQualityParameterName = "ext.parameters.video.quality.parameter-name";
    public const string ParametersVideoQualityValue = "ext.parameters.video.quality.value";
    public const string ParametersVideoQualityAfterGlobal = "ext.parameters.video.quality.global.after";
    public const string ParametersVideoQualityBeforeAdvanced = "ext.parameters.video.quality.advanced.before";
    public const string ParametersVideoQualityPageBottom = "ext.parameters.video.quality.page.bottom";

    public static IReadOnlyCollection<string> All { get; } = new ReadOnlyCollection<string>(
        new[]
        {
            ParametersVideoQualityMode,
            ParametersVideoQualityParameterName,
            ParametersVideoQualityValue,
            ParametersVideoQualityAfterGlobal,
            ParametersVideoQualityBeforeAdvanced,
            ParametersVideoQualityPageBottom
        });
}

/// <summary>支持宿主管理下拉项的 UI 锚点。</summary>
public static class ExtFFmpegFreeUIUiChoiceAnchors
{
    public static IReadOnlyCollection<string> All { get; } = new ReadOnlyCollection<string>(
        new[]
        {
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode
        });
}

/// <summary>原生下拉项的稳定语义 ID；插件不得依赖 SelectedIndex。</summary>
public static class ExtFFmpegFreeUIUiChoices
{
    public const string VideoQualityNone = "ext.video-quality.none";
    public const string VideoQualityCrf = "ext.video-quality.crf";
    public const string VideoQualityVbr = "ext.video-quality.vbr";
    public const string VideoQualityCqp = "ext.video-quality.cqp";
    public const string VideoQualityCbr = "ext.video-quality.cbr";
    public const string VideoQualityTpe = "ext.video-quality.tpe";

    public static IReadOnlyCollection<string> All { get; } = new ReadOnlyCollection<string>(
        new[]
        {
            VideoQualityNone,
            VideoQualityCrf,
            VideoQualityVbr,
            VideoQualityCqp,
            VideoQualityCbr,
            VideoQualityTpe
        });
}

/// <summary>
/// 可声明冲突关系的稳定资源。安全组合 API 不需要声明；直接修改原生控件或接管原生逻辑时需要声明。
/// </summary>
public static class ExtFFmpegFreeUIPluginResources
{
    public const string ParametersVideoQualityModeControl = "ext.parameters.video.quality.mode.control";
    public const string ParametersVideoQualityModeItems = "ext.parameters.video.quality.mode.items";
    public const string ParametersVideoQualityModeBehavior = "ext.parameters.video.quality.mode.behavior";
    public const string ParametersVideoQualityFields = "ext.parameters.video.quality.fields";
    public const string PresetDocument = "ext.preset.document";
    public const string CommandLine = "ext.command.line";
    public const string CommandArguments = "ext.command.arguments";
    public const string CommandPlan = "ext.command.plan";
    public const string TaskAfterProcessing = "ext.task.after-processing";

    public static IReadOnlyCollection<string> All { get; } = new ReadOnlyCollection<string>(
        new[]
        {
            ParametersVideoQualityModeControl,
            ParametersVideoQualityModeItems,
            ParametersVideoQualityModeBehavior,
            ParametersVideoQualityFields,
            PresetDocument,
            CommandLine,
            CommandArguments,
            CommandPlan,
            TaskAfterProcessing
        });
}

/// <summary>可被有序调整或独占替换的原生稳定行为点。</summary>
public static class ExtFFmpegFreeUIBehaviors
{
    public const string ParametersVideoQualityModeChanged = "ext.parameters.video.quality.mode.changed";

    public static IReadOnlyCollection<string> All { get; } = new ReadOnlyCollection<string>(
        new[]
        {
            ParametersVideoQualityModeChanged
        });
}

/// <summary>
/// 管线阶段 ID。预设、队列和命令构建阶段同步执行；任务与进程阶段异步执行，
/// 并接收取消令牌。
/// </summary>
public static class ExtFFmpegFreeUIPipelineStages
{
    public const string PresetBeforeCapture = "ext.preset.before-capture";
    public const string PresetAfterCapture = "ext.preset.after-capture";
    public const string PresetBeforeApply = "ext.preset.before-apply";
    public const string PresetAfterApply = "ext.preset.after-apply";
    public const string QueueBeforeAdd = "ext.queue.before-add";
    public const string TaskBeforePrepare = "ext.task.before-prepare";
    public const string TaskAfterPrepare = "ext.task.after-prepare";
    public const string CommandBeforeBuild = "ext.command.before-build";
    public const string CommandAfterBuild = "ext.command.after-build";
    public const string ProcessBeforeStart = "ext.process.before-start";
    public const string ProcessAfterExit = "ext.process.after-exit";
    /// <summary>全部原生及声明式插件步骤成功后、任务标记完成前执行一次；适合可取消的成功后处理。</summary>
    public const string TaskAfterComplete = "ext.task.after-complete";
    /// <summary>任务确定失败后执行一次；用户取消不触发。</summary>
    public const string TaskAfterFailed = "ext.task.after-failed";
    /// <summary>任务成功、失败或取消后均执行一次；适合有界清理。</summary>
    public const string TaskAfterFinish = "ext.task.after-finish";

    public static IReadOnlyCollection<string> All { get; } = new ReadOnlyCollection<string>(
        new[]
        {
            PresetBeforeCapture,
            PresetAfterCapture,
            PresetBeforeApply,
            PresetAfterApply,
            QueueBeforeAdd,
            TaskBeforePrepare,
            TaskAfterPrepare,
            CommandBeforeBuild,
            CommandAfterBuild,
            ProcessBeforeStart,
            ProcessAfterExit,
            TaskAfterComplete,
            TaskAfterFailed,
            TaskAfterFinish
        });
}
