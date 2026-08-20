# C# Ext Plugin API v2.3 综合示例

本示例用四个可以独立启用的场景覆盖当前主要公共接口：

1. 自动质量策略：在参数面板中保存 CRF，在预设捕获和任务准备阶段写入结构化预设。
2. 命令与进程审计：修改任务名、输出后缀、进阶参数、最终命令和实际启动程序，并观察退出码。
3. 成功后校验：异步计算输出文件 SHA-256，通过 `ReportResult` 显示结果，最终释放任务缓存。
4. v2.3 扩展：枚举全部参数控件、装饰音频编码器、向音频页顶部加控件，并把声明式 metadata 和 `cmd.exe` 前置步骤同时带入预览、模板与执行队列。

所有会改变任务或执行耗时工作的选项默认关闭。代码按职责拆成：

- `SamplePlugin.cs`：入口、版本/能力检测、所有注册、状态模型和并发任务缓存。
- `SamplePlugin.Ui.cs`：3 个装饰型锚点和 3 个插入型锚点。
- `SamplePlugin.Pipeline.cs`：14 个处理阶段，按真实调用顺序排列。
- `SamplePlugin.Commands.cs`：v2.3 参数面板目录、动态控件清理、页面插入和声明式命令计划。

## 接口覆盖

- `RegisterChoice`：用稳定 `ChoiceId` 向原生质量下拉框添加安全选项，不直接修改 `Items`。
- `Behaviors.Register`：在原生质量模式联动之后进行有序观察/变换。
- `Resources.Claim`：声明对原始控件的观察意图，展示冲突协调入口。

- `IExtFFmpegFreeUIPlugin`：`Id`、`DisplayName`、`Initialize`。
- `IExtFFmpegFreeUIHost`：`ApiVersion`、`HostVersion`、`Ui`、`Pipeline`、4 种 `Log` 级别。
- `IExtFFmpegFreeUIHostV23`：`ParameterPanel` 与 `Commands`。
- `AvailablePages` / `AvailableControls`：页面插槽、全部原生控件锚点和动态资源 ID。
- `RegisterParameterProvider`：向 `BeforeOutput` 位置贡献可预览的参数。
- `RegisterStepProvider`：贡献由队列统一执行和取消的 `BeforeNative` 外部命令。
- `ExtPluginUiExtension.Cleanup`：还原对动态发现控件的修改和事件订阅。
- `IExtPluginUiRegistry`：`AvailableAnchors`、`Register`、注册句柄保存。
- `IExtPluginUiContext`：全部身份字段、两个控件字段、`GetAnchorControl`、`StateJson`、
  `StateRestored`、`RequestParameterRefresh`。
- `IExtPluginPipelineRegistry`：`AvailableStages`、`Register` 和 `Order`。
- `ExtPluginPipelineContext`：全部字段、阶段属性、取消令牌、`ReportProgress`、`ReportResult`。
- `ExtFFmpegFreeUIUiAnchors.All` 中的 6 个锚点。
- `ExtFFmpegFreeUIPipelineStages.All` 中的 14 个阶段。

示例保留旧的命令字符串修改以展示兼容接口；新增功能应优先使用声明式参数/步骤。生产插件仍须正确转义参数，并谨慎提供“接受非零退出码”“替换进程”或 shell 命令这类高风险选项。

## 构建和安装

在仓库根目录执行：

```powershell
dotnet build .\Samples\FFmpegFreeUI.Ext.PluginApi.Sample\FFmpegFreeUI.Ext.PluginApi.Sample.csproj -c Release
```

只把生成的 `FFmpegFreeUI.Ext.PluginApi.Sample.3fui.dll` 放到 FFmpegFreeUI 的 `Plugin` 目录。不要复制构建目录中的
`FFmpegFreeUI.Ext.PluginSdk.dll`；SDK 和 PluginHost 应由 FFmpegFreeUI 发行包统一放在程序根目录。
