param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sdkLines = & $DotnetPath --list-sdks
if ($LASTEXITCODE -ne 0 -or !$sdkLines) { throw '找不到 .NET 8 SDK，请通过 -DotnetPath 指定 dotnet.exe。' }
$sdkLine = @($sdkLines | Where-Object { $_ -match '^8\.' })[-1]
if ($sdkLine -notmatch '^(\S+) \[(.+)\]$') { throw '需要 .NET 8 SDK。' }
$sdkDir = Join-Path $Matches[2] $Matches[1]
$dotnetRoot = Split-Path (Split-Path $sdkDir -Parent) -Parent
$refPack = Get-ChildItem (Join-Path $dotnetRoot 'packs/Microsoft.NETCore.App.Ref') -Directory | Where-Object Name -Like '8.*' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
$outDir = Join-Path $projectRoot 'artifacts/checks'
New-Item -ItemType Directory -Force $outDir | Out-Null
$assembly = Join-Path $outDir 'OfflineChecks.dll'
$usings = Join-Path $outDir 'GlobalUsings.cs'
'global using System; global using System.IO; global using System.Linq; global using System.Collections.Generic; global using System.Threading; global using System.Threading.Tasks;' | Set-Content -Encoding UTF8 $usings
$argsFile = Join-Path $outDir 'compile.rsp'
$arguments = @('/nologo','/nostdlib+','/nullable:enable','/langversion:12','/target:exe',('/out:"'+$assembly+'"'))
$arguments += Get-ChildItem (Join-Path $refPack.FullName 'ref/net8.0/*.dll') | ForEach-Object { '/reference:"'+$_.FullName+'"' }
$sources = @(Get-ChildItem (Join-Path $projectRoot 'src/NotificationBarrage/Domain/*.cs') | ForEach-Object FullName)
$sources += @('Services/MessageFilter.cs','Services/BarrageQueue.cs','Services/SettingsStore.cs','Services/AppLogger.cs') | ForEach-Object { Join-Path $projectRoot ('src/NotificationBarrage/'+$_) }
$sources += Join-Path $projectRoot 'tests/OfflineChecks/Program.cs'
$sources += $usings
$arguments += $sources | ForEach-Object { '"'+$_+'"' }
$arguments | Set-Content -Encoding UTF8 $argsFile
& $DotnetPath (Join-Path $sdkDir 'Roslyn/bincore/csc.dll') /noconfig ('@'+$argsFile)
if ($LASTEXITCODE -ne 0) { throw '离线检查编译失败。' }
'{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}' | Set-Content -Encoding UTF8 (Join-Path $outDir 'OfflineChecks.runtimeconfig.json')
& $DotnetPath $assembly
if ($LASTEXITCODE -ne 0) { throw '离线行为检查失败。' }
