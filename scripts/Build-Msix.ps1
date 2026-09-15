param(
    [string]$DotnetPath = 'dotnet',
    [string]$WindowsSdkBin = '',
    [string]$CertificateThumbprint = '',
    [string]$CertificatePath = '',
    [string]$CertificatePassword = '',
    [ValidatePattern('^8\.0\.\d+$')][string]$RuntimeVersion = '8.0.31'
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path
if ($CertificateThumbprint -and $CertificatePath) { throw 'Choose CertificateThumbprint OR CertificatePath.' }
if (!$WindowsSdkBin) {
    $kits = Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles(x86)')) 'Windows Kits\10\bin'
    $sdk = Get-ChildItem $kits -Directory | Where-Object Name -Match '^10\.0\.' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
    if (!$sdk) { throw 'Install Windows SDK, or specify -WindowsSdkBin with the x64 SDK tools directory.' }
    $WindowsSdkBin = Join-Path $sdk.FullName 'x64'
}
foreach ($tool in @('MakeAppx.exe', 'SignTool.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $WindowsSdkBin $tool))) { throw "Missing SDK tool: $tool" }
}
$manifestPath = Join-Path $projectRoot 'packaging\Package.appxmanifest'
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$identity = $manifest.Package.Identity
if ($CertificateThumbprint) {
    $CertificateThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    if ($CertificateThumbprint -notmatch '^[A-F0-9]{40}$') { throw 'Invalid certificate thumbprint.' }
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
    if ($certificate.Subject -ne $identity.Publisher -or !$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) {
        throw 'Signing certificate must be valid, have a private key, and match the manifest Publisher.'
    }
}
$output = Join-Path $projectRoot 'artifacts\msix'
$staging = Join-Path $output ('staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $staging | Out-Null
$package = Join-Path $output ("NotificationBarrage-{0}-x64.msix" -f $identity.Version)
$environmentNames = @('DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'NUGET_PACKAGES')
$previous = @{}
foreach ($name in $environmentNames) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
    $config = Join-Path $projectRoot 'NuGet.Config'
    & $DotnetPath publish (Join-Path $projectRoot 'src\NotificationBarrage\NotificationBarrage.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false "-p:NotifyBarRuntimeVersion=$RuntimeVersion" "-p:RestoreConfigFile=$config" -o $staging
    if ($LASTEXITCODE -ne 0) { throw 'Full application publish failed; no new MSIX was generated.' }
    $runtime = Get-Content -LiteralPath (Join-Path $staging 'NotificationBarrage.runtimeconfig.json') -Raw | ConvertFrom-Json
    $frameworks = @($runtime.runtimeOptions.includedFrameworks)
    foreach ($frameworkName in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        if (!($frameworks | Where-Object { $_.name -eq $frameworkName -and $_.version -eq $RuntimeVersion })) {
            throw "Published runtime did not match $frameworkName $RuntimeVersion."
        }
    }
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $staging 'AppxManifest.xml')
    & (Join-Path $PSScriptRoot 'New-PackageAssets.ps1') -OutputDirectory (Join-Path $staging 'Assets')
    & (Join-Path $WindowsSdkBin 'MakeAppx.exe') pack /d $staging /p $package /o
    if ($LASTEXITCODE -ne 0) { throw 'MSIX manifest validation or packaging failed.' }
    $signed = $false
    if ($CertificateThumbprint -or $CertificatePath) {
        $signArgs = @('sign', '/fd', 'SHA256')
        if ($CertificateThumbprint) { $signArgs += @('/s', 'My', '/sha1', $CertificateThumbprint) }
        else {
            $signArgs += @('/f', (Resolve-Path -LiteralPath $CertificatePath).Path)
            if ($CertificatePassword) { $signArgs += @('/p', $CertificatePassword) }
        }
        & (Join-Path $WindowsSdkBin 'SignTool.exe') @signArgs $package
        if ($LASTEXITCODE -ne 0) { throw 'MSIX signing failed.' }
        $signature = Get-AuthenticodeSignature -LiteralPath $package
        if (!$signature.SignerCertificate -or $signature.SignerCertificate.Subject -ne $identity.Publisher -or ($CertificateThumbprint -and $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint)) {
            throw 'Package signer did not match the requested publisher/certificate.'
        }
        $signed = $true
    }
    $info = [ordered]@{
        Package = $package; Version = [string]$identity.Version; Runtime = $RuntimeVersion
        PublishDirectory = $staging; Signed = $signed
        Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    }
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build-info.json') -Encoding UTF8
    $info | ConvertTo-Json
    if (!$signed) { Write-Warning 'Package is unsigned. Sign it before installation.' }
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
