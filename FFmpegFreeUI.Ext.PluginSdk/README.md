# FFmpegFreeUI Ext Plugin SDK

这是 FFmpegFreeUI API Extended Edition 的 Ext Plugin API 编译期合同包。插件只需引用本 SDK，不要引用 `FFmpegFreeUI.exe` 或 `FFmpegFreeUI.Ext.PluginHost.dll`。

插件项目至少需要：

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

包内的 `buildTransitive` 目标提供一键部署。配置 `EXT_FFMPEGFREEUI_INSTALL_DIR` 环境变量后执行：

```powershell
dotnet build -t:ExtDeployFFmpegFreeUIPlugin
```

目标会编译插件，把 `*.3fui.dll`、可复制依赖以及 Debug 符号增量复制到 FFmpegFreeUI 的 `Plugin` 目录；不会清空该目录，也不会部署 SDK、PluginHost、主程序或 LakeUI 的私有副本。

完整开发约定和 API 说明位于源码仓库的 `doc/Ext-Plugin-API-v2.zh-CN.md`。
