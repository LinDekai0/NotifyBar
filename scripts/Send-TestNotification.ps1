$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Use Windows PowerShell 5.1 (powershell.exe).' }
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
[Windows.UI.Notifications.ToastNotification,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
[Windows.UI.Notifications.ToastNotificationHistory,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument,Windows.Data.Xml.Dom.XmlDocument,ContentType=WindowsRuntime] | Out-Null
$sender = Get-StartApps | Where-Object Name -eq 'Windows PowerShell' | Select-Object -First 1
if (!$sender) { throw 'Windows PowerShell notification sender is not registered.' }
$tag = 'nb' + [guid]::NewGuid().ToString('N').Substring(0,10)
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml('<toast><visual><binding template="ToastGeneric"><text>NotifyBar local test</text><text>Windows notification delivery check: ' + $tag + '</text></binding></visual></toast>')
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
$toast.Tag = $tag
$toast.Group = 'NotifyBarTest'
$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($sender.AppID)
$notifier.Show($toast)
Start-Sleep -Milliseconds 700
$history = [Windows.UI.Notifications.ToastNotificationManager]::History.GetHistory($sender.AppID)
$found = @($history | Where-Object { $_.Tag -eq $tag -and $_.Group -eq 'NotifyBarTest' }).Count
[pscustomobject]@{ Sender=$sender.AppID; TestTag=$tag; PresentInSenderHistory=($found -eq 1) } | ConvertTo-Json

if ($found -ne 1) { throw 'Test toast not found in sender history; check Windows notification settings.' }
