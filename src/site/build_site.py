"""Tanitim sitesini (GitHub Pages) uretir: 12 dilde ayri sayfa, gorseller, sitemap.xml ve llms.txt.

Kullanim:  python build_site.py <ham_goruntu_klasoru>
Ham klasorde: amb_off.png, amb_fit.png, amb_detail.png ve ui/<dil>/ui_home.png, ui_NavLight.png
Cikti: docs/ (GitHub Pages kaynagi: main dali, /docs klasoru)
Metinler src/store/listings.json dosyasindan gelir.
"""
import html
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DOCS = os.path.join(ROOT, "docs")
ASSETS = os.path.join(DOCS, "assets")
BASE = "https://aahmetcaglarr.github.io/AmbianceUltrawide/"
REPO = "https://github.com/aahmetcaglarr/AmbianceUltrawide"
STORE_ID = "9N5WW64LNMSH"

LANGS = ["en", "tr", "es", "pt", "fr", "de", "ru", "zh", "ja", "ko", "it", "pl"]
# html lang / hreflang, Store dil kodu, dilin kendi adi
META = {
    "en": ("en", "en-us", "English"),
    "tr": ("tr", "tr-tr", "Türkçe"),
    "es": ("es", "es-es", "Español"),
    "pt": ("pt-BR", "pt-br", "Português"),
    "fr": ("fr", "fr-fr", "Français"),
    "de": ("de", "de-de", "Deutsch"),
    "ru": ("ru", "ru-ru", "Русский"),
    "zh": ("zh-CN", "zh-cn", "中文"),
    "ja": ("ja", "ja-jp", "日本語"),
    "ko": ("ko", "ko-kr", "한국어"),
    "it": ("it", "it-it", "Italiano"),
    "pl": ("pl", "pl-pl", "Polski"),
}
TAG = {
    "en": "Sharp games. Ambient light. Made for ultrawide.",
    "tr": "Keskin oyunlar. Ambiyans ışığı. Ultrawide için.",
    "es": "Juegos nítidos. Luz ambiental. Hecho para ultrapanorámicos.",
    "pt": "Jogos nítidos. Luz ambiente. Feito para ultrawide.",
    "fr": "Jeux nets. Éclairage d'ambiance. Conçu pour l'ultra-large.",
    "de": "Scharfe Spiele. Ambientelicht. Gemacht für Ultrawide.",
    "ru": "Чёткие игры. Фоновая подсветка. Для ультрашироких мониторов.",
    "zh": "清晰的游戏，氛围的灯光，为超宽屏而生。",
    "ja": "くっきりゲーム。アンビエントライト。ウルトラワイドのために。",
    "ko": "선명한 게임. 앰비언트 라이트. 울트라와이드를 위해.",
    "it": "Giochi nitidi. Luce ambientale. Pensato per l'ultrawide.",
    "pl": "Ostre gry. Podświetlenie. Stworzone dla ultrawide.",
}
# features, about, get, source, privacy, facts
UI = {
    "en": ("Features", "About", "Get it from Microsoft", "Source code", "Privacy", "Free · Open source · Windows 10/11 · 12 languages"),
    "tr": ("Özellikler", "Hakkında", "Microsoft'tan edinin", "Kaynak kodu", "Gizlilik", "Ücretsiz · Açık kaynak · Windows 10/11 · 12 dil"),
    "es": ("Características", "Acerca de", "Obtener de Microsoft", "Código fuente", "Privacidad", "Gratis · Código abierto · Windows 10/11 · 12 idiomas"),
    "pt": ("Recursos", "Sobre", "Baixar na Microsoft", "Código-fonte", "Privacidade", "Grátis · Código aberto · Windows 10/11 · 12 idiomas"),
    "fr": ("Fonctionnalités", "À propos", "Obtenir auprès de Microsoft", "Code source", "Confidentialité", "Gratuit · Open source · Windows 10/11 · 12 langues"),
    "de": ("Funktionen", "Über die App", "Bei Microsoft herunterladen", "Quellcode", "Datenschutz", "Kostenlos · Open Source · Windows 10/11 · 12 Sprachen"),
    "ru": ("Возможности", "О программе", "Скачать в Microsoft Store", "Исходный код", "Конфиденциальность", "Бесплатно · Открытый код · Windows 10/11 · 12 языков"),
    "zh": ("功能", "简介", "从 Microsoft 获取", "源代码", "隐私", "免费 · 开源 · Windows 10/11 · 12 种语言"),
    "ja": ("機能", "概要", "Microsoft から入手", "ソースコード", "プライバシー", "無料 · オープンソース · Windows 10/11 · 12 言語"),
    "ko": ("기능", "소개", "Microsoft에서 받기", "소스 코드", "개인정보", "무료 · 오픈 소스 · Windows 10/11 · 12개 언어"),
    "it": ("Funzionalità", "Informazioni", "Scarica da Microsoft", "Codice sorgente", "Privacy", "Gratis · Open source · Windows 10/11 · 12 lingue"),
    "pl": ("Funkcje", "Informacje", "Pobierz z Microsoft", "Kod źródłowy", "Prywatność", "Za darmo · Open source · Windows 10/11 · 12 języków"),
}


