# FFmpegFreeUI Ext Plugin API v2.3 插件开发指南

本文面向希望扩展 FFmpegFreeUI 原生参数界面和任务处理链的插件开发者，对应当前 Ext Plugin API `2.3.0`。插件只需要面向`FFmpegFreeUI.Ext.PluginSdk.dll` 的公共类型编程，不应引用`FFmpegFreeUI.exe` 的内部类型，也不应通过反射查找 FFmpegFreeUI 的私有控件或方法。

Ext Plugin API v2.3 提供六类能力：

- 参数面板目录：发现全部参数页和原生控件，并通过公开属性辅助器读取、修改、装饰或替换控件。
- 安全 UI 扩展：向宿主管理的原生下拉框添加稳定选项，或在任一参数页顶部/底部插入 WinForms 控件。
- 声明式命令：向 FFmpeg 命令的稳定位置贡献参数，或向任务计划贡献由队列托管的外部进程步骤。
- 处理链扩展：在预设、入队、任务准备、命令生成、进程执行和任务终态阶段注册有序处理器。
- 行为点扩展：在公开的原生联动逻辑之前、之后有序调整上下文，或独占替换一段原生行为。
- 资源冲突协调：插件在深度修改共享 UI、逻辑、预设或命令时声明访问模式，宿主在注册阶段拒绝不兼容组合。

Ext Plugin API v2 仍处于实验阶段。公开的锚点 ID、阶段 ID 和合同类型会作为兼容性契约维护；如果以后必须进行破坏性修改，应提升 API 主版本号。

### Ext 命名隔离规则

为避免与官方未来可能发布的插件 SDK 发生程序集、类型或事件名冲突，扩展 API 使用独立身份：

- 程序集：`FFmpegFreeUI.Ext.PluginSdk.dll`、`FFmpegFreeUI.Ext.PluginHost.dll`；
- 命名空间：`FFmpegFreeUI.Ext.PluginSdk`；
- 公开合同类型：统一以 `Ext` 开头，例如 `IExtFFmpegFreeUIPlugin`、`ExtPluginPipelineContext`；
- 稳定锚点、选项、行为、资源和阶段 ID：统一以 `ext.` 开头，例如 `ext.task.before-prepare`；
- 官方兼容 API：`Entry`、`SetHost_*` 以及官方队列事件名保持原样；同一插件程序集可以同时提供官方入口和 Ext 入口，两个入口按各自规则加载。

早期使用 `FFmpegFreeUI.PluginSdk.dll` 和无 `Ext` 前缀类型编译的扩展插件必须重新引用新 SDK 并重新编译。本项目不部署旧名称兼容垫片，因为保留旧程序集身份会重新引入未来命名冲突。

## 1. 运行时组成与安装目录

Ext Plugin API v2 由三部分组成：

| 组件 | 作用 | 放置位置 |
|---|---|---|
| `FFmpegFreeUI.exe` | FFmpegFreeUI 核心；只保留不直接引用 SDK 的桥接逻辑 | 程序根目录 |
| `FFmpegFreeUI.Ext.PluginHost.dll` | v2 宿主实现；把 SDK 合同连接到 FFmpegFreeUI 的界面和任务系统 | 程序根目录 |
| `FFmpegFreeUI.Ext.PluginSdk.dll` | 插件编译时引用的公共合同 | 程序根目录 |

标准目录结构如下：

```text
FFmpegFreeUI.exe
FFmpegFreeUI.Ext.PluginHost.dll
FFmpegFreeUI.Ext.PluginSdk.dll
Plugin\
├─ MyCompany.MyPlugin.3fui.dll
├─ MyCompany.MyPlugin.Dependency.dll
└─ ...
```

注意：

- FFmpegFreeUI 只从 `Plugin` 目录发现文件名匹配 `*.3fui.dll` 的入口程序集。
- `PluginHost` 和 `PluginSdk` 必须放在程序根目录，不要放进 `Plugin`。
- 插件自己的托管依赖可以放在 `Plugin`；不要复制另一份 SDK、PluginHost、LakeUI 或宿主自带依赖。
- 根目录缺少 `FFmpegFreeUI.Ext.PluginSdk.dll` 时，Ext Plugin API v2 会安全禁用。依赖 SDK 的插件会在程序集加载之前被静默跳过，FFmpegFreeUI 本体仍可运行。
- 根目录缺少 `FFmpegFreeUI.Ext.PluginHost.dll`、版本不兼容或初始化失败时，v2 同样安全禁用。
- 当前桥接层接受 `2.2.0` 或更高的 `2.x` 组件，并要求 SDK 与 PluginHost 的主、次版本一致；当前发行包应使用 `2.3.x` SDK 搭配 `2.3.x` Host。
- 新主程序若误配成完整的 `2.2.x` SDK/Host 组合，会保留 v2.2 插件与处理链，v2.3 参数目录和声明式命令则安全降级为空；这只用于容错，发布时不要混装版本。
- 只提供 `Entry` / `SetHost_*` 的插件仍走官方兼容逻辑；需要补充能力时，可以在同一程序集内再实现 `IExtFFmpegFreeUIPlugin`，无需放弃官方接口。

### 为什么同时需要 PluginSdk 和 PluginHost

`PluginSdk` 只定义接口、上下文和稳定 ID，不引用 FFmpegFreeUI 内部实现；`PluginHost` 负责把这些接口连接到FFmpegFreeUI。这样核心程序没有 SDK 时仍能启动，也避免插件直接依赖庞大的宿主程序集。

相关源码：

- SDK 合同：[`FFmpegFreeUI.Ext.PluginSdk/ExtPluginContracts.cs`](../FFmpegFreeUI.Ext.PluginSdk/ExtPluginContracts.cs)
- 可选桥接：[`FFmpegFreeUI/功能/Ext插件扩展桥接_v2.vb`](../FFmpegFreeUI/功能/Ext插件扩展桥接_v2.vb)
- 宿主实现：[`FFmpegFreeUI/功能/Ext插件扩展宿主_v2.vb`](../FFmpegFreeUI/功能/Ext插件扩展宿主_v2.vb)
- PluginHost 项目：[`FFmpegFreeUI.Ext.PluginHost/FFmpegFreeUI.Ext.PluginHost.vbproj`](../FFmpegFreeUI.Ext.PluginHost/FFmpegFreeUI.Ext.PluginHost.vbproj)

## 2. 开发环境与示例

### 2.1 必备环境

插件和宿主当前使用 Windows、.NET 10 与 WinForms。开发机需要：

- Windows 10/11；目标体系结构应与准备调试的 FFmpegFreeUI 发行包兼容；
- .NET 10 SDK，而不只是 .NET Desktop Runtime；
- Visual Studio（安装“.NET 桌面开发”工作负载）、Rider，或 Visual Studio Code 配合 C# Dev Kit；
- 若使用 VB.NET，选择支持 VB 项目的 Visual Studio、Rider 或 `dotnet` CLI。

先在终端确认 SDK：

```powershell
dotnet --info
dotnet --list-sdks
```

列表中应存在 `10.0.x`。只有运行时而没有 SDK 时，编辑器可能能打开代码，但不能还原或编译插件。

在完整源码仓库中首次构建：

```powershell
git clone https://github.com/SteveYu000/FFmpegFreeUI-API-Extended-Edition.git
cd .\FFmpegFreeUI-API-Extended-Edition
dotnet restore .\FFmpegFreeUI-API-Extended-Edition.sln
dotnet build .\FFmpegFreeUI-API-Extended-Edition.sln -c Debug --no-restore
```

### 2.2 SDK 引用与编辑器提示

Ext 插件只引用 `FFmpegFreeUI.Ext.PluginSdk`。不要引用 `FFmpegFreeUI.exe`、`FFmpegFreeUI.dll`、`FFmpegFreeUI.Ext.PluginHost.dll` 或 LakeUI；这些属于宿主运行时实现，不是插件合同。

推荐按开发方式选择引用：

1. 与本仓库一起开发：使用 `ProjectReference`，可以直接导航 SDK 源码；
2. 独立仓库开发：引用发行包中的 `FFmpegFreeUI.Ext.PluginSdk.dll`，并把同版本 XML 文档放在旁边；
3. 类似 Maven 的包方式：使用本地或将来发布的 `FFmpegFreeUI.Ext.PluginSdk` NuGet 包。

Visual Studio、Rider 和支持 C#/VB 语言服务的 VS Code 会根据程序集元数据提供类型补全、构造函数和方法参数提示、回调签名检查及编译期类型检查。`FFmpegFreeUI.Ext.PluginSdk.xml` 负责额外显示中文摘要；只有 DLL 时仍有类型和参数提示，但通常看不到详细说明。

SDK 构建输出中的开发文件是：

```text
FFmpegFreeUI.Ext.PluginSdk.dll
FFmpegFreeUI.Ext.PluginSdk.xml
FFmpegFreeUI.Ext.PluginSdk.Deploy.targets
```

### 2.3 示例

仓库提供两套可直接编译的示例：

