[CmdletBinding()]
param()
# Run as the interactive Windows user. This reads app registrations, never notification/chat contents.
$ErrorActionPreference = 'Stop'
$pattern = 'WeChat|Weixin|微信|Tencent|QQ'
$registrations = foreach ($path in @('HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings','HKCU:\Software\Classes\AppUserModelId','HKLM:\SOFTWARE\Classes\AppUserModelId')) {
    try {
        $keys = @(Get-ChildItem -LiteralPath $path -ErrorAction Stop)
        $matching = foreach ($key in $keys) {
            $properties = Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction SilentlyContinue
            $displayName = if ($properties) { [string]$properties.DisplayName } else { '' }
            if (($key.PSChildName + ' ' + $displayName) -match $pattern) {
                [pscustomobject]@{AppId=$key.PSChildName; DisplayName=$displayName; Enabled=$properties.Enabled}
            }
        }
        [pscustomobject]@{Path=$path; Status='已读取'; Entries=@($matching)}
    } catch {
        [pscustomobject]@{Path=$path; Status='无法读取或键不存在（不能据此判定不支持）'; Entries=@()}
    }
}
$apps = if (Get-Command Get-StartApps -ErrorAction SilentlyContinue) { @(Get-StartApps | Where-Object { $_.Name -match $pattern } | Select-Object Name,AppID) } else { @() }
$versions = @(Get-Process -Name WeChat,Weixin,QQ -ErrorAction SilentlyContinue | Where-Object Path | Select-Object -ExpandProperty Path -Unique | ForEach-Object {
    $file = Get-Item -LiteralPath $_
    [pscustomobject]@{Executable=$file.FullName;Version=$file.VersionInfo.ProductVersion}
})
[pscustomobject]@{CheckedAt=(Get-Date -Format o); User=[Environment]::UserName; Versions=$versions; StartApps=$apps; Registrations=@($registrations); Conclusion='注册信息只能提供线索。请将微信放到后台，收到一条测试消息后按 Win+N，检查该条消息是否进入 Windows 通知中心。'} | ConvertTo-Json -Depth 6