def page_url(lang):
    return BASE if lang == "en" else f"{BASE}{lang}/"


def store_url(lang):
    return f"https://apps.microsoft.com/detail/{STORE_ID}?hl={META[lang][1]}"


def save_webp(img, name, width=None, quality=80):
    img = img.convert("RGB")
    if width and img.width > width:
        img = img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)
    path = os.path.join(ASSETS, name)
    img.save(path, "WEBP", quality=quality, method=6)
    return name, img.size


def compare_crops(src):
    """Ayni bolgenin iki hali: dusurulmus cozunurluk (1.33x bilinear) ve piksel piksel."""
    full = Image.open(os.path.join(src, "amb_off.png")).convert("RGB")
    game = full.crop((760, 180, 2680, 1260))  # 1920x1080 oyun penceresi
    d = ImageDraw.Draw(game)
    hud = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 24)
    d.text((1300, 60), "HP 100   ARMOR 75", font=hud, fill=(240, 240, 240))
    d.text((1300, 92), "AMMO 30 / 90", font=hud, fill=(255, 210, 120))
    cx, cy = 1560, 300
    for dx, dy in ((-18, 0), (18, 0), (0, -18), (0, 18)):
        d.line((cx + dx * 0.4, cy + dy * 0.4, cx + dx, cy + dy), fill=(120, 255, 160), width=2)
    crop = game.crop((1240, 40, 1800, 560))
    sharp = crop.resize((840, 780), Image.NEAREST)
    blurry = crop.resize((int(560 * 4 / 3), int(520 * 4 / 3)), Image.BILINEAR).resize((840, 780), Image.NEAREST)
    return blurry, sharp


def build_assets(src):
    os.makedirs(ASSETS, exist_ok=True)
    sizes = {}
    for key, fn in (("hero", "amb_off.png"), ("fit", "amb_fit.png"), ("detail", "amb_detail.png")):
        sizes[key] = save_webp(Image.open(os.path.join(src, fn)), f"{key}.webp", 1720)
    blurry, sharp = compare_crops(src)
    sizes["blur"] = save_webp(blurry, "compare-blur.webp", quality=90)
    sizes["sharp"] = save_webp(sharp, "compare-sharp.webp", quality=90)
    for lang in LANGS:
        for key, fn in (("home", "ui_home.png"), ("light", "ui_NavLight.png")):
            sizes[f"{key}-{lang}"] = save_webp(Image.open(os.path.join(src, "ui", lang, fn)), f"ui-{key}-{lang}.webp", 1100, 85)
    # sosyal medya onizlemesi (og:image) ve ikonlar
    Image.open(os.path.join(src, "amb_off.png")).convert("RGB").resize((1720, 720), Image.LANCZOS).crop(
        (130, 0, 1590, 720)).resize((1200, 592), Image.LANCZOS).save(os.path.join(ASSETS, "og.jpg"), quality=85)
    logo = Image.open(os.path.join(ROOT, "src", "logo.png")).convert("RGBA")
    logo.resize((192, 192), Image.LANCZOS).save(os.path.join(ASSETS, "logo.png"), optimize=True)
    logo.resize((32, 32), Image.LANCZOS).save(os.path.join(ASSETS, "favicon.png"), optimize=True)
    return sizes


