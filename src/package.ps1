# Store paketi (MSIX) ve GitHub zip'i uretir.
# Partner Center > Urun > Product identity sayfasindaki uc degeri girin:
#   powershell -ExecutionPolicy Bypass -File package.ps1 -IdentityName "12345Ad.AmbianceUltrawide" -Publisher "CN=XXXXXXXX-..." -PublisherDisplayName "Adiniz"
param(
  # Partner Center > Product identity (Microsoft Store kimligi)
  [string]$IdentityName = "CaglarSoft.AmbianceUltrawide",
  [string]$Publisher = "CN=B95AFB7E-19A6-4ED8-8F8A-760645B227E4",
  [string]$PublisherDisplayName = "CaglarSoft",
  [string]$Version = "1.0.0.0"
)
$ErrorActionPreference = 'Stop'
$here = Split-Path $MyInvocation.MyCommand.Path
Push-Location $here
try {
  & powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
  if ($LASTEXITCODE -ne 0) { throw "Derleme basarisiz" }
  if (-not (Test-Path .\store\Assets\StoreLogo.png)) { & powershell -NoProfile -ExecutionPolicy Bypass -File .\make-icon.ps1 | Out-Null }

  # --- MSIX ---
  $layout = Join-Path $here "out\msix-layout"
  if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
  New-Item -ItemType Directory -Force "$layout\Assets" | Out-Null
  Copy-Item .\out\AmbianceUltrawide.exe $layout
  Copy-Item .\store\Assets\*.png "$layout\Assets"
  $m = [IO.File]::ReadAllText((Join-Path $here "store\AppxManifest.template.xml"), [Text.Encoding]::UTF8)
  $m = $m.Replace("{IDENTITY_NAME}", [Security.SecurityElement]::Escape($IdentityName)).Replace("{PUBLISHER}", [Security.SecurityElement]::Escape($Publisher))
  $m = $m.Replace("{PUBLISHER_DISPLAY_NAME}", [Security.SecurityElement]::Escape($PublisherDisplayName)).Replace("{VERSION}", $Version)
  [IO.File]::WriteAllText("$layout\AppxManifest.xml", $m, (New-Object Text.UTF8Encoding($false)))

  $kit = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
  if (-not $kit) { throw "makeappx.exe bulunamadi (Windows SDK gerekli)" }
  $pri = Join-Path (Split-Path $kit) "makepri.exe"
  Push-Location $layout
  & $pri createconfig /cf priconfig.xml /dq en-US /o | Out-Null
  & $pri new /pr . /cf priconfig.xml /of resources.pri /o | Out-Null
  Remove-Item priconfig.xml
  Pop-Location
  $msix = Join-Path $here "out\AmbianceUltrawide_$Version`_x64.msix"
  & $kit pack /d $layout /p $msix /o | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "makeappx hatasi" }
  "OK: $msix"

  # --- GitHub zip ---
  $zipDir = Join-Path $here "out\zip"
  if (Test-Path $zipDir) { Remove-Item -Recurse -Force $zipDir }
  New-Item -ItemType Directory -Force $zipDir | Out-Null
  Copy-Item .\out\AmbianceUltrawide.exe $zipDir
  if (Test-Path ..\LICENSE) { Copy-Item ..\LICENSE "$zipDir\LICENSE.txt" }
  if (Test-Path ..\docs\BENIOKU.txt) { Copy-Item ..\docs\BENIOKU.txt $zipDir }
  if (Test-Path ..\docs\README.txt) { Copy-Item ..\docs\README.txt $zipDir }
  $v3 = ($Version -split '\.')[0..2] -join '.'
  $zip = Join-Path $here "out\AmbianceUltrawide-$v3-win-x64.zip"
  if (Test-Path $zip) { Remove-Item $zip }
  Compress-Archive -Path "$zipDir\*" -DestinationPath $zip
  "OK: $zip"
} finally { Pop-Location }
