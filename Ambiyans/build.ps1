# Ambiyans.exe derleme betigi. Gereken: Windows 10/11 (.NET Framework 4.8 ile gelir) + Windows SDK (Windows.winmd icin)
# Kullanim:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path $MyInvocation.MyCommand.Path
Push-Location $here
try {
  $fw  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
  $wpf = "$fw\WPF"
  $ref = "${env:ProgramFiles(x86)}\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades"
  $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\References"
  $uni = Get-ChildItem "$kits\*\Windows.Foundation.UniversalApiContract\*\*.winmd" | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
  $fnd = Get-ChildItem "$kits\*\Windows.Foundation.FoundationContract\*\*.winmd" | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
  $stt = Get-ChildItem "$kits\*\Windows.ApplicationModel.StartupTaskContract\*\*.winmd" | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
  if (-not $uni -or -not $fnd -or -not $stt) { throw "Windows SDK bulunamadi (WinRT .winmd)." }
  New-Item -ItemType Directory -Force out | Out-Null
  & "$fw\csc.exe" /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /out:out\Ambiyans.exe `
    /win32manifest:app.manifest /win32icon:app.ico `
    /resource:Ui.xaml,Ambiyans.Ui.xaml /resource:logo.png,Ambiyans.logo.png `
    "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" "/r:$fw\System.Xaml.dll" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    "/r:$uni" "/r:$fnd" "/r:$stt" /nowarn:1701 "/r:$ref\System.Runtime.dll" "/r:$ref\System.Threading.Tasks.dll" `
    Ambiyans.cs GpuCapture.cs Ui.cs Lang.cs Startup.cs
  if ($LASTEXITCODE -ne 0) { throw "Derleme hatasi" }
  "OK: out\Ambiyans.exe"
} finally { Pop-Location }


