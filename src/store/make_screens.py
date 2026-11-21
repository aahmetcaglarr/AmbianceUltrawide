"""Store ekran goruntulerini uretir (1920x1080, dil basina 8 adet).

Kullanim:  python make_screens.py <kaynak_klasor> <cikis_klasoru>
Kaynak klasorde: amb_off.png, amb_fit.png, amb_detail.png (3440x1440 ekran goruntuleri)
ve ui/<dil>/ui_home.png, ui_NavLight.png, ui_NavDisplay.png, ui_NavAbout.png
"""
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
W, H = 1920, 1080
FONTS = "C:/Windows/Fonts/"
FONT_SET = {
    "zh": ("msyhbd.ttc", "msyh.ttc"),
    "ja": ("YuGothB.ttc", "YuGothM.ttc"),
    "ko": ("malgunbd.ttf", "malgun.ttf"),
}
DEFAULT_FONTS = ("segoeuib.ttf", "segoeui.ttf")
ORANGE = (255, 138, 61)
PURPLE = (192, 75, 255)


def fonts(lang, size_t, size_s):
    b, r = FONT_SET.get(lang, DEFAULT_FONTS)
    return ImageFont.truetype(FONTS + b, size_t), ImageFont.truetype(FONTS + r, size_s)


_BG = None


def background():
    """Koyu dikey gecis + solda turuncu, sagda mor yumusak parilti (bir kez hesaplanir)."""
    global _BG
    if _BG is None:
        from PIL import ImageChops
        top, bot = (26, 20, 38), (11, 10, 15)
        col = Image.new("RGB", (1, H))
        for y in range(H):
            t = y / (H - 1)
            col.putpixel((0, y), tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)))
        bg = col.resize((W, H))
        glow = Image.new("RGB", (W, H), (0, 0, 0))
        g = ImageDraw.Draw(glow)
        g.ellipse((-500, 150, 600, 1050), fill=(110, 55, 25))
        g.ellipse((1350, 100, 2450, 1000), fill=(80, 32, 110))
        glow = glow.filter(ImageFilter.GaussianBlur(220))
        _BG = ImageChops.add(bg, glow)
    return _BG.copy()


def rounded_mask(size, r):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), r, fill=255)
    return m


def place(canvas, img, box_w, box_h, top, radius=18):
    s = min(box_w / img.width, box_h / img.height)
    iw, ih = int(img.width * s), int(img.height * s)
    im = img.convert("RGB").resize((iw, ih), Image.LANCZOS)
    x = (W - iw) // 2
    y = top + (box_h - ih) // 2
    # golge
    sh = Image.new("L", (W, H), 0)
    ImageDraw.Draw(sh).rounded_rectangle((x, y + 14, x + iw, y + ih + 14), radius, fill=170)
    sh = sh.filter(ImageFilter.GaussianBlur(26))
    canvas.paste(Image.new("RGB", (W, H), (0, 0, 0)), (0, 0), sh)
    canvas.paste(im, (x, y), rounded_mask((iw, ih), radius))
    ImageDraw.Draw(canvas).rounded_rectangle((x, y, x + iw - 1, y + ih - 1), radius, outline=(255, 255, 255, 40), width=1)
    return x, y, iw, ih


def text_center(draw, y, text, font, fill):
    w = draw.textlength(text, font=font)
    draw.text(((W - w) / 2, y), text, font=font, fill=fill)


def compose(lang, title, sub, img, out):
    c = background()
    d = ImageDraw.Draw(c)
    ft, fs = fonts(lang, 58, 30)
    text_center(d, 62, title, ft, (255, 255, 255))
    text_center(d, 146, sub, fs, (205, 200, 215))
    place(c, img, 1720, 820, 222)
    c.save(out, optimize=True)


def compare_image(src, labels, lang):
    """Sol: dusurulmus cozunurluk (1.33x bilinear buyutme), sag: piksel piksel (Ambiance Ultrawide)."""
    full = Image.open(src).convert("RGB")
    game = full.crop((760, 180, 2680, 1260))  # 1920x1080 oyun penceresi
    d = ImageDraw.Draw(game)
    hud = ImageFont.truetype(FONTS + "segoeuib.ttf", 24)
    # ince detaylar: oyun arayuzu yazilari ve nisangah
    d.text((1300, 60), "HP 100   ARMOR 75", font=hud, fill=(240, 240, 240))
    d.text((1300, 92), "AMMO 30 / 90", font=hud, fill=(255, 210, 120))
    cx, cy = 1560, 300
    for dx, dy in ((-18, 0), (18, 0), (0, -18), (0, 18)):
        d.line((cx + dx * 0.4, cy + dy * 0.4, cx + dx, cy + dy), fill=(120, 255, 160), width=2)
    region = (1240, 40, 1800, 560)  # 560x520
    crop = game.crop(region)
    sharp = crop.resize((840, 780), Image.NEAREST)
    blurry = crop.resize((int(560 * 4 / 3), int(520 * 4 / 3)), Image.BILINEAR).resize((840, 780), Image.NEAREST)
    gap = 40
    out = Image.new("RGB", (840 * 2 + gap, 780 + 70), (14, 12, 20))
    out.paste(blurry, (0, 70))
    out.paste(sharp, (840 + gap, 70))
    dr = ImageDraw.Draw(out)
    fb, _ = fonts(lang, 34, 20)
    for i, (lab, col) in enumerate(((labels[0], (255, 110, 110)), (labels[1], ORANGE))):
        x0 = i * (840 + gap)
        w = dr.textlength(lab, font=fb)
        dr.ellipse((x0 + (840 - w) / 2 - 26, 22, x0 + (840 - w) / 2 - 10, 38), fill=col)
        dr.text((x0 + (840 - w) / 2, 8), lab, font=fb, fill=(255, 255, 255))
    return out


def main():
    src, outdir = sys.argv[1], sys.argv[2]
    data = json.load(open(os.path.join(HERE, "listings.json"), encoding="utf-8"))
    for lang, L in data.items():
        od = os.path.join(outdir, lang)
        os.makedirs(od, exist_ok=True)
        imgs = [
            Image.open(os.path.join(src, "amb_off.png")),
            compare_image(os.path.join(src, "amb_off.png"), L["compare"], lang),
            Image.open(os.path.join(src, "amb_fit.png")),
            Image.open(os.path.join(src, "amb_detail.png")),
            Image.open(os.path.join(src, "ui", lang, "ui_home.png")),
            Image.open(os.path.join(src, "ui", lang, "ui_NavLight.png")),
            Image.open(os.path.join(src, "ui", lang, "ui_NavDisplay.png")),
            Image.open(os.path.join(src, "ui", lang, "ui_NavAbout.png")),
        ]
        for i, (img, (t, s)) in enumerate(zip(imgs, L["captions"]), 1):
            compose(lang, t, s, img, os.path.join(od, "%02d.png" % i))
        print(lang, "ok")


if __name__ == "__main__":
    main()
