# Ambiance Ultrawide ikonunu ciz: app.ico (16..256) ve logo.png (256)
Add-Type -AssemblyName System.Drawing
$dir = Split-Path $MyInvocation.MyCommand.Path

function Draw-Icon([int]$S) {
  $b = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.Clear([System.Drawing.Color]::Transparent)
  $f = $S / 256.0
  # yuvarlak kare zemin
  $r = 56 * $f; $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc(0, 0, 2*$r, 2*$r, 180, 90); $p.AddArc($S-2*$r-1, 0, 2*$r, 2*$r, 270, 90)
  $p.AddArc($S-2*$r-1, $S-2*$r-1, 2*$r, 2*$r, 0, 90); $p.AddArc(0, $S-2*$r-1, 2*$r, 2*$r, 90, 90); $p.CloseFigure()
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Rectangle(0, 0, $S, $S)), [System.Drawing.Color]::FromArgb(255, 30, 26, 40), [System.Drawing.Color]::FromArgb(255, 14, 13, 20), 90)
  $g.FillPath($bg, $p)
  $g.SetClip($p)
  # isik: solda turuncu, sagda mor yumusak parilti
  foreach ($glow in @(@(70, 128, 150, 255, 138, 61), @(186, 128, 150, 192, 75, 255))) {
    $cx = $glow[0] * $f; $cy = $glow[1] * $f; $rad = $glow[2] * $f
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath; $gp.AddEllipse($cx - $rad, $cy - $rad * 0.8, 2 * $rad, 1.6 * $rad)
    $pb = New-Object System.Drawing.Drawing2D.PathGradientBrush($gp)
    $pb.CenterColor = [System.Drawing.Color]::FromArgb(230, $glow[3], $glow[4], $glow[5])
    $pb.SurroundColors = @([System.Drawing.Color]::FromArgb(0, $glow[3], $glow[4], $glow[5]))
    $g.FillPath($pb, $gp)
  }
  $g.ResetClip()
  # ortada oyun ekrani (16:9)
  $w = 132 * $f; $h = 76 * $f; $x = ($S - $w) / 2; $y = ($S - $h) / 2
  $sr = 8 * $f; $sp = New-Object System.Drawing.Drawing2D.GraphicsPath
  $sp.AddArc($x, $y, 2*$sr, 2*$sr, 180, 90); $sp.AddArc($x+$w-2*$sr, $y, 2*$sr, 2*$sr, 270, 90)
  $sp.AddArc($x+$w-2*$sr, $y+$h-2*$sr, 2*$sr, 2*$sr, 0, 90); $sp.AddArc($x, $y+$h-2*$sr, 2*$sr, 2*$sr, 90, 90); $sp.CloseFigure()
  $g.FillPath((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 12, 12, 16))), $sp)
  $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 255, 255, 255), [Math]::Max(1, 5 * $f)); $g.DrawPath($pen, $sp)
  $g.Dispose(); return $b
}

$sizes = 256, 128, 64, 48, 32, 24, 16
$pngs = @()
foreach ($s in $sizes) {
  $bm = Draw-Icon $s; $ms = New-Object System.IO.MemoryStream; $bm.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $pngs += ,($ms.ToArray())
  if ($s -eq 256) { $bm.Save("$dir\logo.png", [System.Drawing.Imaging.ImageFormat]::Png) }
  $bm.Dispose()
}
# ICO dosyasi (PNG gomulu)
$fs = [System.IO.File]::Create("$dir\app.ico"); $bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $s = $sizes[$i]; $d = if ($s -ge 256) { 0 } else { $s }
  $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$pngs[$i].Length); $bw.Write([UInt32]$offset)
  $offset += $pngs[$i].Length
}
foreach ($d in $pngs) { $bw.Write($d) }
$bw.Close()

# Store (MSIX) logolari
$assets = Join-Path $dir "store\Assets"
New-Item -ItemType Directory -Force $assets | Out-Null
function Save-Icon([int]$s, [string]$name) { $bm = Draw-Icon $s; $bm.Save("$assets\$name", [System.Drawing.Imaging.ImageFormat]::Png); $bm.Dispose() }
Save-Icon 50  "StoreLogo.png"
Save-Icon 100 "StoreLogo.scale-200.png"
Save-Icon 44  "Square44x44Logo.png"
Save-Icon 88  "Square44x44Logo.scale-200.png"
Save-Icon 150 "Square150x150Logo.png"
Save-Icon 300 "Square150x150Logo.scale-200.png"
foreach ($t in 16, 24, 32, 48, 256) { Save-Icon $t "Square44x44Logo.targetsize-$t.png"; Save-Icon $t "Square44x44Logo.targetsize-$($t)_altform-unplated.png" }
# genis kutucuk: koyu zemin ortasinda ikon
foreach ($sc in @(@(1, "Wide310x150Logo.png"), @(2, "Wide310x150Logo.scale-200.png"))) {
  $k = $sc[0]; $W = 310 * $k; $H = 150 * $k
  $wb = New-Object System.Drawing.Bitmap($W, $H); $wg = [System.Drawing.Graphics]::FromImage($wb)
  $wg.Clear([System.Drawing.Color]::FromArgb(255, 20, 18, 28)); $wg.InterpolationMode = 'HighQualityBicubic'
  $ic = Draw-Icon (110 * $k); $wg.DrawImage($ic, [int](($W - 110 * $k) / 2), [int](($H - 110 * $k) / 2)); $ic.Dispose(); $wg.Dispose()
  $wb.Save("$assets\$($sc[1])", [System.Drawing.Imaging.ImageFormat]::Png); $wb.Dispose()
}
"ok"
