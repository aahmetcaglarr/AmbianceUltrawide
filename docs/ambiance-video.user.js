// ==UserScript==
// @name         Ambiance Video
// @namespace    https://aahmetcaglarr.github.io/AmbianceUltrawide/
// @version      1.2.0
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
    en: ["Ambient fullscreen", "Exit", "Soft", "Detailed"],
    tr: ["Ambiyans tam ekran", "Çık", "Sade", "Detaylı"],
    es: ["Pantalla completa ambiental", "Salir", "Suave", "Detallado"],
    pt: ["Tela cheia com luz ambiente", "Sair", "Suave", "Detalhado"],
    fr: ["Plein écran d'ambiance", "Quitter", "Doux", "Détaillé"],
    de: ["Ambiente-Vollbild", "Beenden", "Sanft", "Detailliert"],
    ru: ["Полный экран с подсветкой", "Выйти", "Мягкий", "Детальный"],
    zh: ["氛围灯全屏", "退出", "柔和", "细节"],
    ja: ["アンビエント全画面", "終了", "ソフト", "詳細"],
    ko: ["앰비언트 전체 화면", "나가기", "부드럽게", "상세"],
    it: ["Schermo intero ambientale", "Esci", "Morbido", "Dettagliato"],
    pl: ["Pełny ekran z podświetleniem", "Wyjdź", "Łagodny", "Szczegółowy"],
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
    .badge { position: fixed; left: 6px; bottom: 6px; z-index: 2147483646; max-width: calc(100% - 12px); padding: 4px 8px; border-radius: 6px;
      background: rgba(255,138,61,.92); color: #111; font: 600 11px/1.3 -apple-system, system-ui, sans-serif; pointer-events: none; transition: opacity .3s; }
    .badge.hidden { opacity: 0; }
    .stage { position: fixed; inset: 0; z-index: 2147483647; background: #000; overflow: hidden; }
    .glow { position: absolute; inset: 0; width: 100%; height: 100%; transform: scale(1.18); transition: filter .3s; }
    .soft .glow { filter: blur(70px) saturate(1.35) brightness(.95); }
    .detailed .glow { filter: blur(22px) saturate(1.25) brightness(.9); }
    .shade { position: absolute; inset: 0; pointer-events: none; background: radial-gradient(ellipse at center, rgba(0,0,0,0) 45%, rgba(0,0,0,.35) 100%); }
    .bar { position: absolute; top: max(14px, env(safe-area-inset-top)); right: max(14px, env(safe-area-inset-right)); display: flex; gap: 8px;
      transition: opacity .25s; z-index: 3; }
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

  // ---------- teshis etiketi (gecici) ----------
  // Betigin calistigi her sayfa ve cercevede kisa sure bilgi gosterir; iPad'de gelistirici araci olmadan sorun bulmak icin.
  const VERSION = "1.2.0";
  const inFrame = window.top !== window;
  const badge = document.createElement("div");
  badge.className = "badge hidden";
  root.appendChild(badge);
  let badgeTimer = 0;
  function debugBadge(ms) {
    const d = deep();
    const big = d.videos.filter(bigEnough).length;
    const fs = document.fullscreenEnabled || document.webkitFullscreenEnabled;
    badge.textContent = "Ambiance " + VERSION + " · " + (inFrame ? "iframe " + location.hostname : "page") +
      " · video " + d.videos.length + " (" + big + " big) · iframe " + d.frames.length + " · fullscreen " + (fs ? "yes" : "no");
    badge.classList.remove("hidden");
    clearTimeout(badgeTimer);
    badgeTimer = setTimeout(() => badge.classList.add("hidden"), ms);
  }

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
    if (e.type !== "pointermove") debugBadge(4000);
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
  const LIFT = "position:fixed!important;inset:0!important;width:100vw!important;height:100vh!important;max-width:none!important;" +
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
  let stage = null, video = null, marker = null, saved = null, canvas = null, ctx = null, bar = null, loopId = 0, barTimer = 0, styleGuard = null, askedParent = false;
  const FILL = "position:absolute!important;inset:0!important;width:100%!important;height:100%!important;" +
    "max-width:none!important;max-height:none!important;object-fit:contain!important;transform:none!important;" +
    "margin:0!important;z-index:2!important;background:transparent!important;display:block!important;opacity:1!important;visibility:visible!important";

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
    const detailed = style === "detailed";
    canvas.width = detailed ? 128 : 48; canvas.height = detailed ? 72 : 27;
    ctx = canvas.getContext("2d", { alpha: false });
    const shade = document.createElement("div");
    shade.className = "shade";
    bar = document.createElement("div");
    bar.className = "bar";
    const styleBtn = document.createElement("button");
    styleBtn.textContent = detailed ? S[3] : S[2];
    styleBtn.addEventListener("click", () => {
      style = style === "soft" ? "detailed" : "soft";
      store.set("style", style);
      stage.className = "stage " + style;
      const d = style === "detailed";
      canvas.width = d ? 128 : 48; canvas.height = d ? 72 : 27;
      styleBtn.textContent = d ? S[3] : S[2];
      pokeBar();
    });
    const exitBtn = document.createElement("button");
    exitBtn.textContent = "✕  " + S[1];
    exitBtn.addEventListener("click", exit);
    bar.append(styleBtn, exitBtn);
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
    stage.appendChild(bar);
    btn.classList.add("hidden");
    saved.overflow = document.documentElement.style.overflow;   // sayfanin kaydirma cubugu gorunmesin
    document.documentElement.style.overflow = "hidden";

    stage.addEventListener("pointerdown", pokeBar, { passive: true });
    stage.addEventListener("pointermove", pokeBar, { passive: true });
    document.addEventListener("keydown", onKey, true);
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

  // video karesini kucuk tuvale ciz; CSS bulaniklastirip ekrana yayar
  function draw() {
    if (!stage || !video) return;
    if (video.readyState >= 2) { try { ctx.drawImage(video, 0, 0, canvas.width, canvas.height); } catch (e) { /* cizilemeyen video */ } }
  }
  function tick() {
    if (!stage) return;
    draw();
    if (video.requestVideoFrameCallback && !video.paused) loopId = video.requestVideoFrameCallback(tick);
    else loopId = setTimeout(tick, video.paused ? 250 : 33);
  }

  function pokeBar() {
    if (!bar) return;
    bar.classList.remove("hidden");
    clearTimeout(barTimer);
    barTimer = setTimeout(() => bar && bar.classList.add("hidden"), HIDE_MS);
  }
  function onKey(e) { if (e.key === "Escape") exit(); }
  function onFs() {
    const fs = document.fullscreenElement || document.webkitFullscreenElement;
    if (!fs && stage) exit();   // kullanici tam ekrandan cikti
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
    video = marker = canvas = ctx = bar = null;
    // tam ekrandan cikista bazi oynaticilar duraklatir; oynuyorsa devam etsin
    if (v.paused === false) { const p = v.play(); if (p && p.catch) p.catch(() => {}); }
  }

  // teshis: sayfa acilinca bir kez goster (oynaticilar videoyu gec ekledigi icin biraz bekle)
  setTimeout(() => debugBadge(12000), 2500);
})();
