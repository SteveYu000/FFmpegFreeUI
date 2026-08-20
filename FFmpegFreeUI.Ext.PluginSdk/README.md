# FFmpegFreeUI Ext Plugin SDK

这是 FFmpegFreeUI API Extended Edition 的 Ext Plugin API 编译期合同包。插件只需引用本 SDK，不要引用 `FFmpegFreeUI.exe` 或 `FFmpegFreeUI.Ext.PluginHost.dll`。

SDK 已发布到 [NuGet.org](https://www.nuget.org/packages/FFmpegFreeUI.Ext.PluginSdk)。在插件项目目录执行：

```powershell
dotnet add package FFmpegFreeUI.Ext.PluginSdk --version 2.3.0
```

然后确认插件项目至少包含：

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <AssemblyName>MyCompany.MyPlugin.3fui</AssemblyName>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="FFmpegFreeUI.Ext.PluginSdk"
                    Version="2.3.0"
                    PrivateAssets="all"
                    ExcludeAssets="runtime" />
</ItemGroup>
```

`PrivateAssets="all"` 避免 SDK 成为传递依赖，`ExcludeAssets="runtime"` 避免把 SDK 私有副本复制进插件输出。包内同时包含 XML API 文档和通过 `buildTransitive` 自动导入的一键部署目标。

可以为当前 PowerShell 会话配置 FFmpegFreeUI 根目录：

```powershell
$env:EXT_FFMPEGFREEUI_INSTALL_DIR = "D:\Apps\FFmpegFreeUI-API-Extended-Edition"
```

也可以写入当前用户环境变量，供以后打开的 IDE 和终端使用：

```powershell
[Environment]::SetEnvironmentVariable(
  "EXT_FFMPEGFREEUI_INSTALL_DIR",
  "D:\Apps\FFmpegFreeUI-API-Extended-Edition",
  "User")
```

重新打开 IDE 或终端后，执行：

```powershell
dotnet build -c Debug -t:ExtDeployFFmpegFreeUIPlugin
```

也可以不设置环境变量，按次指定目标目录：

```powershell
dotnet build -c Release -t:ExtDeployFFmpegFreeUIPlugin `
  -p:ExtFFmpegFreeUIInstallDir="D:\Apps\FFmpegFreeUI-API-Extended-Edition"
```

目标会还原并编译插件，把 `*.3fui.dll`、可复制依赖以及 Debug 符号增量复制到 FFmpegFreeUI 的 `Plugin` 目录；不会清空该目录，也不会部署 SDK、PluginHost、主程序或 LakeUI 的私有副本。部署前请完全退出 FFmpegFreeUI，以免 DLL 被占用。

完整开发环境、部署约定和 API 说明见 [Ext Plugin API v2 中文开发指南](https://github.com/SteveYu000/FFmpegFreeUI-API-Extended-Edition/blob/main/doc/Ext-Plugin-API-v2.zh-CN.md)。