- [C# v2.3 综合示例](../Samples/FFmpegFreeUI.Ext.PluginApi.Sample)：动态参数控件、页面插入、声明式参数/步骤和 SHA-256 后处理。
- [VB.NET v2.2 兼容基线示例](../Samples/FFmpegFreeUI.Ext.PluginApi.VbVmafSample)：传统锚点、命令/进程处理和 VMAF 后处理。

C# 示例覆盖 v2.3 新能力以及 6 个传统 UI 锚点、1 个安全下拉框锚点、1 个行为点和 14 个处理阶段；VB.NET 示例用于证明只使用 v2.2 合同的插件仍可在 v2.3 宿主运行。遇到文档与行为不一致时，以当前 SDK 公共合同和可编译示例为准。

## 3. 从零创建插件项目

### 3.1 创建 C# 类库

```powershell
dotnet new classlib -lang C# -n MyCompany.MyPlugin -f net10.0
cd MyCompany.MyPlugin
```

将 `.csproj` 改为：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AssemblyName>MyCompany.MyPlugin.3fui</AssemblyName>
    <RootNamespace>MyCompany.MyPlugin</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\FFmpegFreeUI-API-Extended-Edition\FFmpegFreeUI.Ext.PluginSdk\FFmpegFreeUI.Ext.PluginSdk.csproj">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>

  <Import Project="..\FFmpegFreeUI-API-Extended-Edition\FFmpegFreeUI.Ext.PluginSdk\FFmpegFreeUI.Ext.PluginSdk.Deploy.targets" />
</Project>
```

### 3.2 创建 VB.NET 类库

```powershell
dotnet new classlib -lang VB -n MyCompany.MyPlugin -f net10.0
cd MyCompany.MyPlugin
```

将 `.vbproj` 改为：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <OptionExplicit>On</OptionExplicit>
    <OptionInfer>On</OptionInfer>
    <OptionStrict>On</OptionStrict>
    <AssemblyName>MyCompany.MyPlugin.3fui</AssemblyName>
    <RootNamespace>MyCompany.MyPlugin</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\FFmpegFreeUI-API-Extended-Edition\FFmpegFreeUI.Ext.PluginSdk\FFmpegFreeUI.Ext.PluginSdk.csproj">
      <Private>false</Private>
    </ProjectReference>
  </ItemGroup>

  <Import Project="..\FFmpegFreeUI-API-Extended-Edition\FFmpegFreeUI.Ext.PluginSdk\FFmpegFreeUI.Ext.PluginSdk.Deploy.targets" />
</Project>
```

关键设置：

- 必须使用与当前 SDK 兼容的 Windows 目标框架并启用 WinForms。
- `AssemblyName` 必须以 `.3fui` 结尾，输出文件才会匹配 `*.3fui.dll`。
- `<Private>false</Private>` 防止构建时把 SDK 当作插件私有依赖复制到发布目录。
- 如果使用 SDK 二进制而不是源码项目，可改用带 `HintPath` 的 `<Reference>`，仍应设置`<Private>false</Private>`。
- `<Import ...Deploy.targets>` 提供 `ExtDeployFFmpegFreeUIPlugin`，只影响显式部署或启用自动部署的构建；普通 `Build` 默认不会复制到安装目录。
- SDK 当前不是必须从 NuGet 获取；直接引用项目或发行包提供的合同 DLL 即可。
- 虽然FFmpegFreeUI使用了LakeUI，但这不是 Ext Plugin API 的硬依赖。普通 WinForms 控件最不容易受宿主 UI 库版本影响。如果插件自行引用第三方 UI 库，开发者需处理版本兼容、分发和许可证义务。

### 3.3 独立项目引用 SDK 文件

把同一版本的三个开发文件放进插件仓库的 `libs` 目录，然后使用：

```xml
<ItemGroup>
  <Reference Include="FFmpegFreeUI.Ext.PluginSdk">
    <HintPath>libs\FFmpegFreeUI.Ext.PluginSdk.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>

<Import Project="libs\FFmpegFreeUI.Ext.PluginSdk.Deploy.targets" />
```

`FFmpegFreeUI.Ext.PluginSdk.xml` 不需要写进项目文件，只要与 DLL 同名并位于同一目录，编辑器就能读取中文提示。提交插件源码时可以提交这些开发文件，也可以改用包引用；发布插件成品时不要把 SDK 放进 `Plugin`。

### 3.4 本地 NuGet 包：类似 Maven 的依赖方式

SDK 项目支持直接打包，包内包含编译合同、XML 文档和自动导入的一键部署目标：

```powershell
dotnet pack .\FFmpegFreeUI.Ext.PluginSdk\FFmpegFreeUI.Ext.PluginSdk.csproj `
  -c Release -o .\artifacts\packages
```

在独立插件项目中引用本地包：

```xml
<ItemGroup>
  <PackageReference Include="FFmpegFreeUI.Ext.PluginSdk"
                    Version="2.3.0"
                    PrivateAssets="all"
                    ExcludeAssets="runtime" />
</ItemGroup>
```

然后在插件目录执行一次还原，并把刚才的包目录作为源：

```powershell
dotnet restore --source "D:\src\FFmpegFreeUI-API-Extended-Edition\artifacts\packages"
```

`PrivateAssets="all"` 防止 SDK 继续传递给引用插件项目的其他工程；`ExcludeAssets="runtime"` 只保留编译合同，避免将 SDK 私有副本复制到插件输出。使用包引用时，部署目标通过 `buildTransitive` 自动导入，不再需要手写 `<Import>`。将来把同一个包发布到 NuGet.org 或 GitHub Packages 后，只需改包源，不需要改变项目结构。

## 4. 实现插件入口

FFmpegFreeUI 会在每个 `*.3fui.dll` 中查找实现 `IExtFFmpegFreeUIPlugin` 的可实例化类型。入口必须：

1. 实现 `IExtFFmpegFreeUIPlugin`；
2. 不是抽象类或接口；
3. 提供公共无参构造函数；为便于其他工具发现和调试，也建议入口类型本身公开；
4. 返回非空且全局唯一的插件 `Id`。

一个程序集可以包含多个入口，宿主按类型全名排序后初始化；为了发布和排错简单，通常一个程序集只提供一个入口。

### 4.1 官方接口优先与双入口插件

Ext SDK 的定位是补充官方插件接口尚未提供的能力，而不是替代官方接口。官方接口已经提供的功能应继续使用官方方式，例如：

- 使用 `SetHost_AddCustomWinformPanel` 或 `SetHost_AddCustomWpfPanel` 注册主窗口左侧入口和独立页面；
- 使用官方入队、媒体流选择器和队列事件回调完成已有能力；
- 只有原生 UI 锚点、预设扩展数据、有序处理链、取消令牌和资源冲突协调等官方接口没有提供的能力才使用 Ext SDK。

同一个 `*.3fui.dll` 可以同时包含：

1. 一个或多个 `IExtFFmpegFreeUIPlugin` 实现；
2. 一个官方兼容 `Entry` 类及其 `SetHost_*` 方法。

宿主按以下顺序加载双入口插件：

1. 找到 `Entry` 类并注入全部可用的官方回调；
2. 初始化 `IExtFFmpegFreeUIPlugin`，注册 Ext 扩展；
3. 调用官方静态 `Entry()`，完成页面等官方能力注册。

因此官方回调在 Ext `Initialize` 执行前已经可用；一般仍建议把官方注册集中放在 `Entry()` 中，把 Ext 注册集中放在 `Initialize()` 中，避免重复初始化。纯官方插件和纯 Ext 插件的行为保持不变。

宿主首先按原有 `{AssemblyName}.Entry` 约定查找；若未找到，也接受程序集中唯一一个名称为 `Entry` 的非嵌套类型，以兼容程序集名称以 `.3fui` 结尾的 Ext 项目。存在多个候选 `Entry` 时会拒绝加载，插件应只保留一个官方入口。

### 4.2 C# 入口

```csharp
using FFmpegFreeUI.Ext.PluginSdk;

namespace MyCompany.MyPlugin;

public sealed class MyPlugin : IExtFFmpegFreeUIPlugin
{
    private readonly List<IDisposable> _registrations = new();

    // 推荐使用组织前缀或反向域名；发布后不要随意修改。
    public string Id => "com.example.my-plugin";
    public string DisplayName => "我的 FFmpegFreeUI 插件";

    public void Initialize(IExtFFmpegFreeUIHost host)
    {
        if (host.ApiVersion < new Version(2, 2, 0))
        {
            throw new NotSupportedException("需要 Ext Plugin API 2.2 或更高版本");
        }

        host.Log(
            ExtPluginLogLevel.Information,
            $"插件已初始化；API={host.ApiVersion}，FFmpegFreeUI={host.HostVersion}");
    }
}
```

### 4.3 VB.NET 入口

```vb
Imports FFmpegFreeUI.Ext.PluginSdk

Public NotInheritable Class MyPlugin
    Implements IExtFFmpegFreeUIPlugin

    Private ReadOnly 注册项 As New List(Of IDisposable)

    Public ReadOnly Property Id As String Implements IExtFFmpegFreeUIPlugin.Id
        Get
            Return "com.example.my-plugin"
        End Get
    End Property

    Public ReadOnly Property DisplayName As String Implements IExtFFmpegFreeUIPlugin.DisplayName
        Get
            Return "我的 FFmpegFreeUI 插件"
        End Get
    End Property

    Public Sub Initialize(host As IExtFFmpegFreeUIHost) Implements IExtFFmpegFreeUIPlugin.Initialize
        If host.ApiVersion < New Version(2, 2, 0) Then
            Throw New NotSupportedException("需要 Ext Plugin API 2.2 或更高版本")
        End If

        host.Log(
            ExtPluginLogLevel.Information,
            $"插件已初始化；API={host.ApiVersion}，FFmpegFreeUI={host.HostVersion}")
    End Sub
End Class
```

`Initialize` 在启动加载插件时调用。这里只做版本检查和轻量注册，不要同步扫描大量文件、访问网络或等待外部进程。初始化抛出异常会导致当前插件加载失败。

注册方法返回 `IDisposable`。宿主会跟踪这些句柄，并在插件作用域释放时按相反顺序注销；插件也可保存句柄以便主动提前注销。当前版本没有插件热重载，修改 DLL 后应完全退出并重新启动 FFmpegFreeUI。

## 5. 宿主接口 `IExtFFmpegFreeUIHost`

| 成员 | 含义 |
|---|---|
| `ApiVersion` | 宿主实际支持的 Ext Plugin API 版本；注册新能力前应检查。 |
| `HostVersion` | FFmpegFreeUI 主程序集版本字符串，可用于诊断，不建议依赖字符串比较实现功能开关。 |
| `Ui` | UI 扩展注册表。 |
| `Pipeline` | 处理链注册表。 |
| `Behaviors` | 原生稳定行为点注册表。 |
| `Resources` | 共享资源访问声明和冲突协调注册表。 |
| `ParameterPanel` | v2.3 参数页、全部原生控件及其稳定锚点目录。 |
| `Commands` | v2.3 声明式 FFmpeg 参数和外部命令步骤注册表。 |
| `Log(level, message, exception)` | 写插件诊断信息；当前实现输出到调试器，不等同于任务日志。 |

v2.3 直接把新能力加入基础宿主接口，插件不需要额外转换接口。使用新成员前仍应检查宿主能力版本：

```csharp
if (host.ApiVersion < new Version(2, 3, 0))
{
    throw new NotSupportedException("需要 Ext Plugin API 2.3");
}

var pages = host.ParameterPanel.AvailablePages;
var controls = host.ParameterPanel.AvailableControls;
var commands = host.Commands;
```

已编译的 v2.2 插件只消费旧成员，仍可直接运行；v2.3 插件通过 `ApiVersion` 声明最低能力要求。第三方不应自行实现宿主接口，测试替身需要在重新编译时补充两个新成员。

`ExtPluginLogLevel` 包含：

- `Trace`：高频诊断；
- `Information`：初始化或正常状态；
- `Warning`：功能降级或可恢复问题；
- `Error`：插件错误，可附带异常。

需要让用户在当前编码任务日志中看到信息时，使用管线上下文的 `ReportProgress` 或 `ReportResult`，不要只调用 `host.Log`。

## 6. UI 扩展

优先级原则：能用 `RegisterChoice` 或插入型锚点完成的功能，不要直接修改 `AnchorControl`。安全 API 由宿主维护稳定 ID、顺序、注销清理和预设回退；原始控件仅作为兼容旧插件及少数深度场景的逃生口。

### 6.1 向原生下拉框添加安全选项

下面的选项显示在“全局质量控制方式”原生下拉框中，但原生预设仍按 CRF 捕获。插件卸载后该选项会被删除；预设恢复时由插件状态决定是否重新选中。

```csharp
var choice = new ExtPluginUiChoiceExtension(
    id: "automatic-crf",
    anchorId: ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode,
    choiceId: "com.example.my-plugin.automatic-crf",
    displayText: "自动计算 CRF",
    nativeFallbackChoiceId: ExtFFmpegFreeUIUiChoices.VideoQualityCrf)
{
    Order = 100,
    RestoreSelection = context => ReadEnabled(context.StateJson),
    SelectionChanged = (context, selected) =>
        context.StateJson = WriteEnabled(context.StateJson, selected)
};
choice.ValueOverrides[ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityParameterName] = "-crf";
choice.ValueOverrides[ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityValue] = "";
choice.DisabledAnchors.Add(ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityValue);

_registrations.Add(host.Ui.RegisterChoice(choice));
```

`choiceId` 是插件选项的持久语义标识，必须全局唯一且发布后保持不变。不要读取或保存它的 `SelectedIndex`。`nativeFallbackChoiceId` 必须是 `ExtFFmpegFreeUIUiChoices` 中的原生选项；它决定原生联动和预设枚举怎样理解插件选项。

同一锚点的插件选项按 `Order → PluginId → ExtensionId` 排列。宿主在离开选项时恢复 `DisabledAnchors` 的原启用状态，在注销时移除选项；多个插件不能注册相同的 `choiceId`。

### 6.2 注册普通 UI 扩展

```csharp
if (host.Ui.AvailableAnchors.Contains(
        ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityAfterGlobal))
{
    _registrations.Add(host.Ui.Register(new ExtPluginUiExtension(
        id: "quality-options",
        anchorId: ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityAfterGlobal,
        createControl: CreateQualityOptions)
    {
        Order = 100
    }));
}
```

`ExtPluginUiExtension` 成员：

| 成员 | 规则 |
|---|---|
| `Id` | 当前插件内全局唯一；同一插件不能在不同锚点复用同一扩展 ID。 |
| `AnchorId` | 宿主公开的稳定锚点 ID。优先使用 `ExtFFmpegFreeUIUiAnchors` 常量。 |
| `Order` | 同一锚点内从小到大排列。相同值再按插件 ID、扩展 ID 排序。 |
| `CreateControl` | 每个参数面板实例都会调用；插入型返回新控件，装饰型必须返回 `null`/`Nothing`。 |
| `Cleanup` | v2.3 可选回调；扩展注销或界面销毁时在 UI 线程调用。必须在这里移除给原生控件添加的事件并恢复主动修改的属性。 |
| `Mode` / `ResourceId` / `ResourceAccess` | 默认只插入/装饰；替换原生控件必须使用 `ReplaceAnchor`，并以描述符给出的资源 ID 申请 `Exclusive`。 |

`AvailableAnchors` 同时包含 6 个传统合同锚点和 v2.3 当前参数面板目录中的动态锚点。插件加载前主参数面板目录已经建立；仍应在注册前检查目标 ID，便于兼容不同宿主版本。

### 6.3 6 个传统 UI 锚点

这些 v2.2 兼容锚点都位于“参数面板 → 视频参数｜质量”，在 v2.3 中继续原样可用。UI 锚点只决定界面位置，不会自动修改预设或处理链；需要持久化和参数生效时，应配合 `StateJson` 与声明式命令或管线阶段。

| SDK 常量 / ID | 类型与位置 | 可做的事情 | 限制 |
|---|---|---|---|
| `ParametersVideoQualityMode` / `ext.parameters.video.quality.mode` | 装饰型；全局质量控制方式下拉框 | 只读观察和无障碍装饰；添加选项应使用 `RegisterChoice` | 工厂必须返回 `null`/`Nothing`；禁止直接增删或重排 `Items`。 |
| `ParametersVideoQualityParameterName` / `ext.parameters.video.quality.parameter-name` | 装饰型；质量参数名控件 | 读取或填写 `-crf`、`-cq` 等参数名 | 原生捕获仍按宿主逻辑执行。 |
| `ParametersVideoQualityValue` / `ext.parameters.video.quality.value` | 装饰型；质量值输入框 | 读取、填写、清空或锁定质量值 | 耗时计算不能阻塞 UI，应放到异步任务阶段。 |
| `ParametersVideoQualityAfterGlobal` / `ext.parameters.video.quality.global.after` | 插入型；全局质量控制行之后 | 插入与质量策略配套的下拉框、输入框、按钮和说明 | 工厂返回一个新控件。 |
| `ParametersVideoQualityBeforeAdvanced` / `ext.parameters.video.quality.advanced.before` | 插入型；比特率区域之后、进阶质量控制之前 | 插入高级参数生成器、校验按钮或摘要 | 只是视觉位置，参数仍需写入预设或命令上下文。 |
| `ParametersVideoQualityPageBottom` / `ext.parameters.video.quality.page.bottom` | 插入型；进阶参数编辑区域之前的公开插槽 | 插入后处理开关或工具栏 | 名称是兼容性合同，不应假设它永远是滚动页面绝对末尾。 |

### 6.4 `IExtPluginUiContext` 全部成员

| 成员 | 含义 |
|---|---|
| `PluginId` | 当前插件入口 ID。 |
| `ExtensionId` | 当前 UI 扩展 ID。 |
| `AnchorId` | 当前锚点 ID。 |
| `SurfaceId` | 当前参数面板实例 ID；多个面板实例之间不同。 |
| `AnchorControl` | 高风险逃生口。读取/监听可声明 `Observe`；直接修改前必须声明相应资源，并在注销时完整还原。 |
| `ContainerControl` | 插入型锚点由宿主创建的容器；装饰型为 `null`/`Nothing`。 |
| `GetAnchorControl(anchorId)` | 获取同一参数面板实例中的另一个公开锚点控件；不可用时返回空。 |
| `StateJson` | 当前参数面板实例中、按插件 ID 隔离的持久化 JSON。默认是 `{}`。 |
| `StateRestored` | 宿主从另一个预设恢复该插件状态后触发。 |
| `RequestParameterRefresh()` | 请求刷新参数总览和命令预览。 |

同一插件在一个参数面板中注册的多个 UI 扩展共享一份 `StateJson`；不同插件按插件 ID 隔离。赋给`StateJson` 的内容必须是有效 JSON，宿主会解析并规范化，损坏的 JSON 会抛出异常。

### 6.5 插入型控件与状态持久化示例

```csharp
private sealed class PluginState
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public string Value { get; set; } = "32";
}