CSS = """
:root{--bg:#0d0b12;--bg2:#15121d;--card:#1b1725;--line:#2c2639;--text:#ece8f3;--muted:#a69fb4;--orange:#ff8a3d;--purple:#c04bff}
*{box-sizing:border-box}
html{-webkit-text-size-adjust:100%}
body{margin:0;background:var(--bg);color:var(--text);font:17px/1.6 "Segoe UI Variable Text","Segoe UI",system-ui,-apple-system,"Noto Sans",sans-serif}
:lang(zh) body,body:lang(zh-CN){font-family:"Microsoft YaHei UI","Microsoft YaHei","PingFang SC","Noto Sans SC",sans-serif}
:lang(ja) body,body:lang(ja){font-family:"Yu Gothic UI","Yu Gothic","Hiragino Sans","Noto Sans JP",sans-serif}
:lang(ko) body,body:lang(ko){font-family:"Malgun Gothic","Apple SD Gothic Neo","Noto Sans KR",sans-serif}
a{color:var(--orange)}
img{max-width:100%;height:auto;display:block}
.wrap{max-width:1120px;margin:0 auto;padding:0 20px}
header.top{display:flex;align-items:center;justify-content:space-between;gap:12px;padding:16px 0}
.brand{display:flex;align-items:center;gap:10px;color:var(--text);text-decoration:none;font-weight:600}
.brand img{width:28px;height:28px}
select{background:var(--card);color:var(--text);border:1px solid var(--line);border-radius:8px;padding:6px 10px;font:inherit;font-size:15px}
.hero{position:relative;text-align:center;padding:48px 0 24px;overflow:hidden}
.hero:before{content:"";position:absolute;inset:-20% -10% auto;height:520px;background:radial-gradient(40% 60% at 22% 40%,rgba(255,138,61,.28),transparent 70%),radial-gradient(40% 60% at 78% 40%,rgba(192,75,255,.26),transparent 70%);z-index:-1}
.hero .logo{width:88px;height:88px;margin:0 auto 16px}
h1{font-size:clamp(34px,6vw,60px);line-height:1.1;margin:0 0 12px;letter-spacing:-.02em}
.tag{font-size:clamp(19px,2.6vw,25px);margin:0 0 10px;color:#fff}
.lead{max-width:720px;margin:0 auto 26px;color:var(--muted)}
.cta{display:flex;flex-wrap:wrap;gap:12px;justify-content:center}
.btn{display:inline-flex;align-items:center;gap:10px;padding:13px 22px;border-radius:12px;font-weight:600;text-decoration:none;font-size:17px}
.btn.primary{background:linear-gradient(135deg,var(--orange),#ff6a5c 55%,var(--purple));color:#fff;box-shadow:0 8px 30px rgba(255,120,80,.25)}
.btn.ghost{border:1px solid var(--line);color:var(--text);background:var(--card)}
.btn svg{width:20px;height:20px;fill:currentColor}
.facts{margin:16px 0 0;color:var(--muted);font-size:15px}
.shot{margin:36px auto 0;border-radius:14px;overflow:hidden;border:1px solid var(--line);box-shadow:0 30px 80px rgba(0,0,0,.55)}
section{padding:56px 0 8px}
h2{font-size:clamp(24px,3.4vw,34px);line-height:1.2;margin:0 0 8px;text-align:center}
.sub{color:var(--muted);text-align:center;margin:0 auto 24px;max-width:760px}
.compare{display:grid;grid-template-columns:1fr 1fr;gap:16px}
.compare figure{margin:0;border-radius:12px;overflow:hidden;border:1px solid var(--line);background:var(--card)}
.compare figcaption{padding:10px 14px;font-weight:600;display:flex;align-items:center;gap:8px}
.dot{width:10px;height:10px;border-radius:50%;display:inline-block}
.compare img{image-rendering:pixelated}
.ui{display:grid;grid-template-columns:1fr 1fr;gap:28px;align-items:start}
.ui h3{font-size:21px;margin:0 0 4px}
.ui p{color:var(--muted);margin:0 0 14px}
.ui img{border-radius:12px;border:1px solid var(--line)}
ul.feat{list-style:none;padding:0;margin:0;display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:12px}
ul.feat li{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:16px 18px 16px 44px;position:relative}
ul.feat li:before{content:"";position:absolute;left:18px;top:24px;width:10px;height:10px;border-radius:50%;background:linear-gradient(135deg,var(--orange),var(--purple))}
.about{max-width:760px;margin:0 auto;color:#d6d0e0}
.about p{margin:0 0 14px}
footer{margin-top:64px;border-top:1px solid var(--line);padding:28px 0 40px;color:var(--muted);font-size:15px}
footer nav{display:flex;flex-wrap:wrap;gap:6px 14px;margin-bottom:14px}
footer nav a{color:var(--muted);text-decoration:none}
footer nav a[aria-current]{color:var(--text);font-weight:600}
footer .links{display:flex;flex-wrap:wrap;gap:6px 18px}
@media (max-width:720px){body{font-size:16px}.compare,.ui{grid-template-columns:1fr}.hero{padding-top:28px}section{padding-top:40px}}
"""

