// ==UserScript==
// @name         Ambiance Video
// @namespace    https://aahmetcaglarr.github.io/AmbianceUltrawide/
// @version      1.4.0
// @description  Fullscreen videos on any site with ambient light synced to the scene, instead of black bars. Made for iPad Safari.
// @author       CaglarSoft
// @homepageURL  https://github.com/aahmetcaglarr/AmbianceUltrawide
// @downloadURL  https://aahmetcaglarr.github.io/AmbianceUltrawide/ambiance-video.user.js
// @updateURL    https://aahmetcaglarr.github.io/AmbianceUltrawide/ambiance-video.user.js
// @match        *://*/*
// @run-at       document-idle
// @grant        none
// ==/UserScript==

// Sayfadaki buyuk videolarin kosesine "Ambiance" dugmesi koyar. Basinca video tam ekran olur ve
// kenarlarda kalan siyah bosluk, videonun kuculup bulaniklastirilmis haliyle (sahneyle senkron) dolar.
// Video pikselleri hic okunmaz; sadece ekrana cizilir. Bu yuzden baska alan adindan gelen videolarda da calisir.
// DRM'li (Netflix vb.) videolarda tarayici cizimi engeller, isik siyah kalir.

(function () {
  "use strict";
  if (window.__ambianceVideo) return;
  window.__ambianceVideo = true;

  const MIN_W = 240, MIN_H = 135;          // bundan kucuk videolarda dugme cikmaz
  const HIDE_MS = 2800;                    // dugmelerin kaybolma suresi

  // ---------- metinler ----------
  const T = {
    en: ["Ambient fullscreen", "Exit", "Soft", "Detailed", "Black bars", "Auto", "Off", "Cinema", "can't detect"],
    tr: ["Ambiyans tam ekran", "Çık", "Sade", "Detaylı", "Siyah bantlar", "Oto", "Kapalı", "Sinema", "algılanamıyor"],
    es: ["Pantalla completa ambiental", "Salir", "Suave", "Detallado", "Bandas negras", "Auto", "No", "Cine", "no detectable"],
    pt: ["Tela cheia com luz ambiente", "Sair", "Suave", "Detalhado", "Barras pretas", "Auto", "Não", "Cinema", "não detectável"],
    fr: ["Plein écran d'ambiance", "Quitter", "Doux", "Détaillé", "Bandes noires", "Auto", "Non", "Cinéma", "non détectable"],
    de: ["Ambiente-Vollbild", "Beenden", "Sanft", "Detailliert", "Schwarze Balken", "Auto", "Aus", "Kino", "nicht erkennbar"],
    ru: ["Полный экран с подсветкой", "Выйти", "Мягкий", "Детальный", "Чёрные полосы", "Авто", "Выкл.", "Кино", "не определить"],
    zh: ["氛围灯全屏", "退出", "柔和", "细节", "黑边", "自动", "关", "影院", "无法检测"],
    ja: ["アンビエント全画面", "終了", "ソフト", "詳細", "黒帯", "自動", "オフ", "シネマ", "検出不可"],
    ko: ["앰비언트 전체 화면", "나가기", "부드럽게", "상세", "검은 여백", "자동", "끔", "시네마", "감지 불가"],
    it: ["Schermo intero ambientale", "Esci", "Morbido", "Dettagliato", "Bande nere", "Auto", "No", "Cinema", "non rilevabile"],
    pl: ["Pełny ekran z podświetleniem", "Wyjdź", "Łagodny", "Szczegółowy", "Czarne pasy", "Auto", "Wył.", "Kino", "nie wykryto"],
  };
  const S = T[(navigator.language || "en").slice(0, 2).toLowerCase()] || T.en;

  // ---------- ayarlar (site basina, yoksa varsayilan) ----------
  const store = {
    get(k, d) { try { const v = localStorage.getItem("ambiance-video:" + k); return v === null ? d : JSON.parse(v); } catch (e) { return d; } },
    set(k, v) { try { localStorage.setItem("ambiance-video:" + k, JSON.stringify(v)); } catch (e) { /* gizli mod vb. */ } },
  };
  let style = store.get("style", "soft");   // "soft" | "detailed"

  // YouTube gibi siteler "Trusted Types" ile innerHTML'i engeller; bu yuzden tum ogeler DOM ile kurulur
  const SVGNS = "http://www.w3.org/2000/svg";
  function svg(tag, attrs, kids) {
    const e = document.createElementNS(SVGNS, tag);
    for (const k in attrs) e.setAttribute(k, attrs[k]);
    (kids || []).forEach((c) => e.appendChild(c));
    return e;
  }
  function icon() {
    return svg("svg", { viewBox: "0 0 24 24", "aria-hidden": "true" }, [
      svg("defs", {}, [svg("linearGradient", { id: "amb-g", x1: "0", y1: "0", x2: "1", y2: "1" }, [
        svg("stop", { offset: "0", "stop-color": "#ff8a3d" }), svg("stop", { offset: "1", "stop-color": "#c04bff" })])]),
      svg("rect", { x: "2", y: "5", width: "20", height: "14", rx: "3", fill: "none", stroke: "url(#amb-g)", "stroke-width": "2" }),
      svg("rect", { x: "7", y: "9", width: "10", height: "6", rx: "1", fill: "#fff" }),
    ]);
  }

  const CSS = `
    :host { all: initial; }
    * { box-sizing: border-box; font-family: -apple-system, "Segoe UI", system-ui, sans-serif; }
    .btn { position: fixed; z-index: 2147483646; display: flex; align-items: center; gap: 7px; padding: 7px 12px 7px 9px;
      border: 1px solid rgba(255,255,255,.18); border-radius: 999px; background: rgba(20,18,26,.72); color: #fff;
      font-size: 13px; font-weight: 600; letter-spacing: .01em; cursor: pointer; -webkit-backdrop-filter: blur(12px); backdrop-filter: blur(12px);
      box-shadow: 0 4px 18px rgba(0,0,0,.35); transition: opacity .25s; -webkit-tap-highlight-color: transparent; }
    .btn svg { width: 20px; height: 20px; }
    .btn.hidden { opacity: 0; pointer-events: none; }
    .stage { position: fixed; inset: 0; z-index: 2147483647; background: #000; overflow: hidden; }
    .glow { position: absolute; transition: filter .3s; }
    .mask { position: absolute; inset: 0; z-index: 3; pointer-events: none; overflow: hidden; display: none; }
    .soft .glow { filter: blur(70px) saturate(1.35) brightness(.95); }
    .detailed .glow { filter: blur(22px) saturate(1.25) brightness(.9); }
    .shade { position: absolute; inset: 0; pointer-events: none; background: radial-gradient(ellipse at center, rgba(0,0,0,0) 55%, rgba(0,0,0,.18) 100%); }
    .bar { position: absolute; top: max(14px, env(safe-area-inset-top)); right: max(14px, env(safe-area-inset-right)); display: flex; gap: 8px;
      transition: opacity .25s; z-index: 5; }
    .bar.hidden { opacity: 0; pointer-events: none; }
    .bar button { border: 1px solid rgba(255,255,255,.2); border-radius: 999px; background: rgba(20,18,26,.6); color: #fff; font-size: 14px;
      font-weight: 600; padding: 9px 16px; cursor: pointer; -webkit-backdrop-filter: blur(12px); backdrop-filter: blur(12px); }
  `;

  // ---------- arayuz kabugu (sayfa stillerinden yalitilmis) ----------
  const host = document.createElement("div");
  host.id = "ambiance-video-host";
  const root = host.attachShadow({ mode: "open" });
  const styleEl = document.createElement("style");
  styleEl.textContent = CSS;
  root.appendChild(styleEl);
  const btn = document.createElement("button");
  btn.className = "btn hidden";
  btn.title = S[0];
  btn.setAttribute("aria-label", S[0]);
  const label = document.createElement("span");
  label.textContent = "Ambiance";
  btn.append(icon(), label);
  root.appendChild(btn);
  (document.body || document.documentElement).appendChild(host);

  const inFrame = window.top !== window;

  // ---------- dugme: dokunulan / oynatilan videonun kosesinde ----------
  let target = null, hideTimer = 0, posRaf = 0;

  function bigEnough(v) {
    const r = v.getBoundingClientRect();
    return r.width >= MIN_W && r.height >= MIN_H && r.bottom > 0 && r.top < innerHeight;
  }
  // Oynaticilarin "shadow DOM" icine sakladigi videolari ve cerceveleri de bul (sik cagrildigi icin kisa sureli onbellek)
  let deepCache = { t: 0, videos: [], frames: [] };
  const hooked = new WeakSet();
  const onPlay = (e) => { const v = e.composedPath ? e.composedPath()[0] : e.target; if (v instanceof HTMLVideoElement && bigEnough(v)) showBtn(v); };
  function deep() {
    const now = Date.now();
    if (now - deepCache.t < 700) return deepCache;
    const videos = [], frames = [];
    (function walk(r) {
      r.querySelectorAll("video").forEach((v) => videos.push(v));
      r.querySelectorAll("iframe, frame").forEach((f) => frames.push(f));
      r.querySelectorAll("*").forEach((el) => {
        if (el.shadowRoot && el !== host) {
          // "play" olayi shadow DOM disina cikmaz; her golge koke ayrica dinleyici tak
          if (!hooked.has(el.shadowRoot)) { hooked.add(el.shadowRoot); el.shadowRoot.addEventListener("play", onPlay, true); }
          walk(el.shadowRoot);
        }
      });
    })(document);
    deepCache = { t: now, videos, frames };
    return deepCache;
  }
  function videoAt(x, y) {
    const vids = deep().videos;
    for (const v of vids) {
      const r = v.getBoundingClientRect();
      if (x >= r.left && x <= r.right && y >= r.top && y <= r.bottom && bigEnough(v)) return v;
    }
    // Saglayici iframe'lerinde sayfa genelde sadece oynaticidir: nereye dokunulursa dokunulsun en buyuk videoyu al
    if (inFrame) {
      let best = null, area = 0;
      for (const v of vids) { const r = v.getBoundingClientRect(); if (bigEnough(v) && r.width * r.height > area) { best = v; area = r.width * r.height; } }
      return best;
    }
    return null;
  }
  function placeBtn() {
    if (!target || !target.isConnected) { btn.classList.add("hidden"); return; }
    const r = target.getBoundingClientRect();
    btn.style.left = Math.max(8, Math.min(innerWidth - 140, r.right - 130)) + "px";
    btn.style.top = Math.max(8, r.top + 12) + "px";
    posRaf = requestAnimationFrame(placeBtn);
  }
  function showBtn(v) {
    if (stage || !v) return;
    target = v;
    btn.classList.remove("hidden");
    cancelAnimationFrame(posRaf);
    placeBtn();
    clearTimeout(hideTimer);
    hideTimer = setTimeout(() => { btn.classList.add("hidden"); cancelAnimationFrame(posRaf); }, HIDE_MS);
  }
  const onPoint = (e) => {
    if (stage) return;
    const p = e.touches && e.touches[0] ? e.touches[0] : e;
    const v = videoAt(p.clientX, p.clientY);
    if (v) showBtn(v);
  };
  // pencere seviyesinde ve en once dinle: bazi oynaticilar dokunuslari belge seviyesinde durduruyor
  for (const t of ["pointerdown", "pointermove", "touchstart", "mousedown"]) window.addEventListener(t, onPoint, { capture: true, passive: true });
  window.addEventListener("play", onPlay, true);
  btn.addEventListener("click", (e) => { e.preventDefault(); e.stopPropagation(); if (target) enter(target); });

  // ---------- cerceve koprusu ----------
  // Film sitelerinde oynatici genelde baska bir saglayicinin iframe'inin icindedir ve iframe'e tam ekran izni verilmez.
  // O zaman icerideki betik dis sayfadaki betige haber verir; dis sayfa o iframe'i ekrani kaplayacak hale getirir.
  // Ic ice iframe'lerde haber en ust sayfaya kadar zincirle iletilir.
  const MSG = "ambiance-video";
  const lifted = new Map();   // buyutulen iframe -> eski hali
  // Boyut "100vh" DEGIL: iPad Safari'de 100vh, cubuklar acikkenki yukseklik; tam ekranda cerceve kisa kalip ortalaniyor
  // ve ustte/altta siyah serit birakiyordu. Sabit konumda %100 her zaman gercek ekran boyutudur.
  const LIFT = "position:fixed!important;top:0!important;left:0!important;right:0!important;bottom:0!important;" +
    "width:100%!important;height:100%!important;max-width:none!important;min-height:100%!important;" +
    "max-height:none!important;margin:0!important;padding:0!important;border:0!important;transform:none!important;" +
    "z-index:2147483647!important;display:block!important;visibility:visible!important;opacity:1!important";

  function tell(cmd) { try { window.parent.postMessage({ [MSG]: cmd }, "*"); } catch (e) { /* ust sayfa yok */ } }
  function frameOf(win) { return deep().frames.find((f) => { try { return f.contentWindow === win; } catch (e) { return false; } }); }

  function lift(f) {
    if (lifted.has(f)) return;
    lifted.set(f, { style: f.getAttribute("style"), overflow: document.documentElement.style.overflow });
    f.style.cssText = LIFT;
    document.documentElement.style.overflow = "hidden";
    if (inFrame) tell("enter");
    else {
      const req = f.requestFullscreen || f.webkitRequestFullscreen;
      if (req) { try { const p = req.call(f); if (p && p.catch) p.catch(() => {}); } catch (e) { /* sayfayi kaplamasi yeterli */ } }
    }
  }
  function drop(f) {
    const s = lifted.get(f);
    if (!s) return;
    lifted.delete(f);
    if (s.style === null) f.removeAttribute("style"); else f.setAttribute("style", s.style);
    document.documentElement.style.overflow = s.overflow;
    if (inFrame) tell("exit");
    else if ((document.fullscreenElement || document.webkitFullscreenElement) === f) {
      const x = document.exitFullscreen || document.webkitExitFullscreen;
      try { const p = x.call(document); if (p && p.catch) p.catch(() => {}); } catch (e) { /* zaten cikti */ }
    }
  }
  window.addEventListener("message", (e) => {
    const cmd = e.data && typeof e.data === "object" ? e.data[MSG] : null;
    if (cmd !== "enter" && cmd !== "exit") return;
    deepCache.t = 0;
    const f = frameOf(e.source);   // sadece bu sayfadaki bir iframe'den gelen mesajlar
    if (!f) return;
    if (cmd === "enter") lift(f); else drop(f);
  });

  // ---------- ambiyans tam ekran ----------
  let stage = null, video = null, marker = null, saved = null, canvas = null, ctx = null, bar = null, loopId = 0, barTimer = 0,
    styleGuard = null, askedParent = false, masks = [], barsBtn = null;
  const FILL = "position:absolute!important;inset:0!important;width:100%!important;height:100%!important;" +
    "max-width:none!important;max-height:none!important;object-fit:contain!important;transform:none!important;" +
    "margin:0!important;z-index:2!important;background:transparent!important;display:block!important;opacity:1!important;visibility:visible!important";

  // Isik tuvali: ortasi videonun kendisi, cevresinde kenar renklerinin disa uzatildigi bir pay var.
  // Pay ekranin disinda kalir; bulaniklastirma kenarlari karartsa bile o kararma gorunmez.
  let GW = 48, GH = 27, P = 12;
  function sizeGlow() {
    const d = style === "detailed";
    GW = d ? 128 : 48; GH = d ? 72 : 27; P = Math.round(GW * 0.3);
    for (const c of [canvas].concat(masks.map((m) => m.canvas))) {
      c.width = GW + 2 * P; c.height = GH + 2 * P;
      c.style.left = (-P / GW * 100) + "%"; c.style.top = (-P / GH * 100) + "%";
      c.style.width = ((GW + 2 * P) / GW * 100) + "%"; c.style.height = ((GH + 2 * P) / GH * 100) + "%";
    }
  }

  // ---------- videonun icine gomulu siyah bantlar ----------
  // "auto": video pikselleri okunabiliyorsa bantlari kendisi bulur. "2.39" / "1.85": o oranda film varsayar. "off": dokunmaz.
  const MODES = ["auto", "2.39", "1.85", "off"];
  let barsMode = store.get("bars", "auto");
  if (barsMode === "cinema") barsMode = "2.39";          // 1.3 surumundeki ad
  if (MODES.indexOf(barsMode) < 0) barsMode = "auto";
  let crop = { t: 0, b: 0, l: 0, r: 0 };
  let readable = true, probe = null, pctx = null, hist = [], lastProbe = 0;

  function ratioCrop(R) {
    const a = video.videoWidth / video.videoHeight;
    if (!a || a >= R - 0.05) return { t: 0, b: 0, l: 0, r: 0 };
    const f = (1 - a / R) / 2 + 0.004;
    return { t: f, b: f, l: 0, r: 0 };
  }
  function barsLabel() {
    if (!barsBtn) return;
    const m = barsMode === "off" ? S[6] : barsMode === "auto" ? S[5] + (readable ? "" : " (" + S[8] + ")") : barsMode + ":1";
    barsBtn.textContent = S[4] + ": " + m;
  }
  function detectBars() {
    if (!readable) return;
    const N = 120;   // satir/sutun cozunurlugu: bant siniri ~1% hassasiyetle bulunur
    if (!probe) { probe = document.createElement("canvas"); probe.width = N; probe.height = N; pctx = probe.getContext("2d", { willReadFrequently: true }); }
    let d;
    try { pctx.drawImage(video, 0, 0, N, N); d = pctx.getImageData(0, 0, N, N).data; }
    catch (e) { readable = false; barsLabel(); pokeBar(7000); return; }   // baska alan adindan gelen video: tarayici okumaya izin vermiyor
    // her satir/sutun icin ortalama parlaklik ve "parlak" (l > 40) piksel sayisi
    const rowMean = new Array(N).fill(0), rowBright = new Array(N).fill(0), colBright = new Array(N).fill(0);
    let total = 0;
    for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) {
      const i = (y * N + x) * 4, l = 0.2126 * d[i] + 0.7152 * d[i + 1] + 0.0722 * d[i + 2];
      rowMean[y] += l / N; total += l;
      if (l > 40) { rowBright[y]++; colBright[x]++; }
    }
    if (total / (N * N) < 14) return;          // karartma / siyah sahne: karar verme
    // ust/alt bant: neredeyse siyah; altyazi icin satirin %25'ine kadar parlak piksele izin ver
    const darkRow = (y) => rowMean[y] < 24 && rowBright[y] <= N * 0.25;
    // yan bant: sutunda neredeyse hic parlak piksel olmamali (karanlik sahne kenarlari bant sanilmasin)
    const darkCol = (x) => colBright[x] <= N * 0.02;
    let t = 0; while (t < N / 2 && darkRow(t)) t++;
    let b = 0; while (b < N / 2 && darkRow(N - 1 - b)) b++;
    let l = 0; while (l < N / 2 && darkCol(l)) l++;
    let r = 0; while (r < N / 2 && darkCol(N - 1 - r)) r++;
    if (Math.abs(l - r) > 2) { l = 0; r = 0; }   // gercek yan bantlar simetriktir
    hist.push({ t, b, l, r });
    if (hist.length > 8) hist.shift();
    // bantlar sabittir, karanlik sahneler gecicidir: son birkac olcumun en kucugunu al
    const min = (k) => Math.min.apply(null, hist.map((h) => h[k])) / N;
    // cok ince bantlari yok say; bulunanlari resmin kenarina biraz tasir ki arada ince siyah cizgi kalmasin
    const clean = (f) => (f < 0.03 ? 0 : Math.min(f + 0.006, 0.35));
    setCrop({ t: clean(min("t")), b: clean(min("b")), l: clean(min("l")), r: clean(min("r")) });
  }
  function setCrop(c) {
    if (c.t === crop.t && c.b === crop.b && c.l === crop.l && c.r === crop.r) return;
    crop = c;
    layoutMasks();
  }
  function applyBarsMode() {
    hist = [];
    if (barsMode === "off") setCrop({ t: 0, b: 0, l: 0, r: 0 });
    else if (barsMode === "auto") { setCrop({ t: 0, b: 0, l: 0, r: 0 }); detectBars(); }
    else setCrop(ratioCrop(parseFloat(barsMode)));
    barsLabel();
  }

  // Bantlarin ustune ayni isigi koyan katmanlar (ust+alt ve sol+sag). Bantin oldugu yerde video ustunde gorunur.
  function layoutMasks() {
    if (!stage || !video) return;
    const W = stage.clientWidth, H = stage.clientHeight, vw = video.videoWidth, vh = video.videoHeight;
    if (!W || !H || !vw || !vh) return;
    const s = Math.min(W / vw, H / vh), cw = vw * s, ch = vh * s, cx = (W - cw) / 2, cy = (H - ch) / 2;
    const [tb, lr] = masks;
    // Sadece videonun icindeki bant alanini kapla; disaridaki bosluk zaten alttaki isikla dolu.
    // Video ekranin altina kadar iniyorsa en alttaki 64 px'i birak: oynatici kontrolleri orada.
    if (crop.t > 0 || crop.b > 0) {
      const a1 = cy, a2 = cy + crop.t * ch, b1 = cy + ch * (1 - crop.b), b2 = Math.min(cy + ch, H - 64);
      tb.el.style.clipPath = `polygon(0 ${a1}px,${W}px ${a1}px,${W}px ${a2}px,0 ${a2}px,0 ${b1}px,${W}px ${b1}px,${W}px ${Math.max(b1, b2)}px,0 ${Math.max(b1, b2)}px)`;
      tb.el.style.display = "block";
    } else tb.el.style.display = "none";
    if (crop.l > 0 || crop.r > 0) {
      const a1 = cx, a2 = cx + crop.l * cw, b1 = cx + cw * (1 - crop.r), b2 = cx + cw;
      lr.el.style.clipPath = `polygon(${a1}px 0,${a1}px ${H}px,${a2}px ${H}px,${a2}px 0,${b1}px 0,${b1}px ${H}px,${b2}px ${H}px,${b2}px 0)`;
      lr.el.style.display = "block";
    } else lr.el.style.display = "none";
  }

  function enter(v) {
    if (stage) return;
    video = v;
    saved = { style: v.getAttribute("style"), controls: v.controls, playsinline: v.playsInline };
    marker = document.createComment("ambiance-video");
    v.parentNode.insertBefore(marker, v);

    stage = document.createElement("div");
    stage.className = "stage " + style;
    canvas = document.createElement("canvas");
    canvas.className = "glow";
    ctx = canvas.getContext("2d", { alpha: false });
    masks = [0, 1].map(() => {
      const el = document.createElement("div");
      el.className = "mask";
      const c = document.createElement("canvas");
      c.className = "glow";
      const sh = document.createElement("div");
      sh.className = "shade";          // arkadaki isikla birebir ayni gorunsun diye ayni kararma
      el.append(c, sh);
      return { el, canvas: c, ctx: c.getContext("2d", { alpha: false }) };
    });
    sizeGlow();
    const shade = document.createElement("div");
    shade.className = "shade";
    bar = document.createElement("div");
    bar.className = "bar";
    barsBtn = document.createElement("button");
    barsBtn.addEventListener("click", () => {
      barsMode = MODES[(MODES.indexOf(barsMode) + 1) % MODES.length];
      store.set("bars", barsMode);
      applyBarsMode();
      pokeBar();
    });
    const styleBtn = document.createElement("button");
    styleBtn.textContent = style === "detailed" ? S[3] : S[2];
    styleBtn.addEventListener("click", () => {
      style = style === "soft" ? "detailed" : "soft";
      store.set("style", style);
      stage.className = "stage " + style;
      sizeGlow();
      styleBtn.textContent = style === "detailed" ? S[3] : S[2];
      pokeBar();
    });
    const exitBtn = document.createElement("button");
    exitBtn.textContent = "✕  " + S[1];
    exitBtn.addEventListener("click", exit);
    bar.append(barsBtn, styleBtn, exitBtn);
    stage.append(canvas, shade);
    root.appendChild(stage);

    // videoyu sahneye tasi (ayni gorevde geri eklendigi icin oynatma durmaz)
    v.style.cssText = FILL;
    const want = v.style.cssText;   // tarayicinin normallestirdigi hali (inset -> top/right/... gibi)
    // YouTube gibi oynaticilar boyut degisince videonun style'ini yeniden yazar; geri al
    styleGuard = new MutationObserver(() => { if (stage && v.style.cssText !== want) v.style.cssText = FILL; });
    styleGuard.observe(v, { attributes: true, attributeFilter: ["style"] });
    v.controls = true;
    v.playsInline = true;
    stage.appendChild(v);
    for (const m of masks) stage.appendChild(m.el);
    stage.appendChild(bar);
    btn.classList.add("hidden");
    saved.overflow = document.documentElement.style.overflow;   // sayfanin kaydirma cubugu gorunmesin
    document.documentElement.style.overflow = "hidden";

    stage.addEventListener("pointerdown", pokeBar, { passive: true });
    stage.addEventListener("pointermove", pokeBar, { passive: true });
    document.addEventListener("keydown", onKey, true);
    window.addEventListener("resize", layoutMasks);
    v.addEventListener("loadedmetadata", applyBarsMode);
    readable = true; probe = null;
    applyBarsMode();
    pokeBar();

    // Gercek tam ekran dene; iframe'e izin verilmemisse (ya da reddedilirse) dis sayfadan bu cerceveyi buyutmesini iste
    const canFs = document.fullscreenEnabled || document.webkitFullscreenEnabled;
    const req = stage.requestFullscreen || stage.webkitRequestFullscreen;
    const askParent = () => { if (inFrame && stage && !askedParent) { askedParent = true; tell("enter"); } };
    if (req && canFs) { try { const p = req.call(stage); if (p && p.catch) p.catch(askParent); } catch (e) { askParent(); } }
    else askParent();
    document.addEventListener("fullscreenchange", onFs);
    document.addEventListener("webkitfullscreenchange", onFs);
    if (v.paused) { const p = v.play(); if (p && p.catch) p.catch(() => {}); }
    tick();
  }

  // video karesini (bantlar haric) kucuk tuvalin ortasina ciz, kenar renklerini paya uzat; CSS bulaniklastirip ekrana yayar
  function draw() {
    if (!stage || !video || video.readyState < 2) return;
    const vw = video.videoWidth, vh = video.videoHeight;
    if (!vw || !vh) return;
    const c = crop;
    try {
      ctx.drawImage(video, c.l * vw, c.t * vh, vw * (1 - c.l - c.r), vh * (1 - c.t - c.b), P, P, GW, GH);
      ctx.drawImage(canvas, P, P, GW, 1, P, 0, GW, P);                       // ust kenari yukari uzat
      ctx.drawImage(canvas, P, P + GH - 1, GW, 1, P, P + GH, GW, P);         // alt kenari asagi
      ctx.drawImage(canvas, P, 0, 1, GH + 2 * P, 0, 0, P, GH + 2 * P);       // sol kenari sola (koseler dahil)
      ctx.drawImage(canvas, P + GW - 1, 0, 1, GH + 2 * P, P + GW, 0, P, GH + 2 * P);
      for (const m of masks) if (m.el.style.display === "block") m.ctx.drawImage(canvas, 0, 0);
    } catch (e) { /* cizilemeyen video */ }
  }
  function tick() {
    if (!stage) return;
    draw();
    const now = Date.now();
    if (barsMode === "auto" && readable && now - lastProbe > 500) { lastProbe = now; detectBars(); }
    if (video.requestVideoFrameCallback && !video.paused) loopId = video.requestVideoFrameCallback(tick);
    else loopId = setTimeout(tick, video.paused ? 250 : 33);
  }

  function pokeBar(ms) {
    if (!bar) return;
    bar.classList.remove("hidden");
    clearTimeout(barTimer);
    barTimer = setTimeout(() => bar && bar.classList.add("hidden"), typeof ms === "number" ? ms : HIDE_MS);
  }
  function onKey(e) { if (e.key === "Escape") exit(); }
  function onFs() {
    layoutMasks();
    const fs = document.fullscreenElement || document.webkitFullscreenElement;
    if (!fs && stage && !askedParent) exit();   // kullanici tam ekrandan cikti
  }

  function exit() {
    if (!stage) return;
    const v = video, s = stage;
    stage = null;
    if (styleGuard) { styleGuard.disconnect(); styleGuard = null; }
    clearTimeout(loopId);
    if (v.cancelVideoFrameCallback) { try { v.cancelVideoFrameCallback(loopId); } catch (e) { /* yok */ } }
    document.removeEventListener("keydown", onKey, true);
    document.removeEventListener("fullscreenchange", onFs);
    document.removeEventListener("webkitfullscreenchange", onFs);
    window.removeEventListener("resize", layoutMasks);
    v.removeEventListener("loadedmetadata", applyBarsMode);
    const fs = document.fullscreenElement || document.webkitFullscreenElement;
    if (fs) { const x = document.exitFullscreen || document.webkitExitFullscreen; if (x) { try { const p = x.call(document); if (p && p.catch) p.catch(() => {}); } catch (e) { /* zaten cikti */ } } }
    // videoyu yerine ve eski haline geri koy
    if (marker && marker.parentNode) marker.parentNode.replaceChild(v, marker);
    if (saved.style === null) v.removeAttribute("style"); else v.setAttribute("style", saved.style);
    v.controls = saved.controls;
    v.playsInline = saved.playsinline;
    document.documentElement.style.overflow = saved.overflow;
    if (askedParent) { askedParent = false; tell("exit"); }
    s.remove();
    video = marker = canvas = ctx = bar = barsBtn = null;
    masks = [];
    // tam ekrandan cikista bazi oynaticilar duraklatir; oynuyorsa devam etsin
    if (v.paused === false) { const p = v.play(); if (p && p.catch) p.catch(() => {}); }
  }
})();