private static Control CreateQualityOptions(IExtPluginUiContext context)
{
    var row = new FlowLayoutPanel
    {
        AutoSize = true,
        Dock = DockStyle.Top,
        WrapContents = false
    };
    var enabled = new CheckBox { AutoSize = true, Text = "启用插件质量值" };
    var value = new TextBox { Width = 100 };
    var refresh = new Button { AutoSize = true, Text = "刷新预览" };
    row.Controls.AddRange(new Control[] { enabled, value, refresh });

    var restoring = false;

    PluginState ReadState()
    {
        try
        {
            return JsonSerializer.Deserialize<PluginState>(context.StateJson) ?? new();
        }
        catch (JsonException)
        {
            return new PluginState();
        }
    }

    void Restore()
    {
        restoring = true;
        try
        {
            var state = ReadState();
            enabled.Checked = state.Enabled;
            value.Text = state.Value;
        }
        finally
        {
            restoring = false;
        }
    }

    void Save()
    {
        if (restoring) return;
        context.StateJson = JsonSerializer.Serialize(new PluginState
        {
            Enabled = enabled.Checked,
            Value = value.Text.Trim()
        });
        context.RequestParameterRefresh();
    }

    enabled.CheckedChanged += (_, _) => Save();
    value.TextChanged += (_, _) => Save();
    refresh.Click += (_, _) => context.RequestParameterRefresh();

    EventHandler restored = (_, _) => Restore();
    context.StateRestored += restored;
    row.Disposed += (_, _) => context.StateRestored -= restored;
    Restore();
    return row;
}
```

每次调用控件工厂必须创建新的控件，不能复用已经有 `Parent` 的全局实例。订阅 `StateRestored` 后，应在
控件释放时取消订阅；恢复期间使用保护标志，避免 `TextChanged` 等事件形成写回和刷新循环。

### 6.6 装饰型锚点示例

```csharp
private static Control? DecorateQualityValue(IExtPluginUiContext context)
{
    context.AnchorControl.AccessibleDescription = "插件可以辅助填写该质量值";

    var modeControl = context.GetAnchorControl(
        ExtFFmpegFreeUIUiAnchors.ParametersVideoQualityMode);
    // 可以读取或协调另一个公开锚点，但不要依赖宿主私有类型。

    return null; // 装饰型锚点必须返回 null。
}
```

如果装饰型工厂返回了控件，宿主会释放该控件并报错。

### 6.7 v2.3 参数面板目录与全部控件

`IExtFFmpegFreeUIHost.ParameterPanel` 提供两个只读快照：

| 集合 | 描述符关键字段 |
|---|---|
| `AvailablePages` | `PageId`、显示名以及该页的 `TopAnchorId` / `BottomAnchorId`。 |
| `AvailableControls` | `ControlId`、所属页、层级路径、设计器名称、控件类型、`AnchorId`、`ResourceId`、默认值属性名。 |

宿主会收录主参数页、内嵌子页以及已经实例化的画面参数弹窗。页面 ID 包括 `overview`、`presets`、`output`、`decoder`、`video-encoder`、`video-frame`、`video-quality`、`color`、`frame-server`、`audio`、`trim`、`filter-order`、`custom*`、`stream-control`、`additional`、`metadata`、`chapters`、`attachments` 和 `video-frame-*`。不要依靠这段文字穷举；始终枚举 `AvailablePages`。

ID 规则由 `ExtFFmpegFreeUIParameterPanelIds` 定义：

- 页面插槽：`ext.parameters.page.{pageId}.top` / `.bottom`；
- 控件锚点：`ext.parameters.control.{pageId}.{escaped-control-path}`；
- 控件资源通常以 `ext.parameters.control-resource.` 开头；存在传统别名时可能返回传统资源 ID，所以必须使用描述符的 `ResourceId`，不要自行拼接。

控件路径以设计器 `Name` 为主，发布后按兼容合同维护。上游新增控件会自然出现在目录；插件必须按 `PageId + ControlName` 或已保存的 `AnchorId` 查找，并在目标缺失时降级，不得按控件序号或私有窗体字段反射查找。

下面示例修改音频编码器控件。目录只提供描述信息；取得真实控件仍统一通过 `Ui.Register`，因此每个参数面板实例都有独立上下文和完整生命周期：

```csharp
private readonly ConcurrentDictionary<string, (string? Description, EventHandler Changed)>
    _audioEncoderSnapshots = new(StringComparer.OrdinalIgnoreCase);

