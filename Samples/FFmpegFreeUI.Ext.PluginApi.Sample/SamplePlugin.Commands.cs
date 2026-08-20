using System.Collections.Concurrent;
using FFmpegFreeUI.Ext.PluginSdk;

namespace FFmpegFreeUI.Ext.PluginApi.Sample;

public sealed partial class SamplePlugin
{
    private readonly ConcurrentDictionary<string, Action> _v23UiCleanup =
        new(StringComparer.OrdinalIgnoreCase);

    private void RegisterV23Extensions(IExtFFmpegFreeUIHostV23 host)
    {
        var audioPage = host.ParameterPanel.AvailablePages.FirstOrDefault(
            page => page.PageId.Equals("audio", StringComparison.OrdinalIgnoreCase));
        if (audioPage is not null)
        {
            _registrations.Add(host.Ui.Register(new ExtPluginUiExtension(
                "audio-page-command-options",
                audioPage.TopAnchorId,
                CreateV23AudioCommandRow)
            {
                Order = 100
            }));
        }

        // 所有目录项都能作为装饰/替换锚点。写入原生控件前使用描述符给出的资源 ID 申请租约。
        var audioEncoder = host.ParameterPanel.AvailableControls.FirstOrDefault(
            item => item.PageId.Equals("audio", StringComparison.OrdinalIgnoreCase) &&
                    item.ControlName.Equals("MCB_音频编码器", StringComparison.Ordinal));
        if (audioEncoder is not null)
        {
            _registrations.Add(host.Resources.Claim(new ExtPluginResourceClaim(
                "decorate-audio-encoder-control",
                audioEncoder.ResourceId,
                ExtPluginResourceAccess.OrderedTransform)
            {
                Purpose = "演示通过参数面板目录修改任意原生控件，并在 Cleanup 中还原"
            }));
            _registrations.Add(host.Ui.Register(new ExtPluginUiExtension(
                "decorate-audio-encoder-control",
                audioEncoder.AnchorId,
                DecorateAudioEncoder)
            {
                Order = 100,
                Cleanup = CleanupV23Decoration
            }));
        }

        _registrations.Add(host.Commands.RegisterParameterProvider(
            new ExtPluginCommandParameterProvider("sample-metadata", ProvideArguments)
            {
                Order = 100
            }));
        _registrations.Add(host.Commands.RegisterStepProvider(
            new ExtPluginCommandStepProvider("sample-command-step", ProvideSteps)
            {
                Order = 100
            }));
    }

    private Control? DecorateAudioEncoder(IExtPluginUiContext context)
    {
        var target = context.AnchorControl;
        var originalDescription = target.AccessibleDescription;
        EventHandler changed = (_, _) =>
            Log(ExtPluginLogLevel.Trace, $"音频编码器变为：{target.Text}");
        target.AccessibleDescription = "由 Ext v2.3 动态控件目录发现的音频编码器控件";
        target.TextChanged += changed;

        _v23UiCleanup[UiCleanupKey(context)] = () =>
        {
            target.TextChanged -= changed;
            if (!target.IsDisposed)
            {
                target.AccessibleDescription = originalDescription;
            }
        };
        return null;
    }

    private void CleanupV23Decoration(IExtPluginUiContext context)
    {
        if (_v23UiCleanup.TryRemove(UiCleanupKey(context), out var cleanup))
        {
            cleanup();
        }
    }

    private static string UiCleanupKey(IExtPluginUiContext context) =>
        $"{context.SurfaceId}:{context.ExtensionId}";

    private static void ProvideArguments(ExtPluginCommandContext context)
    {
        var state = DeserializeState(context.PluginStateJson);
        if (!state.AddDeclarativeMetadata)
        {
            return;
        }

        context.Arguments.Add(new ExtPluginCommandArgument(
            ExtPluginCommandArgumentPosition.BeforeOutput,
            "-metadata comment=FFmpegFreeUI-Ext-v2.3-sample")
        {
            Description = "示例声明式元数据；会同时出现在预览、模板和实际命令中"
        });
    }

    private static void ProvideSteps(ExtPluginCommandContext context)
    {
        var state = DeserializeState(context.PluginStateJson);
        if (!state.RunDeclarativeCommandStep)
        {
            return;
        }

        context.Steps.Add(new ExtPluginCommandStep(
            "announce",
            "Ext 示例前置命令",
            "cmd.exe",
            "/d /c echo FFmpegFreeUI Ext v2.3 sample step")
        {
            Placement = ExtPluginCommandStepPlacement.BeforeNative,
            IncludeInPreview = true
        });
    }
}
