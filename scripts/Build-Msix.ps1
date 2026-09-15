param([string]$DotnetPath='dotnet', [string]$WindowsSdkBin='', [string]$CertificatePath='', [string]$CertificatePassword='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if (!$WindowsSdkBin) {
    $sdk=Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory | Where-Object Name -Match '^10\.0\.' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
    if (!$sdk) { throw '请安装 Windows 10/11 SDK，或用 -WindowsSdkBin 指定包含 MakeAppx.exe 和 SignTool.exe 的 x64 目录。' }
    $WindowsSdkBin=Join-Path $sdk.FullName 'x64'
}
$output=Join-Path $projectRoot 'artifacts/msix'
$staging=Join-Path $output ('staging-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $staging | Out-Null
$config=Join-Path $projectRoot 'NuGet.Config'
& $DotnetPath publish (Join-Path $projectRoot 'src/NotificationBarrage/NotificationBarrage.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "-p:RestoreConfigFile=$config" -o $staging
if ($LASTEXITCODE -ne 0) { throw '完整版发布失败；尚未生成 MSIX。请检查 .NET SDK、NuGet 网络和 Windows 引用包。' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging/Package.appxmanifest') -Destination (Join-Path $staging 'AppxManifest.xml')
& (Join-Path $PSScriptRoot 'New-PackageAssets.ps1') -OutputDirectory (Join-Path $staging 'Assets')
$package=Join-Path $output 'NotificationBarrage-1.0.0.0-x64.msix'
& (Join-Path $WindowsSdkBin 'MakeAppx.exe') pack /d $staging /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX 清单或打包校验失败。' }
if ($CertificatePath) {
    # Signing does not import or trust any certificate. Publisher must match the manifest.
    $signArgs=@('sign','/fd','SHA256','/f',$CertificatePath)
    if ($CertificatePassword) { $signArgs += @('/p',$CertificatePassword) }
    & (Join-Path $WindowsSdkBin 'SignTool.exe') @signArgs $package
    if ($LASTEXITCODE -ne 0) { throw '签名失败，请检查证书及 Publisher=CN=NotificationBarrage.Dev。' }
    Write-Host "已生成签名包：$package"
} else { Write-Host "已生成未签名包：$package。安装前需要签名；此脚本不会自动导入证书或安装应用。" }
