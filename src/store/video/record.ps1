# Demo sahneyi oynatir, Ambiance Ultrawide'i uzerinde calistirir ve ekrani kaydeder.
# Kullanim: record.ps1 -Exe <AmbianceUltrawide.exe> -Clip <game.mp4> -Out <kayit.mp4> -Zoom 0|-1 -Seconds 12
param([string]$Exe, [string]$Clip, [string]$Out, [string]$Zoom = "0", [int]$Seconds = 12)
if (Get-Process bf6 -ErrorAction SilentlyContinue) { "BF6 acik, kayit yapilmadi"; return }
Get-Process AmbianceUltrawide, ffplay -ErrorAction SilentlyContinue | Stop-Process -Force
$play = Start-Process ffplay -ArgumentList "-hide_banner -loglevel error -noborder -x 1920 -y 1080 -left 200 -top 120 -window_title `"Neon Peaks`" -loop 0 -an `"$Clip`"" -PassThru
$h = 0; for ($i = 0; $i -lt 80 -and $h -eq 0; $i++) { Start-Sleep -Milliseconds 250; $play.Refresh(); $h = $play.MainWindowHandle.ToInt64() }
if ($h -eq 0) { "ffplay penceresi bulunamadi"; Stop-Process -Id $play.Id; return }
$am = Start-Process $Exe -ArgumentList "--boyut 1920x1080 --zoom $Zoom --hwnd $h" -PassThru
Start-Sleep -Seconds 3
ffmpeg -hide_banner -loglevel error -y -f lavfi -i "ddagrab=framerate=30:draw_mouse=0,hwdownload,format=bgra" -t $Seconds -c:v libx264 -preset ultrafast -crf 16 -pix_fmt yuv420p $Out
Stop-Process -Id $am.Id, $play.Id -ErrorAction SilentlyContinue
ffprobe -v error -select_streams v:0 -show_entries stream=width,height,nb_frames -of csv=p=0 $Out