var descriptor = host.ParameterPanel.AvailableControls.FirstOrDefault(x =>
    x.PageId.Equals("audio", StringComparison.OrdinalIgnoreCase) &&
    x.ControlName == "MCB_音频编码器");

if (descriptor is not null)
{
    _registrations.Add(host.Resources.Claim(new ExtPluginResourceClaim(
        "write-audio-encoder", descriptor.ResourceId,
        ExtPluginResourceAccess.OrderedTransform)));

    _registrations.Add(host.Ui.Register(new ExtPluginUiExtension(
        "decorate-audio-encoder", descriptor.AnchorId, context =>
        {
            var key = $"{context.SurfaceId}:{context.ExtensionId}";
            var oldDescription = context.AnchorControl.AccessibleDescription;
            EventHandler changed = (_, _) => host.Log(
                ExtPluginLogLevel.Trace, context.AnchorControl.Text);
            _audioEncoderSnapshots[key] = (oldDescription, changed);
            context.AnchorControl.AccessibleDescription = "由插件增强";
            context.AnchorControl.TextChanged += changed;
            return null;
        })
    {
        Cleanup = context =>
        {
            var key = $"{context.SurfaceId}:{context.ExtensionId}";
            if (!_audioEncoderSnapshots.TryRemove(key, out var snapshot)) return;
            context.AnchorControl.TextChanged -= snapshot.Changed;
            if (!context.AnchorControl.IsDisposed)
                context.AnchorControl.AccessibleDescription = snapshot.Description;
        }
    }));
}
```

示例按 `SurfaceId + ExtensionId` 分别保存每个界面实例的快照。只改无障碍文本可声明 `OrderedTransform`；完整替换控件必须将 UI 扩展设为 `ReplaceAnchor`，并使用同一 `ResourceId` 的 `Exclusive`。读取可用 `Observe`。修改会影响原生预设捕获的值时，调用 `RequestParameterRefresh()`。

`ExtPluginControlAccess` 可在不引用 LakeUI 的情况下按公开属性读写：`TryGetValue` / `TrySetValue` 使用描述符所示的默认值语义，`TryGetProperty` / `TrySetProperty` 可处理 `Text`、`Checked`、`Value`、`Enabled`、`Visible` 等公共属性并执行常见类型转换。调用方必须处于控件所属 UI 线程。

### 6.8 在任意参数页增加控件

使用页面描述符的顶部或底部锚点，无需修改设计器，也无需依赖页面私有布局：

```csharp
var audioPage = host.ParameterPanel.AvailablePages
    .FirstOrDefault(x => x.PageId == "audio");
if (audioPage is not null)
{
    _registrations.Add(host.Ui.Register(new ExtPluginUiExtension(
        "my-audio-options", audioPage.TopAnchorId, context =>
            new FlowLayoutPanel
            {
                AutoSize = true,
                Controls = { new CheckBox { AutoSize = true, Text = "启用插件参数" } }
            })));
}
```

页面插槽内的插件控件按 `Order → PluginId → ExtensionId` 排列。控件由宿主容器持有并随页面释放；插件订阅其他对象事件时仍应在控件 `Disposed` 或扩展 `Cleanup` 中解绑。

## 7. 处理链注册与执行规则

### 7.1 注册处理器

```csharp
if (host.Pipeline.AvailableStages.Contains(
        ExtFFmpegFreeUIPipelineStages.TaskBeforePrepare))
{
    _registrations.Add(host.Pipeline.Register(new ExtPluginPipelineHandler(
        id: "prepare-quality",
        stageId: ExtFFmpegFreeUIPipelineStages.TaskBeforePrepare,
        callback: PrepareQualityAsync)
    {
        Order = 100
    }));
}
```

`ExtPluginPipelineHandler` 成员：

| 成员 | 规则 |
|---|---|
| `Id` | 当前插件内全局唯一；不能在不同阶段复用同一处理器 ID。 |
| `StageId` | 必须是宿主支持的阶段；使用 `ExtFFmpegFreeUIPipelineStages` 常量。 |
| `Order` | 同一阶段内从小到大执行。 |
| `Callback` | 签名为 `ValueTask Callback(ExtPluginPipelineContext, CancellationToken)`。 |

### 7.2 同阶段多个插件如何执行

对同一个上下文，同一阶段的处理器不是并行执行，而是按下面的稳定顺序逐个等待：

1. `Order` 从小到大；
2. `Order` 相同时按插件 ID；
3. 仍相同时按处理器 ID。

每个处理器成功返回后，宿主把它对上下文的修改复制回共享上下文，所以下一个处理器能看到前一个处理器的修改，也可能再次覆盖这些修改。不同阶段的 `Order` 互不比较。

当前实现采用失败即停：

- 一个处理器抛出异常后，当前阶段剩余处理器不再执行；
- 异常信息会包含插件 ID、处理器 ID 和阶段 ID；
- 普通任务阶段的异常通常会使当前任务失败；
- `ext.task.after-failed` 和 `ext.task.after-finish` 的外层会捕获并写任务日志，不改变已经确定的终态，但当前阶段中排在失败处理器之后的其他插件仍不会运行；
- 一个终态处理器长时间不返回，也会阻塞后续插件和任务最终清理。

因此，公共插件尤其应让 `ext.task.after-finish` 保持快速、有界、幂等并自行处理可恢复错误。不要把插件间依赖建立在恰好相同的默认 `Order` 上。

### 7.3 同步阶段与异步阶段

以下阶段是同步阶段：

- `ext.preset.before-apply`
- `ext.preset.after-apply`
- `ext.preset.before-capture`
- `ext.preset.after-capture`
- `ext.queue.before-add`
- `ext.command.before-build`
- `ext.command.after-build`

同步阶段的回调必须立即返回已经完成的 `ValueTask`。如果回调开始异步等待，宿主会报错。不要用`.Result`、`.Wait()` 或 `GetAwaiter().GetResult()` 在同步阶段阻塞 I/O。

其余 `task.*` 和 `process.*` 阶段是异步阶段，可以等待文件、网络和外部进程，并应传递取消令牌。

### 7.4 C# 与 VB.NET 回调写法

C# 可以直接使用 `async ValueTask`：

```csharp
private static async ValueTask PrepareQualityAsync(
    ExtPluginPipelineContext context,
    CancellationToken cancellationToken)
{
    context.ReportProgress("正在分析媒体……", 0.1);
    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
    context.ReportProgress("分析完成", 1);
}
```

VB.NET 的 `Async Function` 不能直接声明为 `ValueTask`，使用适配器：

```vb
Private Shared Function PrepareQualityAsync(
    context As ExtPluginPipelineContext,
    cancellationToken As CancellationToken) As ValueTask

    Return New ValueTask(PrepareQualityCoreAsync(context, cancellationToken))
End Function

Private Shared Async Function PrepareQualityCoreAsync(
    context As ExtPluginPipelineContext,
    cancellationToken As CancellationToken) As Task

    context.ReportProgress("正在分析媒体……", 0.1)
    Await Task.Delay(200, cancellationToken).ConfigureAwait(False)
    context.ReportProgress("分析完成", 1)
End Function
```

### 7.5 v2.3 声明式 FFmpeg 参数

能表达为 FFmpeg 参数的扩展应使用 `IExtPluginCommandRegistry.RegisterParameterProvider`，不要再解析和拼接整段 `CommandLine`。宿主会在每次命令构建时调用同步回调，并把结果放入准确位置：

| `ExtPluginCommandArgumentPosition` | 插入位置 |
|---|---|
| `Global` | `-hide_banner` 之前，只放 FFmpeg 全局选项。 |
| `BeforeInput` | 原生输入选项之后、主输入 `-i` 之前。 |
| `AfterInput` | 主输入和全部附加输入之后、输出选项之前。 |
| `BeforeOutput` | 原生输出选项之后、输出目标之前。 |
| `AfterOutput` | 输出目标之后。 |

```csharp
_registrations.Add(host.Commands.RegisterParameterProvider(
    new ExtPluginCommandParameterProvider("write-comment", context =>
    {
        var state = JsonSerializer.Deserialize<MyState>(context.PluginStateJson);
        if (state?.Enabled == true)
        {
            context.Arguments.Add(new ExtPluginCommandArgument(
                ExtPluginCommandArgumentPosition.BeforeOutput,
                "-metadata comment=my-plugin")
            {
                Order = 100,
                Description = "写入插件标记"
            });
        }
    })
    {
        Order = 100
    }));
