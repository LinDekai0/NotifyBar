param([string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/Assets'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
foreach ($asset in @(@{Name='StoreLogo.png';Size=50},@{Name='Square44x44Logo.png';Size=44},@{Name='Square150x150Logo.png';Size=150})) {
    $size=$asset.Size
    $bitmap=[System.Drawing.Bitmap]::new($size,$size)
    $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(15,23,42))
    $pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(34,211,238),[single]($size*0.07))
    $graphics.DrawRectangle($pen,[single]($size*.23),[single]($size*.25),[single]($size*.53),[single]($size*.37))
    $graphics.DrawLine($pen,[single]($size*.35),[single]($size*.62),[single]($size*.28),[single]($size*.77))
    $graphics.DrawLine($pen,[single]($size*.28),[single]($size*.77),[single]($size*.55),[single]($size*.62))
    $graphics.DrawLine($pen,[single]($size*.32),[single]($size*.43),[single]($size*.65),[single]($size*.43))
    $bitmap.Save((Join-Path $OutputDirectory $asset.Name),[System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose();$graphics.Dispose();$bitmap.Dispose()
}
