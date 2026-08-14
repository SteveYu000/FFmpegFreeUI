<#
.SYNOPSIS
    发布 FFmpegFreeUI API Extended Edition 的框架依赖单文件版本。

.DESCRIPTION
    默认发布 win-x64。主程序会打包为单个 EXE，但不会包含 .NET 运行时。
    可选择发布 win-arm64、同时发布两个架构、启用单文件压缩，或附带 Ext Plugin API v2 组件。

.EXAMPLE
    .\Tools\发布框架依赖单文件.ps1

.EXAMPLE
    .\Tools\发布框架依赖单文件.ps1 -Architecture all -IncludeExtPluginApi -Compress

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Tools\发布框架依赖单文件.ps1 -Architecture arm64
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("x64", "arm64", "all")]
    [string]$Architecture = "x64",

    [Parameter()]
    [switch]$IncludeExtPluginApi,

    [Parameter()]
    [switch]$Compress,

    [Parameter()]
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$仓库根目录 = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$主项目 = Join-Path $仓库根目录 "FFmpegFreeUI\FFmpegFreeUI.vbproj"
$插件宿主项目 = Join-Path $仓库根目录 "FFmpegFreeUI.Ext.PluginHost\FFmpegFreeUI.Ext.PluginHost.vbproj"
$插件SDK项目 = Join-Path $仓库根目录 "FFmpegFreeUI.Ext.PluginSdk\FFmpegFreeUI.Ext.PluginSdk.csproj"
$输出根目录 = Join-Path $仓库根目录 "artifacts\FrameworkDependentSingleFile"
$配置 = "Release"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$FailureMessage
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage（dotnet 退出代码：$LASTEXITCODE）"
    }
}

function Get-SingleBuildFile {
    param(
        [Parameter(Mandatory)]
        [string]$SearchRoot,

        [Parameter(Mandatory)]
        [string]$FileName
    )

    $文件 = @(
        Get-ChildItem -LiteralPath $SearchRoot -Recurse -File -Filter $FileName |
            Sort-Object LastWriteTimeUtc -Descending
    )

    if ($文件.Count -eq 0) {
        throw "未找到构建产物：$FileName"
    }

    return $文件[0].FullName
}

function Publish-Architecture {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("x64", "arm64")]
        [string]$TargetArchitecture,

        [AllowEmptyString()]
        [string]$插件宿主文件,

        [AllowEmptyString()]
        [string]$插件SDK文件
    )

    $运行标识 = "win-$TargetArchitecture"
    $输出目录 = Join-Path $输出根目录 $运行标识

    # 每次都重建对应输出目录，防止旧 DLL 混入不附带 Ext Plugin API 的版本。
    if (Test-Path -LiteralPath $输出目录) {
        Remove-Item -LiteralPath $输出目录 -Recurse -Force
    }
    New-Item -ItemType Directory -Path $输出目录 -Force | Out-Null

    $压缩值 = if ($Compress) { "true" } else { "false" }
    $发布参数 = @(
        "publish",
        $主项目,
        "-c", $配置,
        "-r", $运行标识,
        "--self-contained", "false",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:PublishTrimmed=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:EnableCompressionInSingleFile=$压缩值",
        "-o", $输出目录
    )

    if ($NoRestore) {
        $发布参数 += "--no-restore"
    }

    Write-Host ""
    Write-Host "正在发布 $运行标识 ..." -ForegroundColor Cyan
    Invoke-DotNet -Arguments $发布参数 -FailureMessage "发布 $运行标识 失败"

    $主程序 = Join-Path $输出目录 "FFmpegFreeUI.exe"
    if (-not (Test-Path -LiteralPath $主程序 -PathType Leaf)) {
        throw "发布命令已结束，但未找到主程序：$主程序"
    }

    if ($IncludeExtPluginApi) {
        Copy-Item -LiteralPath $插件宿主文件 -Destination (Join-Path $输出目录 "FFmpegFreeUI.Ext.PluginHost.dll") -Force
        Copy-Item -LiteralPath $插件SDK文件 -Destination (Join-Path $输出目录 "FFmpegFreeUI.Ext.PluginSdk.dll") -Force
    }

    Write-Host "发布完成：$输出目录" -ForegroundColor Green
    Get-ChildItem -LiteralPath $输出目录 -File |
        Sort-Object Name |
        ForEach-Object {
            Write-Host ("  {0,-36} {1,10:N2} MB" -f $_.Name, ($_.Length / 1MB))
        }
}

if (-not (Test-Path -LiteralPath $主项目 -PathType Leaf)) {
    throw "未找到主项目：$主项目"
}

$dotnet版本 = (& dotnet --version)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dotnet版本)) {
    throw "未找到 dotnet SDK，请先安装 .NET 10 SDK。"
}

$主版本文本 = ($dotnet版本.Trim() -split '\.')[0]
$主版本 = 0
if (-not [int]::TryParse($主版本文本, [ref]$主版本) -or $主版本 -lt 10) {
    throw "当前 dotnet SDK 版本为 $dotnet版本，本项目需要 .NET 10 SDK。"
}

$插件宿主文件 = ""
$插件SDK文件 = ""

if ($IncludeExtPluginApi) {
    Write-Host "正在构建 Ext Plugin API v2 组件 ..." -ForegroundColor Cyan
    $构建参数 = @("build", $插件宿主项目, "-c", $配置)
    if ($NoRestore) {
        $构建参数 += "--no-restore"
    }

    Invoke-DotNet -Arguments $构建参数 -FailureMessage "Ext Plugin API v2 组件构建失败"
    $插件宿主文件 = Get-SingleBuildFile `
        -SearchRoot (Join-Path $仓库根目录 "FFmpegFreeUI.Ext.PluginHost\bin\$配置") `
        -FileName "FFmpegFreeUI.Ext.PluginHost.dll"
    $插件SDK文件 = Get-SingleBuildFile `
        -SearchRoot (Join-Path $仓库根目录 "FFmpegFreeUI.Ext.PluginSdk\bin\$配置") `
        -FileName "FFmpegFreeUI.Ext.PluginSdk.dll"
}

$目标架构列表 = switch ($Architecture) {
    "all" { @("x64", "arm64") }
    default { @($Architecture) }
}

foreach ($目标架构 in $目标架构列表) {
    Publish-Architecture `
        -TargetArchitecture $目标架构 `
        -插件宿主文件 $插件宿主文件 `
        -插件SDK文件 $插件SDK文件
}

Write-Host ""
Write-Host "全部任务完成。输出根目录：$输出根目录" -ForegroundColor Green
Write-Host "目标电脑需要安装与架构匹配的 .NET Desktop Runtime 10。" -ForegroundColor Yellow