```

提供器和参数都按 `Order` 排序，相同值再按插件/提供器 ID 稳定排序。宿主自动申请 `ext.command.arguments` 的 `OrderedTransform` 租约。回调可能因参数预览、预设列表、任务准备、重建和二次编码反复执行，必须纯计算、快速、幂等，不能在这里启动进程、访问网络或产生一次性文件。

`ExtPluginCommandContext` 提供 `PluginId`、完整 `PresetJson`、已从 `插件扩展数据[PluginId]` 提取的 `PluginStateJson`、输入/输出路径、任务 ID、阶段名、`IsPreview` 和可扩展 `Properties`。参数文本中的 `<输入文件>` / `<输出文件>` 会按原有通配规则替换。宿主不会对 `Text` 自动加引号；插件必须生成合法的 FFmpeg 参数片段。

参数面板总览、预设命令模板、队列命令快照和真正执行都调用同一提供器，因此不会再出现“插件 UI 有选项，但预览看不到”或“预览与实际执行不同”的分叉。旧的 `ext.command.before-build` / `after-build` 继续兼容，并包围声明式构建：结构化预设变换先运行，声明式参数随后加入，最终原始字符串变换最后运行。

“完全自己写”模式无法可靠猜测任意命令结构。v2.3 支持在模板中显式放置 `ExtFFmpegFreeUICommandPlaceholders` 的五个标记：`<ext:global>`、`<ext:before-input>`、`<ext:after-input>`、`<ext:before-output>`、`<ext:after-output>`。旧模板没有标记时，宿主会兼容性推断：全局/输入前参数放到开头，输入后/输出前参数优先插入 `<输出文件>` 前，输出后参数放到末尾；新模板应显式写标记以避免歧义。

### 7.6 v2.3 插件自定义命令步骤

插件需要在编码前后运行独立程序时，应通过 `RegisterStepProvider` 声明步骤，不要在命令预览回调中直接 `Process.Start`：

```csharp
_registrations.Add(host.Commands.RegisterStepProvider(
    new ExtPluginCommandStepProvider("prepare-sidecar", context =>
    {
        if (!FeatureEnabled(context.PluginStateJson)) return;
        context.Steps.Add(new ExtPluginCommandStep(
            "prepare", "准备插件旁车文件", "my-tool.exe",
            "--input \"<输入文件>\" --output \"<输出文件>.json\"")
        {
            Placement = ExtPluginCommandStepPlacement.BeforeNative,
            WorkingDirectory = AppContext.BaseDirectory,
            IncludeInPreview = true,
            ParseFFmpegProgress = false
        });
    })));
```

步骤的身份是“插件 ID + 提供器 ID + 步骤 ID”；同一回调内步骤 ID 必须非空且唯一。`BeforeNative` 在全部原生 FFmpeg 步骤前执行，`AfterNative` 在全部原生步骤后执行。需要先用 ffprobe 取得掐头去尾总时长时，宿主会先只执行探测并重建计划，防止前置插件步骤执行两次。

队列以 `UseShellExecute=false` 直接启动 `ProcessFileName`，不会通过 `cmd`/PowerShell解释，也不会套用“替代 FFmpeg 程序”或“覆盖参数传递”。需要 shell 语法时插件必须明确把 `cmd.exe` 或 PowerShell 作为进程，并负责正确转义。宿主会：

- 在参数模板和预览中显示进程及参数；
- 捕获标准输出/错误并写入当前步骤日志；
- 统一支持暂停、恢复、停止和取消；
- 在每个步骤外继续触发 `ext.process.before-start` / `after-exit`，并在上下文中提供 `isPluginStep`、`pluginId`、`pluginProviderId`、`pluginStepId`；
- 把非零退出码视为步骤失败，随后按正常任务错误链处理；
- 仅在 `ParseFFmpegProgress=true` 时把输出交给 FFmpeg 进度解析器。

步骤回调本身仍是同步、可重复的计划生成器；真正工作只由队列执行。外部工具不存在、启动失败或回调抛错都会明确使任务准备/执行失败。原来在 `ext.task.after-complete` 中自行启动子进程的插件继续可用，但那类进程不会自动进入命令模板，也不会自动经过逐步骤日志与 `process.*`；能改成声明式步骤时应优先改用步骤提供器。

## 8. 原生行为点与深度定制

行为点适合修改一段明确、稳定的原生逻辑。执行顺序固定为：

```text
BeforeNative（按 Order、插件 ID、处理器 ID）
→ 原生实现，或唯一的 ReplaceNative
→ AfterNative（按 Order、插件 ID、处理器 ID）
```

当前公开行为点：

| 常量 / ID | 稳定属性键 | 能改变什么 |
|---|---|---|
| `ParametersVideoQualityModeChanged` / `ext.parameters.video.quality.mode.changed` | `selectedChoiceId`、`nativeChoiceId`、`parameterName`、`qualityValue` | 修改质量模式变化时参数名和质量值的联动；`AfterNative` 可校正原生结果，`ReplaceNative` 可完整代替这一段联动。 |

C# 有序变换示例：

```csharp
_registrations.Add(host.Behaviors.Register(new ExtPluginBehaviorHandler(
    "normalize-quality-name",
    ExtFFmpegFreeUIBehaviors.ParametersVideoQualityModeChanged,
    ExtPluginBehaviorPhase.AfterNative,
    context =>
    {
        if (context.Properties.TryGetValue("parameterName", out var name))
        {
            context.Properties["parameterName"] = name?.Trim();
        }
    })
{
    Order = 100
}));
```

深度插件把 `Phase` 改成 `ReplaceNative` 即可接管此行为点。宿主自动为替换处理器申请该行为资源的 `Exclusive` 租约；已有其他插件的有序变换或替换时，注册会抛出包含双方插件/声明 ID 的冲突异常。独占替换必须实现完整语义，并考虑空值、原生选项和所有安全插件选项。

### 替换原生 UI

确实需要整套自定义界面时，可以使用 `ExtPluginUiExtensionMode.ReplaceAnchor`。必须同时设置：

```csharp
Mode = ExtPluginUiExtensionMode.ReplaceAnchor,
ResourceId = ExtFFmpegFreeUIPluginResources.ParametersVideoQualityModeControl,
ResourceAccess = ExtPluginResourceAccess.Exclusive
```

宿主隐藏并禁用原生锚点，把插件控件放入相同布局位置；注销时恢复原生控件。`ReplaceAnchor` 只适用于装饰型原生控件锚点，且资源必须与锚点匹配；插入位置应继续使用默认模式。整个质量模式控件资源是 `mode.items` 和 `mode.behavior` 的父资源，因此替换整控件会与其他插件的安全下拉项和行为变换冲突。

若缺少所需行为点，可向开发者申请增加行为合同。

## 9. 完整处理链

### 9.1 总体时序

```text
加载预设到参数面板：
  ext.preset.before-apply
    → FFmpegFreeUI 把 PresetJson 映射到原生控件
    → 恢复各插件 StateJson，并触发 StateRestored
    → ext.preset.after-apply

从参数面板捕获预设：
  预取当前插件 StateJson
    → ext.preset.before-capture
    → FFmpegFreeUI 从全部原生控件捕获预设字段
    → 再次捕获插件 StateJson
    → ext.preset.after-capture

加入编码队列：
  ext.queue.before-add
    → 任务进入队列

任务实际启动：
  ext.task.before-prepare
    → 计算输入、输出和一个或多个步骤
    → 每个步骤生成命令：
        ext.command.before-build
          → FFmpegFreeUI 生成该步骤参数
          → 声明式参数提供器写入稳定位置
          → ext.command.after-build
    → 命令步骤提供器把 BeforeNative / AfterNative 步骤合入计划
    → ext.task.after-prepare
    → 如果任务数据变化，重新计算输出并重建全部步骤
    → 每个步骤依次执行：
        ext.process.before-start
          → 启动外部进程并等待退出
          → ext.process.after-exit

全部原生和声明式插件步骤成功：
  ext.task.after-complete
    → 全部成功后任务标记已完成
    → 任一后处理失败则任务转为错误

进入终态：
  错误：ext.task.after-failed
  成功、错误或用户取消：ext.task.after-finish
