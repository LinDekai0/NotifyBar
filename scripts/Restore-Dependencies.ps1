[CmdletBinding(SupportsShouldProcess = $true)]
param([string]$DotnetPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
if (!$DotnetPath) {
    $candidates = @()
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($installed) { $candidates += $installed.Source }
    $candidates += 'D:\JetBrains Rider 2024.1.5\lib\ReSharperHost\windows-x64\dotnet\dotnet.exe'
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (!(Test-Path -LiteralPath $candidate)) { continue }
        $sdks = @(& $candidate --list-sdks)
        if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match '^8\.' })) { $DotnetPath = $candidate; break }
    }
}
if (!$DotnetPath -or !(Test-Path -LiteralPath $DotnetPath)) { throw '找不到 .NET 8 SDK。请安装 .NET 8 SDK，或用 -DotnetPath 指定 dotnet.exe 完整路径。' }
if (!$PSCmdlet.ShouldProcess($projectRoot, '从官方 NuGet 源恢复依赖并构建、运行测试；不安装应用')) { return }

$environmentNames = @('DOTNET_CLI_HOME','DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_ADD_GLOBAL_TOOLS_TO_PATH','NUGET_PACKAGES')
$previous = @{}
foreach ($name in $environmentNames) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
    $logs = Join-Path $projectRoot 'artifacts\setup-logs'
    New-Item -ItemType Directory -Force $logs | Out-Null
    $solution = Join-Path $projectRoot 'NotificationBarrage.sln'
    $config = Join-Path $projectRoot 'NuGet.Config'
    Write-Host "使用 SDK：$DotnetPath"
    & $DotnetPath restore $solution --configfile $config --packages $env:NUGET_PACKAGES --disable-parallel 2>&1 | Tee-Object -FilePath (Join-Path $logs 'restore.log')
    if ($LASTEXITCODE -ne 0) { throw "依赖恢复失败。请提供 $logs\restore.log 的错误信息；不必先手工逐个下载包。" }
    & $DotnetPath test $solution -c Release --no-restore 2>&1 | Tee-Object -FilePath (Join-Path $logs 'test.log')
    if ($LASTEXITCODE -ne 0) { throw "构建或测试失败。依赖可能已恢复，请提供 $logs\test.log 的错误信息。" }
    Write-Host '依赖恢复、Release 编译及测试通过。真实消息仍需 MSIX 包身份和通知访问授权。'
    Write-Host "生成 MSIX：& '$projectRoot\scripts\Build-Msix.ps1' -DotnetPath '$DotnetPath'"
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
