using System.Collections.Concurrent;
using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FFmpegFreeUI.Ext.PluginSdk;

namespace FFmpegFreeUI.Ext.PluginApi.Sample;

/// <summary>
/// C# 综合示例：以“自动质量策略、命令审计、输出校验”为应用场景，展示 Ext Plugin API v2.3
/// 的参数面板目录、声明式命令计划、兼容 UI 锚点和处理阶段。所有会改变任务的选项默认关闭。
/// </summary>
public sealed partial class SamplePlugin : IExtFFmpegFreeUIPlugin
{
    internal const string PluginId = "sample.csharp-complete-api";
    internal const string PluginTag = "[C# API 示例]";
    private static readonly Version RequiredApiVersion = ExtFFmpegFreeUIPluginApi.Version;

    private readonly List<IDisposable> _registrations = new();
    private readonly ConcurrentDictionary<string, TaskSession> _taskSessions =
        new(StringComparer.OrdinalIgnoreCase);
    private IExtFFmpegFreeUIHost? _host;

    public string Id => PluginId;
    public string DisplayName => "C# Ext Plugin API 综合示例";

    public void Initialize(IExtFFmpegFreeUIHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (host.ApiVersion < RequiredApiVersion)
        {
            throw new NotSupportedException(
                $"本示例需要 Ext Plugin API {RequiredApiVersion} 或更高版本，当前为 {host.ApiVersion}");
        }

        _host = host;
        if (host is not IExtFFmpegFreeUIHostV23 extendedHost)
        {
            throw new NotSupportedException("宿主声明支持 API 2.3，但没有提供 IExtFFmpegFreeUIHostV23");
        }
        host.Log(
            ExtPluginLogLevel.Information,
            $"正在初始化 {DisplayName}；API={host.ApiVersion}，FFmpegFreeUI={host.HostVersion}");

        RegisterUiExtensions(host);
        RegisterSafeChoiceAndBehavior(host);
        RegisterPipelineHandlers(host);
        RegisterV23Extensions(extendedHost);

        // AvailableAnchors / AvailableStages 可用于兼容较旧宿主；不要反射查找宿主私有控件或方法。
        var missingAnchors = ExtFFmpegFreeUIUiAnchors.All.Except(
            host.Ui.AvailableAnchors,
            StringComparer.OrdinalIgnoreCase);
        var missingStages = ExtFFmpegFreeUIPipelineStages.All.Except(
            host.Pipeline.AvailableStages,
            StringComparer.OrdinalIgnoreCase);
        foreach (var anchor in missingAnchors)
        {
            host.Log(ExtPluginLogLevel.Warning, $"宿主未提供 UI 锚点：{anchor}");
        }
        foreach (var stage in missingStages)
        {
            host.Log(ExtPluginLogLevel.Warning, $"宿主未提供处理阶段：{stage}");
        }

        host.Log(ExtPluginLogLevel.Trace, $"已保存 {_registrations.Count} 个可释放的注册句柄");
    }

    private void RegisterSafeChoiceAndBehavior(IExtFFmpegFreeUIHost host)
    {
        if (host.Ui.AvailableChoiceAnchors.Contains(
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode,
            StringComparer.OrdinalIgnoreCase))
        {
            var choice = new ExtPluginUiChoiceExtension(
                "automatic-crf-choice",
                ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode,
                PluginId + ".automatic-crf",
                "示例：自动 CRF",
                ExtFFmpegFreeUIUiChoices.VideoQualityCrf)
            {
                Order = 100,
                RestoreSelection = context => DeserializeState(context.StateJson).Enabled,
                SelectionChanged = (context, selected) =>
                {
                    var state = DeserializeState(context.StateJson);
                    state.Enabled = selected;
                    context.StateJson = JsonSerializer.Serialize(state);
                }
            };
            choice.ValueOverrides[ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityParameterName] = "-crf";
            choice.ValueOverrides[ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityValue] = string.Empty;
            choice.DisabledAnchors.Add(ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityValue);
            _registrations.Add(host.Ui.RegisterChoice(choice));
        }

        if (host.Behaviors.AvailableBehaviors.Contains(
            ExtFFmpegFreeUIBehaviors.ParametersVideoQualityModeChanged,
            StringComparer.OrdinalIgnoreCase))
        {
            // AfterNative 会和其他插件按 Order 组合；ReplaceNative 则要求独占，不要在普通插件中滥用。
            _registrations.Add(host.Behaviors.Register(new ExtPluginBehaviorHandler(
                "trace-quality-mode",
                ExtFFmpegFreeUIBehaviors.ParametersVideoQualityModeChanged,
                ExtPluginBehaviorPhase.AfterNative,
                context =>
                {
                    context.Properties.TryGetValue("selectedChoiceId", out var selectedChoiceId);
                    Log(ExtPluginLogLevel.Trace, $"稳定质量选项={selectedChoiceId}");
                })
            {
                Order = 100
            }));
        }

        // 示例按钮会写原生模式和字段，因此显式声明 OrderedTransform；只读监听可用 Observe。
        _registrations.Add(host.Resources.Claim(new ExtPluginResourceClaim(
            "change-native-quality-mode",
            ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeControl,
            ExtPluginResourceAccess.OrderedTransform)
        {
            Purpose = "示例按钮修改原生质量模式"
        }));
        _registrations.Add(host.Resources.Claim(new ExtPluginResourceClaim(
            "change-native-quality-fields",
            ExtFFmpegFreeUIPluginResources.ParametersVideoQualityFields,
            ExtPluginResourceAccess.OrderedTransform)
        {
            Purpose = "示例按钮修改质量参数名和值"
        }));
    }

