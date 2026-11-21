"""Tanitim videosu icin hareketli 'oyun' sahnesi (Neon Peaks) uretir: 1920x1080, 30 fps.
Kenarlarda renk degistiren isiklar, ambiyans isiginin canli tepkisini gostermek icin.
Kullanim: python game_clip.py cikis.mp4 [saniye]
"""
import math
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw

W, H, FPS = 1920, 1080, 30
out = sys.argv[1]
secs = float(sys.argv[2]) if len(sys.argv) > 2 else 16.0
N = int(secs * FPS)
rng = np.random.default_rng(7)

# gokyuzu paletleri (ust, orta, alt) - zamanla aralarinda gecis
PAL = [
    ((20, 16, 60), (150, 50, 140), (255, 140, 60)),
    ((8, 30, 70), (40, 110, 170), (120, 220, 230)),
    ((40, 10, 50), (190, 40, 120), (255, 90, 90)),
    ((20, 16, 60), (150, 50, 140), (255, 140, 60)),
]


def sky_colors(t):
    p = (t / secs) * (len(PAL) - 1)
    i = min(int(p), len(PAL) - 2)
    f = p - i
    f = f * f * (3 - 2 * f)
    return [tuple(PAL[i][k][c] + (PAL[i + 1][k][c] - PAL[i][k][c]) * f for c in range(3)) for k in range(3)]


def sky(t):
    top, mid, bot = sky_colors(t)
    y = np.linspace(0, 1, H)[:, None]
    a = np.clip(y / 0.55, 0, 1)
    b = np.clip((y - 0.55) / 0.45, 0, 1)
    col = np.where(y < 0.55, np.array(top) * (1 - a) + np.array(mid) * a, np.array(mid) * (1 - b) + np.array(bot) * b)
    return np.broadcast_to(col[:, None, :], (H, W, 3)).astype(np.float32)


def mountain_mask(base, amp, seed, width):
    r = np.random.default_rng(seed)
    img = Image.new("L", (width, H), 0)
    pts = [(0, H)]
    y = base
    for x in range(0, width + 40, 40):
        y = max(base - amp, min(base + amp / 3, y + r.integers(-amp // 3, amp // 3 + 1)))
        pts.append((x, y))
    pts.append((width, H))
    ImageDraw.Draw(img).polygon(pts, fill=255)
    return np.asarray(img, np.float32) / 255.0


layers = [
    (mountain_mask(620, 160, 3, W * 2), (110, 50, 120), 0.6),
    (mountain_mask(720, 140, 11, W * 2), (60, 30, 85), 1.2),
    (mountain_mask(840, 120, 23, W * 2), (25, 16, 40), 2.2),
]
stars = rng.integers(0, [W, 420], size=(220, 2))
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)

proc = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                         "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "medium", "-crf", "16", "-pix_fmt", "yuv420p", out],
                        stdin=subprocess.PIPE)
for n in range(N):
    t = n / FPS
    f = sky(t).copy()
    # yildizlar
    tw = 0.5 + 0.5 * np.sin(t * 3 + stars[:, 0] * 0.1)
    f[stars[:, 1], stars[:, 0]] = np.minimum(255, f[stars[:, 1], stars[:, 0]] + (140 * tw)[:, None])
    # gunes + parilti
    sx, sy = 300 + (W - 600) * (t / secs), 470 + 60 * math.sin(t / secs * math.pi)
    d2 = (xx - sx) ** 2 + (yy - sy) ** 2
    glow = np.exp(-d2 / (2 * 190.0 ** 2))[..., None]
    f = f + glow * np.array([255, 170, 90], np.float32) * 0.55
    disk = (d2 < 85 ** 2)[..., None]
    f = np.where(disk, np.array([255, 232, 175], np.float32), f)
    # daglar (paralaks kayma)
    for mask, col, speed in layers:
        off = int(t * speed * 40) % W
        m = mask[:, off:off + W][..., None]
        f = f * (1 - m) + np.array(col, np.float32) * m
    # kenarlarda neon isiklar (ambiyansin tepki verdigi yerler)
    cl = np.array([0, 210, 255]) * (0.5 + 0.5 * math.sin(t * 1.3)) + np.array([255, 40, 200]) * (0.5 - 0.5 * math.sin(t * 1.3))
    cr = np.array([255, 110, 40]) * (0.5 + 0.5 * math.cos(t * 0.9)) + np.array([60, 255, 120]) * (0.5 - 0.5 * math.cos(t * 0.9))
    f[880:890, 0:150] = cl
    f[920:1080, 0:90] = cl
    f[880:1080, W - 120:W] = cr
    # iki parlama (patlama)
    for t0, x0, col in ((5.5, 140, (255, 230, 120)), (10.5, W - 160, (255, 80, 60))):
        if t0 <= t < t0 + 1.4:
            k = 1 - (t - t0) / 1.4
            e = np.exp(-((xx - x0) ** 2 + (yy - 700) ** 2) / (2 * (160 + 300 * (1 - k)) ** 2))[..., None]
            f = f + e * np.array(col, np.float32) * k * 1.2
    proc.stdin.write(np.clip(f, 0, 255).astype(np.uint8).tobytes())
proc.stdin.close()
proc.wait()
print("ok", out, N, "frames")