```

`command.*` 和 `process.*` 会按步骤重复；参数预览、参数总览、任务重建和二次编码也可能重复生成命令。
相应处理器必须幂等，不能在预览中执行计费、上传、删除文件等一次性副作用。

### 9.2 14 个阶段及其准确作用

| 阶段 | 位置与同步性 | 修改后会被宿主消费的主要内容 | 典型用途和注意事项 |
|---|---|---|---|
| `PresetBeforeApply` / `ext.preset.before-apply` | 同步；预设写入任何原生控件之前 | `PresetJson` | 迁移旧状态、补默认值、把自定义表示转换成原生字段。此时修改会影响随后原生界面映射。 |
| `PresetAfterApply` / `ext.preset.after-apply` | 同步；原生控件和插件状态已经恢复 | 主要作为通知点 | 此时再改 `PresetJson` 不会自动重新映射到界面。插件控件应通过 `StateRestored` 恢复。 |
| `PresetBeforeCapture` / `ext.preset.before-capture` | 同步；宿主尚未从原生控件捕获字段 | 尚未被后续原生捕获覆盖的辅助内容 | 编码器、质量等原生字段随后可能被控件值覆盖；需要最终覆盖时使用 `ext.preset.after-capture`。插件状态还会再次从 UI 捕获。 |
| `PresetAfterCapture` / `ext.preset.after-capture` | 同步；完整原生字段和插件状态均已捕获 | `PresetJson` | 入队前规范化、校验和最终覆盖预设字段的可靠位置。调用频率可能很高，禁止耗时工作。 |
| `QueueBeforeAdd` / `ext.queue.before-add` | 同步；任务对象已创建、尚未进入队列 | `PresetJson`、`InputPath`、`OutputPath`、`CommandLine`、`Properties["taskName"]` | 快速修改新任务快照或显示名。预设任务和纯命令行任务都会触发，后者 `PresetJson` 可能为空。 |
| `TaskBeforePrepare` / `ext.task.before-prepare` | 异步；输出名和步骤生成之前 | `PresetJson`、`InputPath`、`OutputPath`、`CommandLine` | 媒体探测、网络查询、自动选择参数的首选阶段。修改后宿主据此重算输出并构建步骤；必须响应取消。 |
| `CommandBeforeBuild` / `ext.command.before-build` | 同步；每个步骤开始生成参数之前 | 本次构建使用的 `PresetJson`、`InputPath`、`OutputPath` | 对当前 `PhaseName` 做结构化参数调整。`CommandLine` 此时尚未生成；修改通常只作用于本次命令构建。 |
| `CommandAfterBuild` / `ext.command.after-build` | 同步；当前步骤完整参数字符串已生成 | `CommandLine` | 最后追加、删除或重排参数。字符串处理容易破坏引号、路径和映射，优先在更早阶段改结构化预设。其他字段不会触发本次重建。 |
| `TaskAfterPrepare` / `ext.task.after-prepare` | 异步；全部步骤首次生成、任何进程启动之前 | `PresetJson`、`InputPath`、`OutputPath`、`CommandLine` | 最终验证或必要的任务修正。上述任务数据变化时，宿主会应用修改并重建全部步骤，所以 `command.*` 会再次运行。 |
| `ProcessBeforeStart` / `ext.process.before-start` | 异步；进程 `StartInfo` 已创建但尚未 `Start()` | `ProcessFileName`、`CommandLine` | 替换可执行文件、包装命令或做最后一刻调整。不要修改 `PresetJson` 并期待重建。 |
| `ProcessAfterExit` / `ext.process.after-exit` | 异步；每个进程退出、宿主判断步骤成败之前 | `ExitCode` | 读取真实退出码、清理单步骤文件，或按明确的外部工具协议校正退出码。它不是整个任务完成事件。 |
| `TaskAfterComplete` / `ext.task.after-complete` | 异步且一次性；全部原生及声明式插件步骤成功、任务标记完成之前 | `ReportProgress`、`ReportResult`；路径和预设主要供读取 | VMAF、校验和、输出验证等可取消成功后处理的兼容阶段。抛错会让任务转为错误，之后调用 `ext.task.after-failed` 和 `ext.task.after-finish`。回调内自行启动的进程不会经过 `process.*`；需要统一托管时改用命令步骤提供器。 |
| `TaskAfterFailed` / `ext.task.after-failed` | 异步且一次性；任务已确定错误后 | 主要供读取，可 `ReportResult` | 上报失败诊断、保留或清理插件文件。用户取消不触发。使用不可取消令牌，应快速返回；异常只写终态日志，但会中断本阶段后续处理器。 |
| `TaskAfterFinish` / `ext.task.after-finish` | 异步且一次性；成功、错误或取消的专用阶段之后 | `TaskStatus`、路径供读取，可 `ReportResult` | 无论终态如何都执行的最终清理点，适合释放以 `TaskId` 为键的缓存。使用不可取消令牌，不要执行无限或长时间等待。 |

阶段相邻不代表所有调用构成一条只执行一次的直线。`preset.*` 围绕参数面板，`queue.*` 围绕入队，`task.*` 围绕一次实际执行，而 `command.*` / `process.*` 围绕每个步骤。

## 10. `ExtPluginPipelineContext` 全部字段

宿主为每个处理器创建 SDK 上下文，插件通常不需要自行调用其构造函数。该类保留一个只接收进度回调的构造函数，以及一个同时接收进度和结构化结果回调的构造函数，主要供宿主实现、独立适配器或插件单元测试创建上下文；正常管线回调应直接使用宿主传入的实例。

| 字段 | 含义与写入规则 |
|---|---|
| `StageId` | 当前阶段 ID。宿主在调用前设置，插件只读使用。 |
| `PresetJson` | 完整 v6 预设 JSON。替换时必须保留未知字段及其他插件在 `插件扩展数据` 中的键。纯命令行任务可能为空。 |
| `InputPath` | 当前输入路径。是否会影响任务取决于阶段表。 |
| `OutputPath` | 当前输出路径。修改后应考虑自动命名、扩展名和容器一致性。 |
| `CommandLine` | FFmpeg/ffprobe 参数字符串，不包含 `ProcessFileName`。 |
| `ProcessFileName` | 实际启动的程序名，主要在 `ext.process.before-start` 中有效。 |
| `TaskId` | 实际任务标识；预览和纯 UI 上下文可能为空。用于区分并发任务。 |
| `SurfaceId` | 参数面板实例标识，主要在预设/UI 阶段有意义。 |
| `PhaseName` | 当前命令或进程阶段名称，例如普通单次或二次编码步骤。 |
| `IsPreview` | 当前命令生成是否为预览。预览处理器必须避免外部副作用。 |
| `ExitCode` | 可空退出码；`ext.process.after-exit` 中有实际值，并可覆盖宿主随后判断使用的值。 |
| `TaskStatus` | `Unknown`、`Pending`、`Running`、`Paused`、`Succeeded`、`Failed`、`Canceled`。只读使用；赋值不会改变 FFmpegFreeUI 的真实状态。 |
| `Properties` | 不区分大小写的阶段附加字典；处理器应保留不属于自己的键。 |

当前公开的 `Properties` 键：

| 键 | 可用位置 | 含义 |
|---|---|---|
| `taskName` | `ext.queue.before-add` | 任务显示名称；修改会被宿主读取。 |
| `stepCount` | 任务/进程上下文 | 总步骤数；早期准备阶段可能是 `0`。 |
| `stepIndex` | 有当前步骤时 | 从 `0` 开始的步骤索引。 |
| `stepNumber` | 有当前步骤时 | 从 `1` 开始的步骤序号。 |
| `isFinalStep` | 有当前步骤时 | 小写字符串 `true` / `false`。 |
| `commandStage` | `ext.process.before-start`、`ext.process.after-exit` | 宿主命令步骤枚举名。 |
| `isPluginStep` | `ext.process.before-start`、`ext.process.after-exit` | 是否为 v2.3 声明式插件步骤。 |
| `pluginId` / `pluginProviderId` / `pluginStepId` | 同上；仅插件步骤有非空值 | 当前命令步骤来源。 |
| `elapsedMilliseconds` | `ext.task.after-complete`、`ext.task.after-failed`、`ext.task.after-finish` | 当前执行经过的毫秒数。 |

插件可以在 `Properties` 中加入自己的临时键，后面的同阶段处理器能看到，但宿主不会持久化未知键，也不保证在另一个阶段重新提供它们。跨阶段数据应放在插件拥有的预设状态，或放在以 `TaskId` 为键的插件内部线程安全缓存中，并在 `ext.task.after-finish` 清理。

## 11. 修改 `PresetJson` 的安全方式

`PresetJson` 是整个 v6 预设，不属于单个插件。插件不得把它替换为只包含自己字段的新对象，也不得删除未知字段。

插件 UI 状态在完整预设中的结构类似：

```json
{
  "插件扩展数据": {
    "com.example.my-plugin": "{\"Version\":1,\"Enabled\":true,\"Value\":\"32\"}"
  }
}
```

外层的 `插件扩展数据` 是“插件 ID → JSON 字符串”字典。插件只解释自己的值。

C# 中可使用 `JsonNode` 保留未知字段：

```csharp
var preset = JsonNode.Parse(context.PresetJson)?.AsObject()
    ?? throw new InvalidOperationException("预设 JSON 无效");

// 示例：写入宿主当前公开预设中的原生质量字段。
preset["视频参数_比特率_控制方式"] = 1;
preset["视频参数_质量控制_参数名"] = "crf";
preset["视频参数_质量控制_值"] = "32";

context.PresetJson = preset.ToJsonString(
    new JsonSerializerOptions { WriteIndented = true });