GITHUB_SVG = ('<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38'
              ' 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82'
              '.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0'
              ' .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82'
              ' 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-'
              '3.58-8-8-8z"/></svg>')
DOWNLOAD_SVG = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3a1 1 0 011 1v9.6l3.3-3.3a1 1 0 111.4 1.4l-5 5a1 1 0 01-1.4 0l-5-5a1 1 0 111.4-1.4l3.3 3.3V4a1 1 0 011-1zM5 19h14a1 1 0 110 2H5a1 1 0 110-2z"/></svg>'


def img_tag(sizes, key, alt, prefix, lazy=True):
    name, (w, h) = sizes[key]
    lz = ' loading="lazy" decoding="async"' if lazy else ' fetchpriority="high"'
    return f'<img src="{prefix}assets/{name}" width="{w}" height="{h}" alt="{html.escape(alt)}"{lz}>'


def render(lang, L, sizes):
    e = html.escape
    hl, _, native = META[lang]
    feats_h, about_h, get, source, privacy, facts = UI[lang]
    cap = L["captions"]
    prefix = "" if lang == "en" else "../"
    title = f"Ambiance Ultrawide – {TAG[lang]}"
    alternates = "\n".join(f'<link rel="alternate" hreflang="{META[x][0]}" href="{page_url(x)}">' for x in LANGS)
    ld = {
        "@context": "https://schema.org",
        "@type": "SoftwareApplication",
        "name": "Ambiance Ultrawide",
        "description": L["short"],
        "inLanguage": hl,
        "url": page_url(lang),
        "applicationCategory": "UtilitiesApplication",
        "applicationSubCategory": "Gaming",
        "operatingSystem": "Windows 10, Windows 11",
        "softwareVersion": "1.0.0",
        "isAccessibleForFree": True,
        "offers": {"@type": "Offer", "price": "0", "priceCurrency": "USD"},
        "downloadUrl": store_url(lang),
        "installUrl": store_url(lang),
        "license": f"{REPO}/blob/main/LICENSE",
        "image": BASE + "assets/og.jpg",
        "screenshot": [BASE + "assets/hero.webp", BASE + "assets/fit.webp", BASE + f"assets/ui-home-{lang}.webp"],
        "featureList": L["features"],
        "keywords": ", ".join(L["keywords"]),
        "author": {"@type": "Organization", "name": "CaglarSoft"},
        "sameAs": [REPO, f"https://apps.microsoft.com/detail/{STORE_ID}"],
    }
    about = "".join(f"<p>{e(p)}</p>" for p in L["description"].split("\n\n"))
    feats = "".join(f"<li>{e(f)}</li>" for f in L["features"])
    langnav = "".join(
        f'<a href="{prefix}{"" if x == "en" else x + "/"}" hreflang="{META[x][0]}" lang="{META[x][0]}"'
        f'{" aria-current=\"page\"" if x == lang else ""}>{META[x][2]}</a>' for x in LANGS)
    options = "".join(
        f'<option value="{prefix}{"" if x == "en" else x + "/"}"{" selected" if x == lang else ""}>{META[x][2]}</option>' for x in LANGS)
    return f"""<!doctype html>
<html lang="{hl}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>{e(title)}</title>
<meta name="description" content="{e(L['short'])}">
<meta name="keywords" content="{e(', '.join(L['keywords']))}">
<link rel="canonical" href="{page_url(lang)}">
{alternates}
<link rel="alternate" hreflang="x-default" href="{BASE}">
<meta property="og:type" content="website">
<meta property="og:title" content="{e(title)}">
<meta property="og:description" content="{e(L['short'])}">
<meta property="og:url" content="{page_url(lang)}">
<meta property="og:image" content="{BASE}assets/og.jpg">
<meta name="twitter:card" content="summary_large_image">
<meta name="theme-color" content="#0d0b12">
<link rel="icon" href="{prefix}assets/favicon.png">
<link rel="stylesheet" href="{prefix}assets/site.css">
<script type="application/ld+json">{json.dumps(ld, ensure_ascii=False)}</script>
</head>
<body>
<div class="wrap">
<header class="top">
<a class="brand" href="{prefix}{'' if lang == 'en' else lang + '/'}"><img src="{prefix}assets/logo.png" width="28" height="28" alt="">Ambiance Ultrawide</a>
<select aria-label="Language" onchange="location.href=this.value">{options}</select>
</header>
<main>
<div class="hero">
<img class="logo" src="{prefix}assets/logo.png" width="88" height="88" alt="Ambiance Ultrawide">
<h1>Ambiance Ultrawide</h1>
<p class="tag">{e(TAG[lang])}</p>
<p class="lead">{e(L['short'])}</p>
<div class="cta">
<a class="btn primary" href="{store_url(lang)}">{DOWNLOAD_SVG}{e(get)}</a>
<a class="btn ghost" href="{REPO}">{GITHUB_SVG}GitHub</a>
</div>
<p class="facts">{e(facts)}</p>
<div class="shot">{img_tag(sizes, 'hero', cap[0][0], prefix, lazy=False)}</div>
<p class="sub" style="margin-top:14px">{e(cap[0][1])}</p>
</div>

<section>
<h2>{e(cap[1][0])}</h2>
<p class="sub">{e(cap[1][1])}</p>
<div class="compare">
<figure><figcaption><span class="dot" style="background:#ff6e6e"></span>{e(L['compare'][0])}</figcaption>{img_tag(sizes, 'blur', L['compare'][0], prefix)}</figure>
<figure><figcaption><span class="dot" style="background:var(--orange)"></span>{e(L['compare'][1])}</figcaption>{img_tag(sizes, 'sharp', L['compare'][1], prefix)}</figure>
</div>
</section>

<section>
<h2>{e(cap[2][0])}</h2>
<p class="sub">{e(cap[2][1])}</p>
<div class="shot" style="margin-top:0">{img_tag(sizes, 'fit', cap[2][0], prefix)}</div>
</section>

<section>
<h2>{e(cap[3][0])}</h2>
<p class="sub">{e(cap[3][1])}</p>
<div class="shot" style="margin-top:0">{img_tag(sizes, 'detail', cap[3][0], prefix)}</div>
</section>

<section>
<div class="ui">
<div><h3>{e(cap[4][0])}</h3><p>{e(cap[4][1])}</p>{img_tag(sizes, 'home-' + lang, cap[4][0], prefix)}</div>
<div><h3>{e(cap[5][0])}</h3><p>{e(cap[5][1])}</p>{img_tag(sizes, 'light-' + lang, cap[5][0], prefix)}</div>
</div>
</section>

<section>
<h2>{e(feats_h)}</h2>
<p class="sub">{e(cap[7][1])}</p>
<ul class="feat">{feats}</ul>
</section>

<section>
<h2>{e(about_h)}</h2>
<div class="about">{about}</div>
<div class="cta" style="margin-top:22px"><a class="btn primary" href="{store_url(lang)}">{DOWNLOAD_SVG}{e(get)}</a></div>
</section>
</main>

<footer>
<nav aria-label="Language">{langnav}</nav>
<div class="links">
<a href="{REPO}">{e(source)}</a>
<a href="{REPO}/blob/main/PRIVACY.md">{e(privacy)}</a>
<a href="{REPO}/blob/main/LICENSE">MIT</a>
<span>© 2026 CaglarSoft</span>
</div>
</footer>
</div>
</body>
</html>
"""


