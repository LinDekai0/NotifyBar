[CmdletBinding(SupportsShouldProcess = $true)]
param([switch]$RestoreDependencies)
$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath (Split-Path $PSScriptRoot -Parent)).Path.TrimEnd('\')
$destinationPath = [IO.Path]::GetFullPath('D:\notification-barrage').TrimEnd('\')
$allowedSource = [IO.Path]::GetFullPath('D:\股价信息预测系统\notification-barrage').TrimEnd('\')
if ($sourcePath -ne $allowedSource -and $sourcePath -ne $destinationPath) { throw '脚本只允许移动已确认的 notification-barrage 项目。' }
 $sourceHasSolution = Test-Path -LiteralPath (Join-Path $sourcePath 'NotificationBarrage.sln')
 $destinationHasSolution = Test-Path -LiteralPath (Join-Path $destinationPath 'NotificationBarrage.sln')
 $partialMove = !$sourceHasSolution -and $destinationHasSolution -and (Test-Path -LiteralPath (Join-Path $destinationPath '.git'))
if (!$sourceHasSolution -and !$partialMove) { throw '源目录不含预期解决方案，且未发现可续接的 D:\notification-barrage 半成品目标，已停止。' }
if ($sourcePath -ne $destinationPath -and (Test-Path -LiteralPath $destinationPath) -and !$partialMove) { throw 'D:\notification-barrage 已存在，不会覆盖或合并。' }
$links = @(Get-ChildItem -LiteralPath $sourcePath -Recurse -Force -Attributes ReparsePoint)
if ($links.Count -gt 0) { throw '项目包含目录链接或符号链接，需先核实后再移动。' }
$action = if ($RestoreDependencies) { '移动完整项目（含 Git 历史），然后恢复 NuGet 依赖并编译测试；不安装 MSIX' } else { '移动完整项目（含 Git 历史），不覆盖任何已有目录' }
if (!$PSCmdlet.ShouldProcess("$sourcePath -> $destinationPath", $action)) { return }

if ($sourcePath -ne $destinationPath) {
    # Keep the process working directory outside the directory being moved.
    Set-Location -LiteralPath 'D:\'
    if ($partialMove) {
        foreach ($item in Get-ChildItem -LiteralPath $sourcePath -Force) {
            if ($item.Name -eq '.git') { continue }
            $target = Join-Path $destinationPath $item.Name
            if (Test-Path -LiteralPath $target) { throw "续接目标已存在 $target，不会覆盖。" }
            Move-Item -LiteralPath $item.FullName -Destination $destinationPath
        }
    } else {
        Move-Item -LiteralPath $sourcePath -Destination $destinationPath
    }
    if (!(Test-Path -LiteralPath (Join-Path $destinationPath 'NotificationBarrage.sln'))) { throw '移动结果异常，请检查两个目录，不要再次覆盖执行。' }
}
Write-Host "项目现位于 $destinationPath"
Write-Host "预览程序：$destinationPath\artifacts\preview\NotificationBarrage.Preview.exe"
if ($RestoreDependencies) {
    & (Join-Path $destinationPath 'scripts\Restore-Dependencies.ps1')
} else {
    Write-Host "恢复并编译：& '$destinationPath\scripts\Restore-Dependencies.ps1'"
}
Write-Host '后续开发请在 Codex 中打开 D:\notification-barrage 作为工作目录。'
