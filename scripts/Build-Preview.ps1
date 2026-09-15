param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sdkLine = @(& $DotnetPath --list-sdks | Where-Object { $_ -match '^8\.' })[-1]
if ($sdkLine -notmatch '^(\S+) \[(.+)\]$') { throw '找不到 .NET 8 SDK。可用 -DotnetPath 指定 dotnet.exe。' }
$sdkDir = Join-Path $Matches[2] $Matches[1]
$dotnetRoot = Split-Path (Split-Path $sdkDir -Parent) -Parent
$outDir = Join-Path $projectRoot 'artifacts/preview'
New-Item -ItemType Directory -Force $outDir | Out-Null
$assembly = Join-Path $outDir 'NotificationBarrage.Preview.dll'
$argsFile = Join-Path $outDir 'compile.rsp'
$arguments = @('/nologo','/nostdlib+','/nullable:enable','/langversion:12','/target:winexe','/platform:x64','/define:PREVIEW','/main:NotificationBarrage.Program',('/out:"'+$assembly+'"'),('/win32manifest:"'+(Join-Path $projectRoot 'src/NotificationBarrage/app.manifest')+'"'))
$refs = foreach ($packName in @('Microsoft.NETCore.App.Ref','Microsoft.WindowsDesktop.App.Ref')) {
    $pack = Get-ChildItem (Join-Path $dotnetRoot "packs/$packName") -Directory | Where-Object Name -Like '8.*' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
    Get-ChildItem (Join-Path $pack.FullName 'ref/net8.0/*.dll')
}
$arguments += $refs | Group-Object Name | ForEach-Object { '/reference:"'+$_.Group[-1].FullName+'"' }
$arguments += Get-ChildItem (Join-Path $projectRoot 'src/NotificationBarrage') -Filter '*.cs' -Recurse | Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } | ForEach-Object { '"'+$_.FullName+'"' }
$arguments | Set-Content -Encoding UTF8 $argsFile
& $DotnetPath (Join-Path $sdkDir 'Roslyn/bincore/csc.dll') /noconfig ('@'+$argsFile)
if ($LASTEXITCODE -ne 0) { throw '预览版编译失败。' }
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.WindowsDesktop.App","version":"8.0.0"}}}' | Set-Content -Encoding UTF8 (Join-Path $outDir 'NotificationBarrage.Preview.runtimeconfig.json')
Add-Type -Path (Join-Path $sdkDir 'Microsoft.NET.HostModel.dll')
$hostPack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Host.win-x64') -Directory | Where-Object Name -Like '8.*' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
$exe = Join-Path $outDir 'NotificationBarrage.Preview.exe'
[Microsoft.NET.HostModel.AppHost.HostWriter]::CreateAppHost((Join-Path $hostPack.FullName 'runtimes/win-x64/native/apphost.exe'), $exe, 'NotificationBarrage.Preview.dll', $true, $assembly, $false)
Write-Host "预览版编译完成（不读取系统通知）：$exe"
Write-Host '双击 EXE 可打开预览版。需要 .NET 8 Desktop Runtime；--demo 显示测试弹幕，--smoke 测试后自动退出。'