```

字段名属于当前 v6 预设格式，仍可能随宿主预设版本演进。生产插件应验证字段类型、为缺失字段提供默认值，并保留自己的状态版本号以便迁移。

## 12. 进度与结构化结果

### 12.1 `ReportProgress`

```csharp
context.ReportProgress("正在计算质量指标……", 0.25);
context.ReportProgress("质量指标计算完成", 1);
```

- `fraction` 可省略，范围是 `0` 到 `1`；宿主会限制越界值。
- 在实际任务上下文中，它会更新任务进度并向任务日志写入消息。
- UI、预设和预览上下文可能没有实际任务接收器，此时调用不会产生任务显示。

### 12.2 `ReportResult`

```csharp
context.ReportResult("quality.mean", "96.417", "质量均值");
context.ReportResult("output.bytes", "1234567", "输出大小", "bytes");
```

```vb
context.ReportResult("quality.mean", "96.417", "质量均值")
context.ReportResult("bitrate.average", "1842", "平均码率", "kbps")
```

参数：

| 参数 | 含义 |
|---|---|
| `key` | 插件内部稳定键，不能为空。 |
| `value` | 字符串值。 |
| `displayName` | 可选的人类可读标题。 |
| `unit` | 可选单位。 |

宿主按“插件 ID + `key`”隔离结果。因此两个插件都上报 `quality.mean` 不会冲突；同一插件对相同 `key`再次上报会更新原结果。结果会写入当前任务日志和结果摘要，属于当前一次任务执行，任务重新运行时清空。

当前上下文不提供读取其他插件结果集合的 API。插件之间如需协作，应定义明确的外部协议，不能依赖任务结果摘要作为实时通信总线。

## 13. 成功后处理示例

需要在全部原生及声明式插件步骤成功后计算质量分数、校验和或生成报告时，使用`ext.task.after-complete`，不要用 `ext.process.after-exit` 猜测“最后一个进程”。

```csharp
private static async ValueTask VerifyOutputAsync(
    ExtPluginPipelineContext context,
    CancellationToken cancellationToken)
{
    if (!File.Exists(context.OutputPath))
    {
        throw new FileNotFoundException("输出文件不存在", context.OutputPath);
    }

    context.ReportProgress("正在校验输出……", 0.5);
    await using var stream = new FileStream(
        context.OutputPath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        128 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    var hash = await SHA256.HashDataAsync(stream, cancellationToken)
        .ConfigureAwait(false);
    context.ReportResult(
        "output.sha256",
        Convert.ToHexString(hash).ToLowerInvariant(),
        "SHA-256");
    context.ReportProgress("输出校验完成", 1);
}
```

后处理必须：

- 把取消令牌传给异步 I/O 和外部进程等待；
- 用户停止任务时结束插件启动的整棵子进程树；
- 使用唯一临时文件名，最好包含插件 ID、`TaskId` 或随机 GUID；
- 在 `Finally` 中清理临时文件；
- 明确失败策略：处理器抛错会让原本编码成功的任务进入错误状态，但已经生成的输出文件不会自动删除。

VB.NET 中调用外部工具并解析 VMAF 的完整实现见[VB.NET 综合示例](../Samples/FFmpegFreeUI.Ext.PluginApi.VbVmafSample/VbVmafPlugin.Pipeline.vb)。

## 14. 取消、并发与线程安全

### 14.1 取消令牌

- `ext.task.before-prepare`、`ext.task.after-prepare`、`ext.process.before-start`、`ext.process.after-exit` 和
  `ext.task.after-complete` 接收当前任务取消令牌。
- 用户取消后应尽快停止插件的文件、网络和子进程工作。
- `ext.task.after-failed` 和 `ext.task.after-finish` 为保证清理而接收不可取消令牌；它们必须只做有界工作。
- 用户取消不会触发 `ext.task.after-failed`，但会触发 `ext.task.after-finish`，此时 `TaskStatus=Canceled`。

### 14.2 同一任务与不同任务

- 同一任务、同一阶段的插件处理器串行执行。
- 多个编码任务可以并行，所以同一个插件回调可能同时处理不同 `TaskId`。
- 不要用未加锁的全局字段保存“当前文件”“当前进程”或“当前分数”。
- 推荐使用 `ConcurrentDictionary<string, TaskState>`，以 `TaskId` 为键，并在 `ext.task.after-finish` 删除。
- 预览可能没有 `TaskId`，预览处理器不应创建必须依赖任务终态清理的长期状态。

### 14.3 UI 线程

UI 控件工厂和 UI 事件运行在宿主 UI 线程。任务/进程处理器不保证在 UI 线程；如果必须更新插件控件，应使用该控件的 `InvokeRequired` / `BeginInvoke` 切回创建线程。不要在 UI 事件中同步等待异步任务。

## 15. 多插件冲突与资源租约

`IExtPluginResourceRegistry` 在注册阶段协调同一宿主资源的访问。三种访问模式如下：

| 模式 | 能否与 `Observe` 共存 | 能否与其他 `OrderedTransform` 共存 | 能否与其他插件的 `Exclusive` 共存 |
|---|---:|---:|---:|
| `Observe` | 是 | 是 | 是；观察者不得写入资源 |
| `OrderedTransform` | 是 | 是，按各自 API 的稳定顺序组合 | 否 |
| `Exclusive` | 是 | 否 | 否 |

安全下拉项、行为处理器和多数处理链阶段会自动申请资源租约：

- `RegisterChoice` 自动申请对应下拉项集合的 `OrderedTransform`；若会覆盖/禁用字段，还申请质量字段资源。
- `BeforeNative` / `AfterNative` 自动申请行为资源的 `OrderedTransform`。
- `ReplaceNative` 自动申请行为资源的 `Exclusive`。
- 预设、命令和任务后处理阶段分别自动映射到 `ext.preset.document`、`ext.command.line`、`ext.task.after-processing`。
- 声明式参数/步骤提供器分别自动申请 `ext.command.arguments` / `ext.command.plan` 的 `OrderedTransform`。
- `ReplaceAnchor` 要求插件显式指定父资源并使用 `Exclusive`。
- 直接通过 `AnchorControl` 修改原生控件时，插件必须用控件描述符的 `ResourceId` 手动 `Claim`；宿主无法从任意用户代码自动推断所有写操作。

资源冲突只比较不同插件。同一插件可以把一项功能拆成多个内部处理器；它们仍按 `Order` 执行。声明冲突会使后加载插件初始化失败，并显示资源 ID、双方插件 ID 和声明 ID，不会静默覆盖。

示例：原始控件只读监听声明 `Observe`；会设置质量值的按钮声明：

```csharp
_registrations.Add(host.Resources.Claim(new ExtPluginResourceClaim(
    "write-quality-fields",
    ExtFFmpegFreeUIPluginResources.ParametersVideoQualityFields,
    ExtPluginResourceAccess.OrderedTransform)
{
    Purpose = "用户点击按钮时写入质量参数名和值"
}));
```

租约解决的是“谁被允许修改”，不能自动合并所有业务语义。可组合处理器仍应保留未知 JSON/字典键；处理器异常仍会中止同一阶段剩余处理器；外部文件应使用插件 ID + `TaskId` + GUID 隔离，并对网络、子进程和锁设置超时。

插件与 FFmpegFreeUI 在同一进程、同一用户权限下运行，不是安全沙箱。只安装可信插件；插件崩溃、死锁或修改共享文件都有可能影响宿主。

## 16. 选择阶段的快速规则

- 只保存 UI 设置：使用 `StateJson`，通常不需要处理器。
- 加载旧预设前迁移字段：`ext.preset.before-apply`。
- 原生控件完成捕获后最终修正预设：`ext.preset.after-capture`。
- 每个文件入队时快速改任务名或任务快照：`ext.queue.before-add`。
- 需要媒体探测、网络查询或外部工具后再决定编码参数：`ext.task.before-prepare`。
- 只需向 FFmpeg 稳定位置追加参数：v2.3 `RegisterParameterProvider`。
- 需要在原生编码前后执行且应进入预览/队列管理的外部程序：v2.3 `RegisterStepProvider`。
- 需要结构化调整当前步骤参数：`ext.command.before-build`。
- 只能修改最终参数字符串：`ext.command.after-build`。
- 全部步骤生成后必须做最终验证：`ext.task.after-prepare`。
- 必须替换实际启动程序：`ext.process.before-start`。
- 必须观察或校正真实进程退出码：`ext.process.after-exit`。
- 全部编码步骤成功后做耗时评测或校验：`ext.task.after-complete`。
- 只处理错误任务：`ext.task.after-failed`。
- 无论成功、错误还是取消都释放任务缓存：`ext.task.after-finish`。

## 17. 构建、安装和调试

### 17.1 构建

```powershell
dotnet restore .\MyCompany.MyPlugin.csproj
dotnet build .\MyCompany.MyPlugin.csproj -c Debug --no-restore
```

VB.NET 项目把扩展名改为 `.vbproj`。

### 17.2 一键部署

仓库提供 `ExtDeployFFmpegFreeUIPlugin` MSBuild 目标，作用类似 Maven 的插件部署阶段：它先执行 `Build`，再把入口 `*.3fui.dll`、`ReferenceCopyLocalPaths` 中的插件依赖、运行时资源、标记为复制到输出目录的内容，以及可选 PDB 增量复制到目标程序的 `Plugin` 目录。

使用 `ProjectReference` 或 DLL 引用时，在插件项目末尾导入目标：

```xml
<Import Project="路径\FFmpegFreeUI.Ext.PluginSdk.Deploy.targets" />
```

使用 SDK `PackageReference` 时该目标由包自动导入，不需要这行。然后在当前 PowerShell 会话配置一次 FFmpegFreeUI 根目录：

```powershell
$env:EXT_FFMPEGFREEUI_INSTALL_DIR = "D:\Apps\FFmpegFreeUI-API-Extended-Edition"
```

一条命令完成还原后的编译和部署：

```powershell
dotnet build .\MyCompany.MyPlugin.csproj `
  -c Debug --no-restore -t:ExtDeployFFmpegFreeUIPlugin
```

也可以不设置环境变量，直接传 MSBuild 属性：

```powershell
dotnet build .\MyCompany.MyPlugin.csproj `
  -c Release -t:ExtDeployFFmpegFreeUIPlugin `
  -p:ExtFFmpegFreeUIInstallDir="D:\Apps\FFmpegFreeUI-API-Extended-Edition"
```

如果希望在 IDE 中每次普通构建后自动部署，可在只供本机使用的项目配置中加入：

```xml
<PropertyGroup>
  <ExtFFmpegFreeUIAutoDeploy>true</ExtFFmpegFreeUIAutoDeploy>
</PropertyGroup>
```

建议把安装路径放在环境变量或不提交版本控制的本机配置中，不要把个人绝对路径提交到公共插件仓库。Debug 默认部署 PDB，Release 默认不部署；可用 `ExtFFmpegFreeUIDeploySymbols=true/false` 覆盖。

部署目标的安全规则：

- 要求目标根目录存在 `FFmpegFreeUI.exe`、匹配的 Ext SDK 和 PluginHost，防止复制到错误目录；
- 要求插件 `AssemblyName` 以 `.3fui` 结尾；
- 不复制 SDK、PluginHost、FFmpegFreeUI 主程序集或 LakeUI 的插件私有副本；
- 不清空 `Plugin`，也不删除其他插件，只覆盖本次同名输出并跳过未变化文件；
- 依赖被删除或改名后，旧文件不会自动清理，需要开发者确认目标后手动移除；
- 部署前应退出 FFmpegFreeUI，否则正在使用的 DLL 可能被锁定并导致复制失败。

两个仓库示例已经导入该目标，可以直接用于验证：

```powershell
dotnet build .\Samples\FFmpegFreeUI.Ext.PluginApi.Sample\FFmpegFreeUI.Ext.PluginApi.Sample.csproj `
  -c Release -t:ExtDeployFFmpegFreeUIPlugin `
  -p:ExtFFmpegFreeUIInstallDir="D:\Apps\FFmpegFreeUI-API-Extended-Edition"
```

### 17.3 手动安装

1. 完全退出 FFmpegFreeUI。
2. 确认程序根目录存在匹配版本的 `FFmpegFreeUI.Ext.PluginHost.dll` 和
   `FFmpegFreeUI.Ext.PluginSdk.dll`。
3. 只把 `MyCompany.MyPlugin.3fui.dll` 和插件独有依赖复制到 `Plugin`。
4. 重新启动 FFmpegFreeUI。

不要把示例或插件输出目录中的 SDK 再复制一份到 `Plugin`。

### 17.4 调试

推荐在 IDE 中将启动程序设置为 `FFmpegFreeUI.exe`，在下面位置设置断点：

- 插件 `Initialize`；
- UI 控件工厂；
- `StateRestored`；
- 各管线回调；
- 外部进程启动和取消处理。

`host.Log` 当前通过 `Debug.WriteLine` 输出，可在调试器“输出”窗口查看。任务阶段的
`ReportProgress` / `ReportResult` 可在编码队列任务日志中查看。

发布包应说明：

- 所需 FFmpegFreeUI 和 Ext Plugin API 版本；
- 安装及卸载方法；
- 插件自己的托管和原生依赖；
- 是否启动网络请求或外部程序；
- 临时文件和隐私策略；
- 插件以及第三方库各自适用的许可证。

## 18. 常见问题

### 插件完全没有加载

依次检查：

1. 文件名是否以 `.3fui.dll` 结尾；
2. DLL 是否位于程序根目录下的 `Plugin`；
3. 根目录是否同时存在兼容的 PluginHost 和 SDK；
4. 入口是否公开、非抽象、实现 `IExtFFmpegFreeUIPlugin` 并有公共无参构造函数；
5. 插件 ID 是否为空或与其他插件重复；
6. 插件自己的依赖是否齐全；
7. `Initialize` 是否抛出异常。

### 没有 PluginSdk 时为什么不报错

这是预期行为。FFmpegFreeUI 在加载插件程序集之前读取其程序集引用表；发现插件依赖 SDK 而 v2 宿主不可用时，
直接跳过该插件，避免触发类型加载错误。

### UI 注册成功但页面没有出现控件

- 确认注册的是当前宿主提供的锚点；
- 打开“参数面板 → 视频参数｜质量”让相应界面实例创建；
- 插入型工厂必须返回一个新控件；
- 装饰型工厂必须返回空，它不会自动显示新的一行；
- 检查工厂是否抛出异常；
- 不要复用已经属于另一个父容器的控件实例。

### 为什么添加原生下拉项后预设变成“未选择”

不要直接修改 `AnchorControl.Items`。改用 `RegisterChoice` 并给出稳定 `choiceId` 和原生 `nativeFallbackChoiceId`。宿主会让原生联动、预设捕获、插件状态恢复和注销清理使用同一语义映射。

### 两个插件都要修改同一个原生逻辑怎么办

只需校正输入/输出时，都注册 `BeforeNative` 或 `AfterNative`，宿主按稳定顺序串行组合。必须完整代替原生实现时使用 `ReplaceNative`；它会取得独占租约，第二个深度插件会在加载时收到明确冲突，而不是运行时随机覆盖。

### 没有我需要的行为点，能否直接注入私有方法

不建议。先在 FFmpegFreeUI 核心增加一个只暴露必要属性的稳定行为点，再由插件注册处理器。反射私有成员或 IL 补丁绕过版本合同、资源冲突和注销回滚，更新宿主后很容易失效。

### 为什么 `ext.preset.before-capture` 写入的质量值消失了

该阶段之后宿主才从原生控件捕获质量等字段，因此会被控件当前值覆盖。改为直接协调公开的原生控件，或在`ext.preset.after-capture` 对完整预设做最终修正。

### 为什么 `ext.preset.after-apply` 修改 JSON 后界面没变化

原生映射已经结束，不会自动执行第二遍。需要改变加载到原生控件的字段时使用`ext.preset.before-apply`；需要恢复插件控件时监听 `StateRestored`。

### 为什么同步阶段提示处理器不能异步等待

预设、入队和命令构建阶段要求返回已经完成的 `ValueTask`。把耗时或异步工作移到`ext.task.before-prepare`、`ext.task.after-prepare` 或其他异步任务阶段。

### 为什么 `command.*` 执行很多次

参数预览、总览、任务准备、任务重建、ffprobe 和二次编码都会生成命令。检查 `IsPreview` 和`PhaseName`，保持修改幂等，不要在这些阶段执行一次性外部副作用。

### 插件参数为什么没有出现在预览或命令模板

不要只在按钮点击后私下保存一段字符串，也不要只在 `process.before-start` 临时改参数。把状态写入 `StateJson`，调用 `RequestParameterRefresh()`，再由 `RegisterParameterProvider` 根据 `PluginStateJson` 返回参数。三条链路会得到同一结果。“完全自己写”模板应加入对应的 `<ext:...>` 位置标记。

### 插件要执行自定义命令，应该在哪个回调启动

需要作为编码流程一部分的命令不要在预览、`command.*` 或 UI 回调中启动，应由 `RegisterStepProvider` 返回 `ExtPluginCommandStep`。只有不能表示为固定前/后步骤的复杂成功后处理才继续使用 `ext.task.after-complete` 自行管理进程；后者需要插件自己处理输出、取消、超时和清理。

### 如何给音频参数页或其他页面增加控件

直接从 `host.ParameterPanel.AvailablePages` 找到页面，再把普通 `ExtPluginUiExtension` 注册到 `TopAnchorId` 或 `BottomAnchorId`。需要修改已有控件时从 `AvailableControls` 找描述符，并注册到其 `AnchorId`；不要反射宿主私有字段。

### 编码完成后计算 VMAF 或校验和用哪个阶段

使用 `ext.task.after-complete`。它在全部原生及声明式插件步骤成功后调用一次，并在后处理成功之前不把任务标记为完成。使用 `ReportProgress` 展示进度，使用 `ReportResult` 发布最终分数。

### 多个 `ext.task.after-finish` 插件会并发吗

对同一任务不会，它们按 `Order`、插件 ID、处理器 ID 串行执行。但是一个插件异常会中止该阶段剩余插件，一个插件长时间等待也会阻塞后续插件。不同编码任务仍可能并行执行同一处理器。

### 修改 `TaskStatus` 能改变任务结果吗

不能。该属性只用于读取宿主状态，宿主不会把插件赋值复制回真实任务。需要让成功后处理失败时，将异常抛出；需要校正单个外部进程结果时，只能在有明确协议的情况下修改 `ext.process.after-exit` 的 `ExitCode`。

### 能否读取其他插件通过 `ReportResult` 发布的结果

当前不能。结果按插件 ID 隔离并供任务日志/摘要展示，管线上下文没有暴露结果集合。

### 插件可以用 VB.NET 编写吗

可以。任何能够生成兼容 .NET 程序集并实现 SDK 接口的语言原则上都可用。VB.NET 的主要差异是异步`ValueTask` 回调需要使用本指南中的 `Task` 适配器。

## 19. 公共类型速查

| 类型 | 用途 |
|---|---|
| `IExtFFmpegFreeUIPlugin` | 插件入口：`Id`、`DisplayName`、`Initialize`。 |
| `IExtFFmpegFreeUIHost` | 版本、日志、UI、处理链、行为点、资源、参数面板和命令注册表。 |
| `IExtPluginParameterPanelCatalog` | 当前全部参数页面和原生控件描述符。 |
| `ExtPluginParameterPageDescriptor` / `ExtPluginParameterControlDescriptor` | 页面插槽、控件锚点、类型、默认值属性和资源 ID。 |
| `ExtPluginControlAccess` | 不依赖 LakeUI 的公共控件属性读写辅助器。 |
| `ExtPluginLogLevel` | `Trace`、`Information`、`Warning`、`Error`。 |
| `IExtPluginUiRegistry` | `AvailableAnchors`、`AvailableChoiceAnchors`、`Register`、`RegisterChoice`。 |
| `ExtPluginUiExtension` | 普通/替换 UI 扩展、锚点、顺序、控件工厂和资源声明。 |
| `ExtPluginUiChoiceExtension` | 稳定下拉项、原生回退、字段覆盖、禁用目标和状态回调。 |
| `IExtPluginUiChoiceContext` | 安全选项身份、选择状态、插件状态和刷新。 |
| `IExtPluginUiContext` | UI 身份、原生控件、插入容器、状态持久化和刷新。 |
| `IExtPluginBehaviorRegistry` | 可用行为点和行为处理器注册。 |
| `ExtPluginBehaviorHandler` | `BeforeNative`、`AfterNative` 或独占 `ReplaceNative`。 |
| `ExtPluginBehaviorContext` | 行为点的稳定键值上下文。 |
| `IExtPluginResourceRegistry` | 共享资源声明和冲突检查。 |
| `ExtPluginResourceClaim` | 资源 ID、访问模式和用途说明。 |
| `IExtPluginPipelineRegistry` | `AvailableStages`、`Register`。 |
| `IExtPluginCommandRegistry` | 注册声明式 FFmpeg 参数提供器和外部命令步骤提供器。 |
| `ExtPluginCommandContext` | 预设、插件状态、路径、阶段、预览标记以及参数/步骤输出集合。 |
| `ExtPluginCommandArgument` / `ExtPluginCommandArgumentPosition` | FFmpeg 参数片段及五个稳定插入位置。 |
| `ExtPluginCommandStep` / `ExtPluginCommandStepPlacement` | 队列托管的前置/后置外部进程步骤。 |
| `ExtFFmpegFreeUICommandPlaceholders` | “完全自己写”模板中的五个可选声明式参数插槽。 |
| `ExtPluginPipelineHandler` | 处理器 ID、阶段、顺序和回调。 |
| `ExtPluginPipelineCallback` | 返回 `ValueTask` 的处理器委托。 |
| `ExtPluginPipelineContext` | 预设、路径、命令、进程、任务、属性、进度和结果。 |
| `ExtPluginPipelineProgress` | 宿主内部传递进度消息和比例的记录类型。 |
| `ExtPluginTaskResult` | 宿主内部传递结构化结果的记录类型。 |
| `ExtPluginTaskStatus` | `Unknown`、`Pending`、`Running`、`Paused`、`Succeeded`、`Failed`、`Canceled`。 |
| `ExtFFmpegFreeUIPluginApi` | 当前 SDK 声明版本 `Version`。 |
| `ExtFFmpegFreeUIUiAnchors` | 6 个 v2.2 兼容 UI 锚点和 `All` 集合。 |
| `ExtFFmpegFreeUIParameterPanelIds` | v2.3 页面、控件锚点和动态资源 ID 规则。 |
| `ExtFFmpegFreeUIUiChoiceAnchors` / `ExtFFmpegFreeUIUiChoices` | 可安全扩展的下拉框和原生选项稳定 ID。 |
| `ExtFFmpegFreeUIBehaviors` | 可组合或独占替换的原生行为点。 |
| `ExtFFmpegFreeUIPluginResources` | 可进行冲突协调的共享资源。 |
| `ExtFFmpegFreeUIPipelineStages` | 14 个稳定处理阶段和 `All` 集合。 |

建议先编译并运行仓库中的 C# 或 VB.NET 综合示例，再从最接近自己用途的 UI 扩展和处理阶段逐步删减，这样最容易保持正确的生命周期、取消和并发行为。