    private void RegisterUiExtensions(IExtFFmpegFreeUIHost host)
    {
        RegisterUiIfAvailable(
            host,
            "decorate-quality-mode",
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode,
            CreateQualityModeDecoration,
            10);
        RegisterUiIfAvailable(
            host,
            "decorate-parameter-name",
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityParameterName,
            CreateParameterNameDecoration,
            20);
        RegisterUiIfAvailable(
            host,
            "decorate-quality-value",
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityValue,
            CreateQualityValueDecoration,
            30);
        RegisterUiIfAvailable(
            host,
            "quality-policy-row",
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityAfterGlobal,
            CreateQualityPolicyRow,
            100);
        RegisterUiIfAvailable(
            host,
            "command-options-row",
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityBeforeAdvanced,
            CreateCommandOptionsRow,
            200);
        RegisterUiIfAvailable(
            host,
            "post-process-row",
            ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityPageBottom,
            CreatePostProcessRow,
            300);
    }

    private void RegisterUiIfAvailable(
        IExtFFmpegFreeUIHost host,
        string id,
        string anchorId,
        Func<IExtPluginUiContext, Control?> factory,
        int order)
    {
        if (!host.Ui.AvailableAnchors.Contains(anchorId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _registrations.Add(host.Ui.Register(new ExtPluginUiExtension(id, anchorId, factory)
        {
            Order = order
        }));
    }

    private void RegisterPipelineHandlers(IExtFFmpegFreeUIHost host)
    {
        RegisterStage(host, "migrate-state", ExtFFmpegFreeUIPipelineStages.PresetBeforeApply, PresetBeforeApply, -200);
        RegisterStage(host, "observe-applied-preset", ExtFFmpegFreeUIPipelineStages.PresetAfterApply, PresetAfterApply, 200);
        RegisterStage(host, "mark-capture", ExtFFmpegFreeUIPipelineStages.PresetBeforeCapture, PresetBeforeCapture, -100);
        RegisterStage(host, "normalize-captured-preset", ExtFFmpegFreeUIPipelineStages.PresetAfterCapture, PresetAfterCapture, 100);
        RegisterStage(host, "name-queued-task", ExtFFmpegFreeUIPipelineStages.QueueBeforeAdd, QueueBeforeAdd, 100);
        RegisterStage(host, "analyze-task", ExtFFmpegFreeUIPipelineStages.TaskBeforePrepare, TaskBeforePrepareAsync, 100);
        RegisterStage(host, "adjust-structured-command", ExtFFmpegFreeUIPipelineStages.CommandBeforeBuild, CommandBeforeBuild, 100);
        RegisterStage(host, "adjust-final-command", ExtFFmpegFreeUIPipelineStages.CommandAfterBuild, CommandAfterBuild, 100);
        RegisterStage(host, "validate-prepared-task", ExtFFmpegFreeUIPipelineStages.TaskAfterPrepare, TaskAfterPrepareAsync, 100);
        RegisterStage(host, "configure-process", ExtFFmpegFreeUIPipelineStages.ProcessBeforeStart, ProcessBeforeStartAsync, 100);
        RegisterStage(host, "observe-process", ExtFFmpegFreeUIPipelineStages.ProcessAfterExit, ProcessAfterExitAsync, 100);
        RegisterStage(host, "hash-output", ExtFFmpegFreeUIPipelineStages.TaskAfterComplete, TaskAfterCompleteAsync, 100);
        RegisterStage(host, "report-failure", ExtFFmpegFreeUIPipelineStages.TaskAfterFailed, TaskAfterFailedAsync, 100);
        RegisterStage(host, "release-task-cache", ExtFFmpegFreeUIPipelineStages.TaskAfterFinish, TaskAfterFinishAsync, 100);
    }

    private void RegisterStage(
        IExtFFmpegFreeUIHost host,
        string id,
        string stageId,
        ExtPluginPipelineCallback callback,
        int order)
    {
        if (!host.Pipeline.AvailableStages.Contains(stageId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _registrations.Add(host.Pipeline.Register(new ExtPluginPipelineHandler(id, stageId, callback)
        {
            Order = order
        }));
    }

    internal void Log(ExtPluginLogLevel level, string message, Exception? exception = null) =>
        _host?.Log(level, message, exception);

    // 调用示例：Log(ExtPluginLogLevel.Error, "处理失败", exception);

    internal TaskSession GetOrCreateSession(ExtPluginPipelineContext context)
    {
        var taskId = string.IsNullOrWhiteSpace(context.TaskId)
            ? $"preview:{context.SurfaceId}"
            : context.TaskId;
        return _taskSessions.GetOrAdd(taskId, _ => new TaskSession());
    }

    internal bool TryRemoveSession(string taskId, out TaskSession? session)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            session = null;
            return false;
        }

        return _taskSessions.TryRemove(taskId, out session);
    }

    internal sealed class TaskSession
    {
        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
        public string InputPath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public ConcurrentQueue<int> ExitCodes { get; } = new();
    }

    internal sealed class SampleState
    {
        public int Version { get; set; } = 2;
        public bool Enabled { get; set; }
        public int Crf { get; set; } = 32;
        public bool PrefixTaskName { get; set; }
        public string OutputSuffix { get; set; } = string.Empty;
        public string AdvancedArguments { get; set; } = string.Empty;
        public bool AddNoStats { get; set; }
        public bool AddNoStdin { get; set; }
        public string ProcessOverride { get; set; } = string.Empty;
        public bool AcceptExitCodeOne { get; set; }
        public bool ComputeSha256 { get; set; }
        public bool AddDeclarativeMetadata { get; set; }
        public bool RunDeclarativeCommandStep { get; set; }
        public string LastSurfaceId { get; set; } = string.Empty;
    }

    internal static SampleState DeserializeState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SampleState();
        }

        try
        {
            return JsonSerializer.Deserialize<SampleState>(json) ?? new SampleState();
        }
        catch (JsonException)
        {
            return new SampleState();
        }
    }

