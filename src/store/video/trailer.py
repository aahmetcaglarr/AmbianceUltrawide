"""Tanitim videosunu birlestirir (1920x1080, 30 fps, H.264 + AAC).
Kullanim: python trailer.py <kaynak_klasor> <cikis.mp4> [dil]
Kaynak klasorde: rec_off.mp4, rec_fit.mp4 (3440x1440 ekran kayitlari), music.wav
Ayrica docs/screenshots/store/<dil>/ gorselleri ve src/logo.png kullanilir.
"""
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
sys.path.insert(0, os.path.join(HERE, ".."))
import make_screens as ms  # noqa: E402

W, H, FPS = 1920, 1080, 30
XF = 0.6  # gecis suresi
src, out = sys.argv[1], sys.argv[2]
lang = sys.argv[3] if len(sys.argv) > 3 else "en"
data = json.load(open(os.path.join(HERE, "..", "listings.json"), encoding="utf-8"))[lang]
shots = os.path.join(ROOT, "docs", "screenshots", "store", lang)
logo = Image.open(os.path.join(ROOT, "src", "logo.png")).convert("RGBA")
BG = ms.background()
FT, FS = ms.fonts(lang, 60, 32)
FBIG, _ = ms.fonts("en", 104, 30)
OUTRO = {
    "en": ("Free on the Microsoft Store", "12 languages  ·  About 1% CPU  ·  Open source"),
    "tr": ("Microsoft Store'da ücretsiz", "12 dil  ·  Yaklaşık %1 işlemci  ·  Açık kaynak"),
    "es": ("Gratis en Microsoft Store", "12 idiomas  ·  ~1% de CPU  ·  Código abierto"),
    "pt": ("Grátis na Microsoft Store", "12 idiomas  ·  ~1% de CPU  ·  Código aberto"),
    "fr": ("Gratuit sur le Microsoft Store", "12 langues  ·  ~1 % de CPU  ·  Open source"),
    "de": ("Kostenlos im Microsoft Store", "12 Sprachen  ·  ca. 1 % CPU  ·  Open Source"),
    "ru": ("Бесплатно в Microsoft Store", "12 языков  ·  около 1% ЦП  ·  Открытый код"),
    "zh": ("在 Microsoft Store 免费获取", "12 种语言  ·  CPU 约 1%  ·  开源"),
    "ja": ("Microsoft Store で無料", "12 言語  ·  CPU 約 1%  ·  オープンソース"),
    "ko": ("Microsoft Store에서 무료", "12개 언어  ·  CPU 약 1%  ·  오픈 소스"),
    "it": ("Gratis su Microsoft Store", "12 lingue  ·  circa 1% di CPU  ·  Open source"),
    "pl": ("Za darmo w Microsoft Store", "12 języków  ·  ok. 1% CPU  ·  Open source"),
}