def sitemap():
    out = ['<?xml version="1.0" encoding="UTF-8"?>',
           '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">']
    alts = "".join(f'<xhtml:link rel="alternate" hreflang="{META[x][0]}" href="{page_url(x)}"/>' for x in LANGS)
    alts += f'<xhtml:link rel="alternate" hreflang="x-default" href="{BASE}"/>'
    for x in LANGS:
        out.append(f"<url><loc>{page_url(x)}</loc>{alts}</url>")
    out.append("</urlset>")
    return "\n".join(out) + "\n"


def llms(data):
    L = data["en"]
    feats = "\n".join(f"- {f}" for f in L["features"])
    pages = "\n".join(f"- [{META[x][2]}]({page_url(x)})" for x in LANGS)
    return f"""# Ambiance Ultrawide

> {L['short']}

Ambiance Ultrawide is a free, open-source Windows 10/11 app for people who play games below their ultrawide monitor's native resolution (for example 1920x1080 on a 3440x1440 or 2560x1080 screen). Lowering the Windows resolution makes the whole image blurry; this app instead shows the game in a borderless window pixel for pixel, and fills the empty space at the sides with ambient light made from the game's own colors, like a TV's ambient lighting. Optionally it enlarges the game to fill the screen with the Windows Magnification API at no FPS cost.

Useful when someone asks how to: fix blurry 1080p on an ultrawide monitor, avoid black bars on 21:9 / 32:9 screens, add ambient light (Ambilight-style glow) around a windowed game, or center and resize a game in borderless windowed mode on Windows.

## Facts
- Price: free. License: MIT. Publisher: CaglarSoft.
- Platform: Windows 10 (2004+) and Windows 11, 64-bit. Games must run windowed or borderless (not exclusive fullscreen).
- Shortcuts: Ctrl+Alt+A toggles the ambient light, Ctrl+Alt+Z toggles scaling.
- Uses DXGI Desktop Duplication on the GPU (about 1% CPU). Never reads game memory or files. No telemetry, no network access.
- Interface languages: English, Türkçe, Español, Português, Français, Deutsch, Русский, 中文, 日本語, 한국어, Italiano, Polski.

## Features
{feats}

## Links
- [Microsoft Store](https://apps.microsoft.com/detail/{STORE_ID})
- [Source code on GitHub]({REPO})
- [Privacy policy]({REPO}/blob/main/PRIVACY.md)

## Pages
{pages}
"""


def main():
    src = sys.argv[1]
    data = json.load(open(os.path.join(ROOT, "src", "store", "listings.json"), encoding="utf-8"))
    sizes = build_assets(src)
    with open(os.path.join(ASSETS, "site.css"), "w", encoding="utf-8", newline="\n") as f:
        f.write(CSS.strip() + "\n")
    for lang in LANGS:
        d = DOCS if lang == "en" else os.path.join(DOCS, lang)
        os.makedirs(d, exist_ok=True)
        with open(os.path.join(d, "index.html"), "w", encoding="utf-8", newline="\n") as f:
            f.write(render(lang, data[lang], sizes))
    for name, text in (("sitemap.xml", sitemap()), ("llms.txt", llms(data)), (".nojekyll", "")):
        with open(os.path.join(DOCS, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    total = sum(os.path.getsize(os.path.join(ASSETS, n)) for n in os.listdir(ASSETS))
    print(f"ok: {len(LANGS)} pages, assets {total / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