    internal static (JsonObject Preset, SampleState State) ReadPresetAndState(string presetJson)
    {
        var preset = JsonNode.Parse(presetJson)?.AsObject()
            ?? throw new InvalidOperationException("宿主没有提供有效的预设 JSON 对象");
        var extensionData = preset["插件扩展数据"] as JsonObject;
        string? stateJson = null;
        if (extensionData?[PluginId] is JsonValue stateValue &&
            stateValue.TryGetValue<string>(out var value))
        {
            stateJson = value;
        }

        return (preset, DeserializeState(stateJson));
    }

    internal static void WriteState(JsonObject preset, SampleState state)
    {
        var extensionData = preset["插件扩展数据"] as JsonObject;
        if (extensionData is null)
        {
            extensionData = new JsonObject();
            preset["插件扩展数据"] = extensionData;
        }

        extensionData[PluginId] = JsonSerializer.Serialize(state);
    }

    internal static void WritePreset(ExtPluginPipelineContext context, JsonObject preset) =>
        context.PresetJson = preset.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    internal static bool IsActive(SampleState? state) => state is not null &&
        (state.Enabled ||
         state.PrefixTaskName ||
         !string.IsNullOrWhiteSpace(state.OutputSuffix) ||
         !string.IsNullOrWhiteSpace(state.AdvancedArguments) ||
         state.AddNoStats ||
         state.AddNoStdin ||
         !string.IsNullOrWhiteSpace(state.ProcessOverride) ||
         state.AcceptExitCodeOne ||
         state.ComputeSha256 ||
         state.AddDeclarativeMetadata ||
         state.RunDeclarativeCommandStep);
}