def ease(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


class Clip:
    """Ekran kaydini sirayla okur, 1920x804'e olcekler."""

    def __init__(self, path, h=804):
        self.h = h
        self.p = subprocess.Popen(["ffmpeg", "-loglevel", "error", "-i", path, "-vf", f"scale={W}:{h}:flags=lanczos",
                                   "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE)
        self.idx = -1
        self.cur = None

    def frame(self, t):
        want = int(t * FPS)
        while self.idx < want:
            buf = self.p.stdout.read(W * self.h * 3)
            if len(buf) < W * self.h * 3:
                break
            self.cur = Image.frombuffer("RGB", (W, self.h), buf)
            self.idx += 1
        return self.cur


def text(d, y, s, f, fill, alpha=1.0):
    w = d.textlength(s, font=f)
    d.text(((W - w) / 2, y), s, font=f, fill=fill + (int(255 * alpha),))


def layer_text(img, items):
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for y, s, f, col, a in items:
        if a > 0:
            text(d, y, s, f, col, a)
    return Image.alpha_composite(img.convert("RGBA"), ov).convert("RGB")


def scene_intro(t, dur):
    img = BG.copy().convert("RGBA")
    a = ease(t / 1.2)
    s = 0.9 + 0.1 * ease(t / 1.6)
    lg = logo.resize((int(230 * s), int(230 * s)), Image.LANCZOS)
    lg.putalpha(lg.getchannel("A").point(lambda v: int(v * a)))
    img.alpha_composite(lg, ((W - lg.width) // 2, 250 - (lg.height - 230) // 2))
    return layer_text(img, [(530, "Ambiance Ultrawide", FBIG, (255, 255, 255), ease((t - 0.5) / 1.0)),
                            (680, data["tagline"], FS, (215, 208, 225), ease((t - 1.3) / 1.0))])


def make_still(path, z0=1.0, z1=1.06):
    im = Image.open(path).convert("RGB")

    def f(t, dur):
        z = z0 + (z1 - z0) * (t / dur)
        w, h = int(W / z), int(H / z)
        x, y = (W - w) // 2, (H - h) // 2
        return im.crop((x, y, x + w, y + h)).resize((W, H), Image.BILINEAR)
    return f


def make_live(clip, title, sub):
    def f(t, dur):
        img = BG.copy()
        fr = clip.frame(t)
        if fr is not None:
            img.paste(fr, (0, 190))
        return layer_text(img, [(48, title, FT, (255, 255, 255), ease((t - 0.3) / 0.8)),
                                (126, sub, FS, (210, 204, 220), ease((t - 0.8) / 0.8))])
    return f


def make_slides(paths):
    ims = [Image.open(p).convert("RGB") for p in paths]

    def f(t, dur):
        seg = dur / len(ims)
        i = min(int(t / seg), len(ims) - 1)
        lt = t - i * seg
        cur = ims[i]
        if i + 1 < len(ims) and lt > seg - 0.5:
            return Image.blend(cur, ims[i + 1], ease((lt - (seg - 0.5)) / 0.5))
        return cur
    return f


def scene_outro(t, dur):
    img = BG.copy().convert("RGBA")
    a = ease(t / 1.0)
    lg = logo.resize((170, 170), Image.LANCZOS)
    lg.putalpha(lg.getchannel("A").point(lambda v: int(v * a)))
    img.alpha_composite(lg, ((W - 170) // 2, 230))
    o1, o2 = OUTRO.get(lang, OUTRO["en"])
    res = layer_text(img, [(440, "Ambiance Ultrawide", FBIG, (255, 255, 255), ease((t - 0.3) / 0.8)),
                           (600, o1, FT, (255, 170, 110), ease((t - 0.9) / 0.8)),
                           (700, o2, FS, (210, 204, 220), ease((t - 1.4) / 0.8))])
    fade = ease((t - (dur - 1.2)) / 1.2)
    return Image.blend(res, Image.new("RGB", (W, H), (0, 0, 0)), fade) if fade > 0 else res


cap = data["captions"]
rec_off = Clip(os.path.join(src, "rec_off.mp4"))
rec_fit = Clip(os.path.join(src, "rec_fit.mp4"))
SCENES = [
    (4.5, scene_intro),
    (5.0, make_still(os.path.join(shots, "02.png"))),
    (12.0, make_live(rec_off, cap[0][0], cap[0][1])),
    (7.5, make_live(rec_fit, cap[2][0], cap[2][1])),
    (8.0, make_slides([os.path.join(shots, "%02d.png" % i) for i in (5, 6, 7)])),
    (6.0, scene_outro),
]
starts, t = [], 0.0
for dur, _ in SCENES:
    starts.append(t)
    t += dur - XF
total = starts[-1] + SCENES[-1][0]
NF = int(total * FPS)

music = os.path.join(src, "music.wav")
proc = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
                         "-i", "-", "-i", music, "-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-preset", "slow", "-crf", "18",
                         "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-af", f"afade=t=out:st={total - 2.5}:d=2.5",
                         "-t", f"{total:.2f}", "-movflags", "+faststart", out], stdin=subprocess.PIPE)
for n in range(NF):
    gt = n / FPS
    active = [(i, gt - starts[i]) for i in range(len(SCENES)) if starts[i] <= gt < starts[i] + SCENES[i][0]]
    i, lt = active[0]
    frame = SCENES[i][1](lt, SCENES[i][0])
    if len(active) > 1:
        j, lt2 = active[1]
        nxt = SCENES[j][1](lt2, SCENES[j][0])
        frame = Image.blend(frame, nxt, ease(lt2 / XF))
    proc.stdin.write(frame.tobytes())
proc.stdin.close()
proc.wait()

# Store icin: video kucuk resmi ve 16:9 "super hero" gorseli (baslik yazisi olmadan)
thumb = scene_intro(4.0, 4.5)
thumb.save(os.path.splitext(out)[0] + "_thumbnail.png")
print("ok", out, f"{total:.1f}s", NF, "frames")
