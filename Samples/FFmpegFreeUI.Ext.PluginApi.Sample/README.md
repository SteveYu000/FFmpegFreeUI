# C# Ext Plugin API v2.2 综合示例

本示例用三个可以独立启用的场景覆盖当前全部公共接口：

1. 自动质量策略：在参数面板中保存 CRF，在预设捕获和任务准备阶段写入结构化预设。
2. 命令与进程审计：修改任务名、输出后缀、进阶参数、最终命令和实际启动程序，并观察退出码。
3. 成功后校验：异步计算输出文件 SHA-256，通过 `ReportResult` 显示结果，最终释放任务缓存。

所有会改变任务或执行耗时工作的选项默认关闭。代码按职责拆成：

- `SamplePlugin.cs`：入口、版本/能力检测、所有注册、状态模型和并发任务缓存。
- `SamplePlugin.Ui.cs`：3 个装饰型锚点和 3 个插入型锚点。
- `SamplePlugin.Pipeline.cs`：14 个处理阶段，按真实调用顺序排列。

## 接口覆盖

- `RegisterChoice`：用稳定 `ChoiceId` 向原生质量下拉框添加安全选项，不直接修改 `Items`。
- `Behaviors.Register`：在原生质量模式联动之后进行有序观察/变换。
- `Resources.Claim`：声明对原始控件的观察意图，展示冲突协调入口。

- `IExtFFmpegFreeUIPlugin`：`Id`、`DisplayName`、`Initialize`。
- `IExtFFmpegFreeUIHost`：`ApiVersion`、`HostVersion`、`Ui`、`Pipeline`、4 种 `Log` 级别。
- `IExtPluginUiRegistry`：`AvailableAnchors`、`Register`、注册句柄保存。
- `IExtPluginUiContext`：全部身份字段、两个控件字段、`GetAnchorControl`、`StateJson`、
  `StateRestored`、`RequestParameterRefresh`。
- `IExtPluginPipelineRegistry`：`AvailableStages`、`Register` 和 `Order`。
- `ExtPluginPipelineContext`：全部字段、阶段属性、取消令牌、`ReportProgress`、`ReportResult`。
- `ExtFFmpegFreeUIUiAnchors.All` 中的 6 个锚点。
- `ExtFFmpegFreeUIPipelineStages.All` 中的 14 个阶段。

示例中的命令字符串修改只用于解释接口。生产插件应使用可靠的参数解析/转义方式，并谨慎提供“接受非零
退出码”或“替换进程”这类高风险选项。

## 构建和安装

在仓库根目录执行：

```powershell
dotnet build .\Samples\FFmpegFreeUI.Ext.PluginApi.Sample\FFmpegFreeUI.Ext.PluginApi.Sample.csproj -c Release
```

只把生成的 `FFmpegFreeUI.Ext.PluginApi.Sample.3fui.dll` 放到 FFmpegFreeUI 的 `Plugin` 目录。不要复制构建目录中的
`FFmpegFreeUI.Ext.PluginSdk.dll`；SDK 和 PluginHost 应由 FFmpegFreeUI 发行包统一放在程序根目录。
