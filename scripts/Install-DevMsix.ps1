param([string]$PackagePath = '', [string]$CertificatePath = '', [switch]$TrustCertificate)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run this script using Windows PowerShell 5.1 (powershell.exe).' }
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$PackagePath) {
    $PackagePath = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'artifacts\msix') -Filter 'NotificationBarrage-*.msix' |
        Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName
    if (!$PackagePath) { throw 'No NotifyBar MSIX found. Build it first with scripts\Build-Msix.ps1.' }
}
if (!$CertificatePath) { $CertificatePath = Join-Path $projectRoot 'artifacts\msix\NotifyBar-Dev.cer' }
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$certificateFile = (Resolve-Path -LiteralPath $CertificatePath).Path
$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certificateFile)
if ($certificate.Subject -ne 'CN=NotificationBarrage.Dev' -or $certificate.HasPrivateKey -or $certificate.NotBefore -gt (Get-Date) -or $certificate.NotAfter -le (Get-Date)) {
    throw 'Expected a valid public NotifyBar development certificate (.cer).'
}
$eku = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' })
if (!$eku -or !($eku.EnhancedKeyUsages | Where-Object Value -eq '1.3.6.1.5.5.7.3.3')) { throw 'The certificate must allow code signing.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entry = $archive.GetEntry('AppxManifest.xml')
    if (!$entry) { throw 'Package has no AppxManifest.xml.' }
    $reader = New-Object IO.StreamReader($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
} finally { $archive.Dispose() }
$identity = $manifest.Package.Identity
if ($identity.Name -ne 'NotificationBarrage' -or $identity.Publisher -ne $certificate.Subject -or $identity.ProcessorArchitecture -ne 'x64') {
    throw 'This installer only accepts the NotifyBar x64 package.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $package
if (!$signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint -or $signature.Status -in @('HashMismatch', 'NotSigned', 'NotSupportedFileFormat', 'Incompatible')) {
    throw 'The package signature is missing, damaged, or does not match the supplied certificate.'
}
$trustPath = 'Cert:\LocalMachine\TrustedPeople\' + $certificate.Thumbprint
if (!(Test-Path -LiteralPath $trustPath)) {
    if (!$TrustCertificate) { throw 'Certificate not trusted. Review it, then run again with -TrustCertificate to request Windows UAC.' }
    # Embed this exact public certificate, avoiding path substitution across the UAC boundary.
    $trustTemplate = @'
$ErrorActionPreference = 'Stop'
try {
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList (,[Convert]::FromBase64String('__PUBLIC_CERTIFICATE__'))
    if ($cert.Thumbprint -ne '__THUMBPRINT__' -or $cert.Subject -ne 'CN=NotificationBarrage.Dev') { throw 'Certificate mismatch.' }
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPeople', 'LocalMachine')
    $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    try { $store.Add($cert) } finally { $store.Close() }
    exit 0
} catch { exit 1 }
'@
    $trustCommand = $trustTemplate.Replace('__PUBLIC_CERTIFICATE__', [Convert]::ToBase64String($certificate.RawData)).Replace('__THUMBPRINT__', $certificate.Thumbprint)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($trustCommand))
    try {
        # Only certificate import runs elevated. Add-AppxPackage runs below as the original user.
        $elevated = Start-Process -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -Verb RunAs -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded)
        if (!$elevated.WaitForExit(60000)) { throw 'UAC approval timed out; approve it and run the installer again.' }
        if ($elevated.ExitCode -ne 0) { throw 'Certificate trust import failed.' }
    } catch { throw "Windows certificate approval did not complete. Run this installer again when ready. $($_.Exception.Message)" }
    if (!(Test-Path -LiteralPath $trustPath)) { throw 'The expected certificate was not added to LocalMachine TrustedPeople.' }
}
$verified = Get-AuthenticodeSignature -LiteralPath $package
if ($verified.Status -ne 'Valid' -or $verified.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw "Package signature verification failed after trust: $($verified.Status)."
}
Add-AppxPackage -Path $package -ErrorAction Stop
$installed = Get-AppxPackage -Name 'NotificationBarrage' | Where-Object { $_.Publisher -eq $identity.Publisher -and $_.Version -eq [version]$identity.Version }
if (!$installed -or $installed.Status -ne 'Ok') { throw 'Installed package identity/version/status could not be confirmed.' }
$installed | Select-Object Name, Version, Status, PackageFamilyName, InstallLocation
