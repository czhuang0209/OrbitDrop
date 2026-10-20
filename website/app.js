// OrbitDrop website: a live playground (drag a file, tap Shift, let go) and interactive wheel previews.
// The wheels mirror the app: same slices, same order, groups that open when you rest on them, three modes.
(() => {
  "use strict";
  const $ = (s, el = document) => el.querySelector(s);
  const $$ = (s, el = document) => [...el.querySelectorAll(s)];
  const NS = "http://www.w3.org/2000/svg";
  const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const STORE = "https://apps.microsoft.com/detail/9NJQRMQLL2SZ?mode=direct&cid=website-";

  // ------------------------------------------------------------------ icons (24 × 24, stroked)
  const ICONS = {
    edit: "M4 20h4L19 9l-4-4L4 16v4zM14 6l4 4",
    compress: "M4 14h6v6M20 10h-6V4M14 10l6-6M4 20l6-6",
    resize: "M14 4h6v6M10 20H4v-6M20 4l-7 7M4 20l7-7",
    rotate: "M20 12a8 8 0 1 1-2.3-5.6M20 4v5h-5",
    gray: "M12 3a9 9 0 1 0 0 18zM12 3a9 9 0 0 1 0 18",
    text: "M5 6V4h14v2M12 4v16M9 20h6",
    trim: "M6 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM8.1 7.9 20 20M8.1 16.1 20 4",
    snapshot: "M4 8h3l2-3h6l2 3h3v11H4zM12 17a4 4 0 1 0 0-8 4 4 0 0 0 0 8z",
    mute: "M4 9h4l5-4v14l-5-4H4zM17 9l4 6M21 9l-4 6",
    info: "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18zM12 11v6M12 7.5v.5",
    wave: "M3 12h2M7 8v8M11 5v14M15 8v8M19 10v4M21 12h0",
    channels: "M4 15v-3a8 8 0 0 1 16 0v3M4 15h3v5H4zM17 15h3v5h-3z",
    link: "M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1",
    folder: "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
    copy: "M9 9h11v11H9zM5 15H4V4h11v1",
    move: "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2zM10 13h6M13 10l3 3-3 3",
    pin: "M15 4l5 5-3 1-3 3 1 4-2 1-4-4-5 5M9 11l3-3 1-3",
    share: "M14 5l6 6-6 6M20 11H11a7 7 0 0 0-7 7",
    zip: "M4 7h16v13H4zM3 4h18v3H3zM10 11h4",
    terminal: "M4 5h16v14H4zM8 10l2 2-2 2M12 15h4",
    wallpaper: "M3 5h18v11H3zM8 20h8M12 16v4",
  };
  const icon = (name, size = 22) => {
    const svg = document.createElementNS(NS, "svg");
    svg.setAttribute("viewBox", "0 0 24 24"); svg.setAttribute("width", size); svg.setAttribute("height", size);
    svg.innerHTML = `<path d="${ICONS[name]}" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>`;
    return svg;
  };

  // ------------------------------------------------------------------ what each kind of file offers (as in the app)
  const folders = (verb) => ["Documents", "Pictures", "Desktop", "Choose a folder…"].map((f) => ({ label: f, act: verb, dest: f, detail: `${verb === "copy" ? "Copy" : "Move"} to ${f}` }));
  const actions = (withWallpaper) => [
    { label: "Copy path", icon: "link", group: [{ label: "Copy path", act: "path", detail: "Full path to the clipboard" }, { label: "Copy name", act: "name", detail: "Just the file name" }] },
    { label: "Show in folder", icon: "folder", act: "show", detail: "Open File Explorer there" },
    { label: "Copy to", icon: "copy", group: folders("copy") },
    { label: "Move to", icon: "move", group: folders("move") },
    { label: "Float", icon: "pin", act: "float", detail: "Keep it on a shelf" },
    { label: "Share", icon: "share", act: "share", detail: "Nearby Share and apps" },
    { label: "Zip", icon: "zip", act: "zip", detail: "Put it in a ZIP" },
    { label: "Terminal", icon: "terminal", act: "terminal", detail: "Open a terminal here" },
    ...(withWallpaper ? [{ label: "Wallpaper", icon: "wallpaper", act: "wallpaper", detail: "Set as desktop wallpaper" }] : []),
  ];
  const to = (exts, kind) => exts.map((e) => ({ label: e, act: "convert", ext: e.toLowerCase(), detail: kind === "audio-out" ? `Extract audio as ${e}` : `Convert to ${e}`, slow: kind !== "image" }));
  const imageTools = [
    { label: "Edit", icon: "edit", act: "edit", detail: "Crop, adjust, mark up or redact" },
    { label: "Compress", icon: "compress", group: [
      { label: "50%", act: "tool", suffix: "compressed", detail: "Compress to about half the size" },
      { label: "75%", act: "tool", suffix: "compressed", detail: "Compress to about three quarters" },
      { label: "25%", act: "tool", suffix: "compressed", detail: "Compress to about a quarter" },
      { label: "1 MB", act: "tool", suffix: "compressed", detail: "Compress to under 1 MB" },
      { label: "500 KB", act: "tool", suffix: "compressed", detail: "Compress to under 500 KB" },
      { label: "200 KB", act: "tool", suffix: "compressed", detail: "Compress to under 200 KB" }] },
    { label: "Resize", icon: "resize", group: ["50%", "1920 px", "1280 px", "800 px"].map((s) => ({ label: s, act: "tool", suffix: "resized", detail: s.endsWith("%") ? "Half the width and height" : `Fit within ${s}` })) },
    { label: "Rotate & flip", icon: "rotate", group: ["Rotate right", "Rotate left", "Rotate 180°", "Flip horizontal", "Flip vertical"].map((s) => ({ label: s, act: "tool", suffix: s.startsWith("Flip") ? "flipped" : "rotated", detail: s })) },
    { label: "Grayscale", icon: "gray", act: "tool", suffix: "grayscale", detail: "Black and white copy" },
    { label: "Copy text", icon: "text", act: "ocr", detail: "Copy the text in the image" },
  ];
  const KINDS = {
    image: {
      convert: (ext) => to(["JPG", "PNG", "BMP", "TIFF", "GIF", "PDF"].filter((e) => e.toLowerCase() !== ext && !(ext === "jpg" && e === "JPG")), "image"),
      tools: () => imageTools, actions: () => actions(true),
    },
    video: {
      convert: () => [...to(["WMV", "GIF"], "video"), ...to(["MP3", "M4A", "WAV", "WMA", "FLAC"], "audio-out")],
      tools: () => [
        { label: "Trim", icon: "trim", act: "trim", detail: "Keep just the part you choose" },
        { label: "Compress", icon: "compress", group: ["1080p", "720p", "480p", "Half size"].map((s) => ({ label: s, act: "tool", suffix: "compressed", slow: true, detail: s === "Half size" ? "Half the width and height" : `Compress to ${s}` })) },
        { label: "Snapshot", icon: "snapshot", act: "snapshot", detail: "Save the middle frame as PNG" },
        { label: "Remove audio", icon: "mute", act: "tool", suffix: "no audio", slow: true, detail: "A silent copy" },
        { label: "Info", icon: "info", act: "info", detail: "See and edit details" },
      ],
      actions: () => actions(false),
    },
    audio: {
      convert: () => to(["MP3", "M4A", "FLAC", "WMA"], "audio"),
      tools: () => [
        { label: "Trim", icon: "trim", act: "trim", detail: "Keep just the part you choose" },
        { label: "Compress", icon: "compress", group: ["128 kbps", "96 kbps", "64 kbps"].map((s) => ({ label: s, act: "tool", suffix: "compressed", slow: true, detail: `Compress to ${s}` })) },
        { label: "Normalize", icon: "wave", act: "tool", suffix: "normalized", slow: true, detail: "As loud as it can be, no clipping" },
        { label: "Channels", icon: "channels", group: [{ label: "Mono", act: "tool", suffix: "mono", slow: true, detail: "Make it mono" }, { label: "Stereo", act: "tool", suffix: "stereo", slow: true, detail: "Make it stereo" }] },
        { label: "Info", icon: "info", act: "info", detail: "See and edit details" },
      ],
      actions: () => actions(false),
    },
  };
  const MODES = ["convert", "tools", "actions"];
  const kindOf = (name) => /\.(png|jpe?g|gif|bmp|tiff?)$/i.test(name) ? "image" : /\.(mp4|wmv)$/i.test(name) ? "video" : /\.(wav|mp3|m4a|flac|wma)$/i.test(name) ? "audio" : /\.zip$/i.test(name) ? "zip" : "image";
  const extOf = (name) => name.split(".").pop().toLowerCase();
  const baseOf = (name) => name.replace(/\.[^.]+$/, "");
  const menuFor = (name, mode) => {
    const k = kindOf(name);
    if (k === "zip") return mode === "actions" ? actions(false) : [];
    return KINDS[k][mode](extOf(name));
  };

  // ------------------------------------------------------------------ the wheel (SVG, same geometry idea as the app)
  const R = 150, R0 = 64, HUB = 54;
  const polar = (a, r) => [Math.cos(a) * r, Math.sin(a) * r];
  function sector(i, n) {
    const span = (Math.PI * 2) / n, mid = -Math.PI / 2 + i * span, a0 = mid - span / 2, a1 = mid + span / 2;
    const [x0, y0] = polar(a0, R), [x1, y1] = polar(a1, R), [x2, y2] = polar(a1, R0), [x3, y3] = polar(a0, R0);
    const large = span > Math.PI ? 1 : 0;
    if (n === 1) return `M0 ${-R}A${R} ${R} 0 1 1 0 ${R}A${R} ${R} 0 1 1 0 ${-R}zM0 ${-R0}A${R0} ${R0} 0 1 0 0 ${R0}A${R0} ${R0} 0 1 0 0 ${-R0}z`;
    return `M${x0} ${y0}A${R} ${R} 0 ${large} 1 ${x1} ${y1}L${x2} ${y2}A${R0} ${R0} 0 ${large} 0 ${x3} ${y3}z`;
  }

  class Wheel {
    constructor(host, { size = 300, interactive = true } = {}) {
      this.el = document.createElement("div");
      this.el.className = "wheel";
      this.el.style.width = this.el.style.height = `${size}px`;
      this.size = size;
      this.svg = document.createElementNS(NS, "svg");
      this.svg.setAttribute("viewBox", `${-R - 8} ${-R - 8} ${2 * R + 16} ${2 * R + 16}`);
      this.el.append(this.svg);
      this.hub = document.createElement("div");
      this.hub.className = "hub";
      this.hub.innerHTML = `<span class="hub-mode"></span><b class="hub-value"></b><span class="hub-detail"></span><span class="hub-dots"><i></i><i></i><i></i></span>`;
      this.el.append(this.hub);
      host.append(this.el);
      this.items = []; this.index = -1; this.mode = "convert";
      if (interactive) {
        this.el.addEventListener("pointermove", (e) => this.hoverAt(e.clientX, e.clientY));
        this.el.addEventListener("pointerleave", () => this.highlight(-1));
      }
    }
    set(items, mode, value, detail, spin = false) {
      this.items = items; this.mode = mode; this.idleValue = value; this.idleDetail = detail; this.index = -1;
      const n = Math.max(1, items.length);
      this.svg.innerHTML = `<circle class="disc" r="${R + 6}"/>`;
      this.labels = [];
      items.forEach((it, i) => {
        const g = document.createElementNS(NS, "g");
        g.setAttribute("class", "seg");
        g.innerHTML = `<path d="${sector(i, n)}"/>`;
        const span = (Math.PI * 2) / n, mid = -Math.PI / 2 + i * span;
        const [lx, ly] = polar(mid, (R + R0) / 2 + 2);
        const fo = document.createElementNS(NS, "foreignObject");
        fo.setAttribute("x", lx - 46); fo.setAttribute("y", ly - 30); fo.setAttribute("width", 92); fo.setAttribute("height", 60);
        const lab = document.createElement("div");
        lab.className = `seg-label${n > 6 ? " small" : ""}`;
        if (it.icon) lab.append(icon(it.icon, n > 6 ? 18 : 21));
        const t = document.createElement("span"); t.textContent = it.label; lab.append(t);
        if (it.group) { const d = document.createElement("em"); d.textContent = "•••"; lab.append(d); }
        fo.append(lab); g.append(fo);
        this.svg.append(g);
      });
      this.hub.querySelector(".hub-mode").textContent = mode.toUpperCase();
      $$(".hub-dots i", this.hub).forEach((d, k) => d.classList.toggle("on", MODES[k] === mode));
      this.showHub(value, detail);
      if (spin && !reduceMotion) { this.el.classList.remove("spin"); void this.el.offsetWidth; this.el.classList.add("spin"); }
    }
    showHub(value, detail) { this.hub.querySelector(".hub-value").textContent = value; this.hub.querySelector(".hub-detail").textContent = detail; }
    highlight(i) {
      if (i === this.index) return;
      this.index = i;
      $$(".seg", this.svg).forEach((g, k) => g.classList.toggle("on", k === i));
      const it = this.items[i];
      if (it) this.showHub(it.label, it.group ? "Rest for more" : it.detail);
      else this.showHub(this.idleValue, this.idleDetail);
      this.onHighlight?.(it, i);
    }
    /** Which slice is under a screen point: -1 none, -2 the hub. */
    hit(cx, cy) {
      const r = this.el.getBoundingClientRect();
      const k = (2 * R + 16) / r.width;
      const x = (cx - r.left - r.width / 2) * k, y = (cy - r.top - r.height / 2) * k;
      const d = Math.hypot(x, y);
      if (d < R0 - 4) return -2;
      if (d > R + 70 || !this.items.length) return -1;
      const n = this.items.length, span = 360 / n;
      let a = (Math.atan2(y, x) * 180) / Math.PI + 90 + span / 2;
      a = ((a % 360) + 360) % 360;
      return Math.floor(a / span) % n;
    }
    hoverAt(cx, cy) { const i = this.hit(cx, cy); this.highlight(i >= 0 ? i : -1); return i; }
  }

  // ------------------------------------------------------------------ file thumbnails (drawn, like the app's sample files)
  function thumb(name) {
    const k = kindOf(name), ext = extOf(name);
    const label = `<span class="badge">${ext.toUpperCase()}</span>`;
    if (k === "video") return `<div class="thumb video"><svg viewBox="0 0 64 64"><rect x="4" y="10" width="56" height="44" rx="8" fill="#16161A"/><rect x="4" y="16" width="56" height="2" fill="#2E2E35"/><rect x="4" y="46" width="56" height="2" fill="#2E2E35"/><path d="M27 24l13 8-13 8z" fill="#fff"/></svg>${label}</div>`;
    if (k === "audio") return `<div class="thumb audio"><svg viewBox="0 0 64 64"><rect x="6" y="6" width="52" height="52" rx="12" fill="#26262C"/><g stroke="#fff" stroke-width="3" stroke-linecap="round"><path d="M18 28v8M24 22v20M30 17v30M36 24v16M42 20v24M48 28v8"/></g></svg>${label}</div>`;
    if (k === "zip") return `<div class="thumb zip"><svg viewBox="0 0 64 64"><rect x="10" y="8" width="44" height="50" rx="6" fill="#E9E9EE" stroke="#C9C9D1"/><path d="M32 8v6h-4v6h4v6h-4v6h4" stroke="#8E8E96" stroke-width="3" fill="none"/><rect x="26" y="34" width="12" height="10" rx="2" fill="#8E8E96"/></svg>${label}</div>`;
    const gray = /grayscale/.test(name);
    const sky = gray ? ["#9A9AA0", "#6E6E74"] : name.startsWith("beach") ? ["#8EC5FF", "#DCEBFF"] : ["#5B6EA8", "#E9A87C"];
    const land = gray ? "#3B3B40" : name.startsWith("beach") ? "#E9D7A8" : "#2E3550";
    const flip = /flipped|rotated/.test(name) ? ' transform="rotate(90 32 32)"' : "";
    return `<div class="thumb photo"><svg viewBox="0 0 64 64"><defs><linearGradient id="g-${name.replace(/\W/g, "")}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${sky[0]}"/><stop offset="1" stop-color="${sky[1]}"/></linearGradient><clipPath id="c-${name.replace(/\W/g, "")}"><rect x="6" y="6" width="52" height="52" rx="10"/></clipPath></defs><g clip-path="url(#c-${name.replace(/\W/g, "")})"${flip}><rect x="6" y="6" width="52" height="52" fill="url(#g-${name.replace(/\W/g, "")})"/><circle cx="41" cy="27" r="7" fill="${gray ? "#E5E5E5" : "#FFE3A3"}"/><path d="M6 44q14-10 26-2t26-4v20H6z" fill="${land}"/></g></svg>${label}</div>`;
  }

  // ------------------------------------------------------------------ the playground
  const scene = $("#playground");
  if (scene) playground(scene);

  function playground(scene) {
    const desk = $(".desk", scene), hint = $("#hint"), hintText = $("#hint-text"), toasts = $(".toasts", scene), shelf = $(".shelf", scene);
    const keyBtn = $("#shift-key");
    const START = ["photo.png", "beach.jpg", "clip.mp4", "song.wav"];
    let files = [];
    const slots = 10, perCol = 4;
    const ghost = document.createElement("div"); ghost.className = "ghost"; scene.append(ghost);
    const wheelHost = document.createElement("div"); wheelHost.className = "wheel-layer"; scene.append(wheelHost);
    const wheel = new Wheel(wheelHost, { size: 300, interactive: false });
    wheel.el.classList.add("live");

    let drag = null; // { file, el, sx, sy, x, y, moved, pointerId, touch }
    let open = null; // { file, mode, stack: [items], cx, cy, click }
    let dwell = 0, dwellTarget = null, tries = 0;

    function setHint(text, keyed = false) {
      hintText.innerHTML = text;
      hint.classList.toggle("keyed", keyed);
      hint.classList.remove("pop"); void hint.offsetWidth; hint.classList.add("pop");
    }
    const pos = (i) => ({ left: 28 + Math.floor(i / perCol) * 108, top: 22 + (i % perCol) * 104 });
    function render() {
      desk.innerHTML = "";
      files.forEach((f, i) => {
        const b = document.createElement("button");
        b.className = `file${f.fresh ? " fresh" : ""}`; b.type = "button";
        b.dataset.name = f.name;
        b.setAttribute("aria-label", `${f.name}: drag it, then tap Shift`);
        const p = pos(i); b.style.left = `${p.left}px`; b.style.top = `${p.top}px`;
        b.innerHTML = `${thumb(f.name)}<span class="name">${f.name}</span>`;
        b.addEventListener("pointerdown", (e) => down(e, f, b));
        b.addEventListener("keydown", (e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); const r = b.getBoundingClientRect(); openWheel(f, r.left + r.width / 2, r.top + r.height / 2, true); } });
        desk.append(b);
        f.fresh = false;
      });
    }
    // A dashed arrow from the hint to the first file, until someone tries it.
    const arrow = document.createElementNS(NS, "svg");
    arrow.setAttribute("class", "hint-arrow"); arrow.setAttribute("aria-hidden", "true");
    scene.append(arrow);
    function drawArrow() {
      const s = scene.getBoundingClientRect(), h = hint.getBoundingClientRect(), f = desk.firstElementChild?.getBoundingClientRect();
      if (!f) return;
      const x0 = h.left - s.left - 8, y0 = h.top - s.top + h.height / 2;
      const x1 = f.right - s.left + 6, y1 = f.top - s.top + 26;
      const mx = (x0 + x1) / 2;
      arrow.innerHTML = `<path d="M${x0} ${y0} C${mx} ${y0} ${mx + 20} ${y1} ${x1} ${y1}"/><path class="head" d="M${x1 + 9} ${y1 - 6} L${x1} ${y1} L${x1 + 9} ${y1 + 6}"/>`;
    }
    addEventListener("resize", drawArrow);
    function reset() { files = START.map((name) => ({ name })); render(); closeWheel(); scene.classList.remove("wall"); shelf.classList.remove("on"); shelf.innerHTML = ""; setHint("Drag a file"); scene.classList.add("idle"); requestAnimationFrame(drawArrow); }
    $("#reset")?.addEventListener("click", reset);
    reset();

    function addFile(name, after) {
      let n = name, k = 2;
      while (files.some((f) => f.name === n)) n = `${baseOf(name)} (${k++}).${extOf(name)}`;
      const at = files.findIndex((f) => f.name === after);
      files.splice(at + 1, 0, { name: n, fresh: true });
      if (files.length > slots) files.splice(files.findIndex((f, i) => i > at + 1 || i < at), 1);
      render();
      return n;
    }

    // --- pointer: press on a file, move to drag, Shift opens the wheel, release chooses
    function down(e, f, el) {
      if (open?.click) return;
      e.preventDefault();
      el.setPointerCapture?.(e.pointerId);
      drag = { file: f, el, sx: e.clientX, sy: e.clientY, x: e.clientX, y: e.clientY, moved: false, touch: e.pointerType !== "mouse" };
    }
    window.addEventListener("pointermove", (e) => {
      if (!drag) { if (open?.click) hover(e.clientX, e.clientY); return; }
      drag.x = e.clientX; drag.y = e.clientY;
      if (!drag.moved && Math.hypot(drag.x - drag.sx, drag.y - drag.sy) > 6) {
        drag.moved = true;
        scene.classList.remove("idle");
        drag.el.classList.add("lifted");
        ghost.innerHTML = thumb(drag.file.name);
        ghost.classList.add("on");
        if (drag.touch) setHint("Now tap the <kbd>⇧ Shift</kbd> key below", true);
        else setHint("Keep holding, and tap <kbd>⇧ Shift</kbd> on your keyboard", true);
      }
      if (drag.moved) {
        const r = scene.getBoundingClientRect();
        ghost.style.transform = `translate(${drag.x - r.left + 10}px, ${drag.y - r.top + 16}px)`;
        if (open) hover(drag.x, drag.y);
      }
    });
    window.addEventListener("pointerup", (e) => {
      if (open?.click && !drag) { choose(open.index ?? wheel.index); return; }
      if (!drag) return;
      const d = drag; drag = null;
      d.el.classList.remove("lifted"); ghost.classList.remove("on");
      if (!d.moved) { const r = d.el.getBoundingClientRect(); openWheel(d.file, r.left + r.width / 2, r.top + r.height / 2, true); return; }
      if (open) { choose(wheel.index); return; }
      setHint(++tries > 1 ? "Tip: tap <kbd>⇧ Shift</kbd> <i>while</i> you're still holding the file" : "Almost! Tap <kbd>⇧ Shift</kbd> while you're still holding the file", true);
    });
    window.addEventListener("keydown", (e) => {
      if (!scene.isConnected) return;
      if (e.key === "Shift" && !e.repeat) { if (shift()) e.preventDefault(); }
      else if (e.key === "Escape" && open) { closeWheel(); setHint("Cancelled. Nothing changed."); }
    });
    keyBtn?.addEventListener("pointerdown", (e) => { e.preventDefault(); e.stopPropagation(); if (!shift()) setHint("First press and drag a file, then tap <kbd>⇧ Shift</kbd>", true); });

    function shift() {
      if (open) { cycle(); return true; }
      if (drag?.moved) { openWheel(drag.file, drag.x, drag.y, false); return true; }
      return false;
    }

    function openWheel(file, x, y, click) {
      const r = scene.getBoundingClientRect();
      const half = wheel.size / 2;
      const cx = Math.min(Math.max(x - r.left, half + 8), r.width - half - 8);
      const cy = Math.min(Math.max(y - r.top, half + 8), r.height - half - 48);
      wheel.el.style.left = `${cx - half}px`; wheel.el.style.top = `${cy - half}px`;
      open = { file, mode: "convert", stack: [], click };
      scene.classList.remove("idle");
      show(false);
      wheelHost.classList.add("on");
      setHint(click ? "Click an option. Click the centre for Tools and Actions" : "Now move onto an option and let go", false);
    }
    function current() { return open.stack.length ? open.stack[open.stack.length - 1].group : menuFor(open.file.name, open.mode); }
    function show(spin) {
      const items = current();
      const top = open.stack[open.stack.length - 1];
      wheel.set(items, open.mode, top ? top.label : sizeOf(open.file.name), top ? "Rest here to go back" : open.file.name, spin);
      if (!items.length) wheel.showHub("Nothing here", "Tap Shift for Actions");
    }
    function cycle() {
      if (!open) return;
      open.mode = MODES[(MODES.indexOf(open.mode) + 1) % 3]; open.stack = [];
      show(true);
      const names = { convert: "Convert: new formats", tools: "Tools: quick edits", actions: "Actions: copy, move, share, zip" };
      setHint(`${names[open.mode]}. Tap <kbd>⇧ Shift</kbd> again to switch`, true);
    }
    function hover(x, y) {
      const i = wheel.hoverAt(x, y);
      // Resting on a slice with dots opens it; resting on the centre goes back.
      const target = i >= 0 && wheel.items[i]?.group ? `g${i}` : i === -2 && open.stack.length ? "back" : null;
      if (target !== dwellTarget) {
        clearTimeout(dwell); dwellTarget = target;
        if (target) dwell = setTimeout(() => {
          if (!open) return;
          if (target === "back") open.stack.pop(); else open.stack.push(wheel.items[i]);
          show(true); dwellTarget = null;
        }, 550);
      }
      if (open?.click && i === -2) wheel.showHub(wheel.idleValue, "Click for " + (open.mode === "convert" ? "Tools" : open.mode === "tools" ? "Actions" : "Convert"));
    }
    function choose(i) {
      if (!open) return;
      if (i === -2 && open.click) { cycle(); return; }
      const it = wheel.items[i];
      if (!it) { closeWheel(); setHint("Cancelled. Nothing changed. Try again!"); return; }
      if (it.group) { open.stack.push(it); show(true); return; }
      const file = open.file;
      closeWheel();
      perform(it, file);
    }
    function closeWheel() { wheelHost.classList.remove("on"); open = null; clearTimeout(dwell); dwellTarget = null; }
    // Click outside the wheel closes it in click mode.
    scene.addEventListener("pointerdown", (e) => { if (open?.click && !wheel.el.contains(e.target)) { closeWheel(); setHint("Closed. Drag a file and tap Shift, or click one"); } });
    wheel.el.addEventListener("pointerdown", (e) => { if (open?.click) { e.stopPropagation(); open.index = wheel.hoverAt(e.clientX, e.clientY); } });

    // --- results, the way the app shows them
    function sizeOf(name) { return { image: "2.4 MB", video: "18.4 MB", audio: "6.1 MB", zip: "2.2 MB" }[kindOf(name)]; }
    function toast(title, detail, { slow = false, progress = "" } = {}) {
      const t = document.createElement("div");
      t.className = "toast";
      t.innerHTML = `<span class="tick"></span><div><b></b><small></small></div><i class="bar"></i>`;
      toasts.append(t);
      const set = (a, b) => { $("b", t).textContent = a; $("small", t).textContent = b; };
      requestAnimationFrame(() => t.classList.add("on"));
      const done = () => { t.classList.add("done"); set(title, detail); setTimeout(() => { t.classList.remove("on"); setTimeout(() => t.remove(), 400); }, 3200); };
      if (slow && !reduceMotion) { set(progress, "Working on it…"); t.classList.add("running"); setTimeout(done, 1500); } else done();
      while (toasts.children.length > 2) toasts.firstElementChild.remove();
    }
    function perform(it, file) {
      const name = file.name, base = baseOf(name), ext = extOf(name);
      const praise = ++tries && files.length > 4 ? "Nice! Try another file, or tap Shift more than once" : "Done! Try Tools: drag again and tap Shift twice";
      switch (it.act) {
        case "convert": {
          const out = addFile(`${base}.${it.ext}`, name);
          toast(`Saved ${out}`, "On the desktop", { slow: it.slow, progress: `Converting to ${it.label}` });
          setHint(praise); break;
        }
        case "tool": {
          const outExt = it.suffix === "compressed" && kindOf(name) === "image" ? "jpg" : ext;
          const out = addFile(`${base} (${it.suffix}).${outExt}`, name);
          toast(`Saved ${out}`, "Your original is untouched", { slow: it.slow, progress: it.detail });
          setHint(praise); break;
        }
        case "snapshot": { const out = addFile(`${base} (snapshot).png`, name); toast(`Saved ${out}`, "The middle frame, as PNG"); setHint(praise); break; }
        case "zip": { const out = addFile(`${base}.zip`, name); toast(`Saved ${out}`, "On the desktop"); setHint(praise); break; }
        case "move": files = files.filter((f) => f.name !== name); render(); toast(`Moved to ${it.dest}`, name); setHint(praise); break;
        case "copy": toast(`Copied to ${it.dest}`, name); setHint(praise); break;
        case "path": toast("Path copied", `C:\\Users\\You\\Desktop\\${name}`); setHint(praise); break;
        case "name": toast("Name copied", name); setHint(praise); break;
        case "show": toast("Opening folder", "File Explorer, with the file selected"); setHint(praise); break;
        case "share": toast("Share opened", "Nearby Share, Mail, Teams…"); setHint(praise); break;
        case "terminal": toast("Terminal opened", "In the file's folder"); setHint(praise); break;
        case "ocr": toast("Text copied", "Paste it anywhere with Ctrl+V"); setHint(praise); break;
        case "info": toast("Info opened", "Camera, size, length, tags and more"); setHint(praise); break;
        case "trim": toast("Trim opened", "Drag the start and end, then Save"); setHint(praise); break;
        case "wallpaper": scene.classList.add("wall"); toast("Wallpaper set", name); setHint("Pretty! Press Reset to put things back"); break;
        case "float": {
          shelf.innerHTML = `<b>Shelf</b>${thumb(name)}<span>${name}</span>`; shelf.classList.add("on");
          toast("On the shelf", "Drag it out whenever you need it"); setHint(praise); break;
        }
        case "edit": $("#editor-dialog")?.showModal?.(); setHint(praise); break;
      }
    }
  }

  // ------------------------------------------------------------------ interactive previews (feature tabs and actions)
  const PREVIEW = {
    image: { file: "photo.png", cards: [
      ["Convert", "JPG, PNG, GIF, BMP, TIFF or PDF. Reads WebP, HEIC, AVIF and RAW too."],
      ["Compress", "To half the size, or under 1 MB, 500 KB or 200 KB."],
      ["Edit", "Crop, adjust, mark up and redact. Saves a copy."],
      ["Copy text", "Pull the text out of a screenshot or photo, offline."],
      ["Resize & rotate", "Fit within 1920, 1280 or 800 px. Rotate or flip."],
      ["Clean up", "Remove the location, or all metadata, before you share."],
    ] },
    video: { file: "clip.mp4", cards: [
      ["Convert", "MP4 or WMV, or an animated GIF."],
      ["Just the audio", "MP3, M4A, WAV, WMA or FLAC from any clip."],
      ["Compress", "1080p, 720p, 480p or half size, ready to send."],
      ["Trim", "Keep just the part you want. Play it before you save."],
      ["Snapshot", "Save the middle frame as a picture."],
      ["Join", "Drop several clips at once to make one video."],
    ] },
    audio: { file: "song.wav", cards: [
      ["Convert", "MP3, M4A, FLAC or WMA."],
      ["Compress", "128, 96 or 64 kbps for smaller files."],
      ["Normalize", "Quiet recordings, as loud as they can be. No clipping."],
      ["Trim", "Cut the silence, keep the good part."],
      ["Mono or stereo", "Switch channels in one move."],
      ["Details", "See and edit the artist, album and more."],
    ] },
  };
  $$(".preview").forEach((box) => {
    const mode = box.dataset.mode;
    const host = $(".preview-wheel", box), out = $(".preview-result", box);
    const w = new Wheel(host, { size: +box.dataset.size || 260 });
    w.onHighlight = (it) => { out.textContent = it ? (it.group ? `${it.label} · rest for more` : it.detail) : box.dataset.idle || "Point at an option"; };
    box.wheel = w;
    box.load = (file) => { w.set(menuFor(file, mode), mode, sizeOf(file), file); w.onHighlight(null); };
    box.load(box.dataset.file || "photo.png");
    // A gentle tour of the slices until someone points at it.
    let i = 0, touched = false;
    host.addEventListener("pointerenter", () => { touched = true; });
    host.addEventListener("pointerleave", () => { touched = false; });
    if (!reduceMotion) setInterval(() => { if (!touched && isOnScreen(box)) { w.highlight(i % w.items.length); i++; } }, 1400);
  });
  function sizeOf(name) { return { image: "2.4 MB", video: "18.4 MB", audio: "6.1 MB" }[kindOf(name)] || "2.4 MB"; }
  function isOnScreen(el) { const r = el.getBoundingClientRect(); return r.bottom > 0 && r.top < innerHeight; }

  const tabs = $$(".tabs button"), cards = $("#kind-cards");
  function selectKind(kind) {
    tabs.forEach((t) => { const on = t.dataset.kind === kind; t.setAttribute("aria-selected", on); t.tabIndex = on ? 0 : -1; });
    const p = PREVIEW[kind];
    $$(".kinds-panel .preview").forEach((box) => box.load(p.file));
    cards.innerHTML = p.cards.map(([h, t]) => `<article><h4>${h}</h4><p>${t}</p></article>`).join("");
    cards.classList.remove("in"); void cards.offsetWidth; cards.classList.add("in");
  }
  tabs.forEach((t, k) => {
    t.addEventListener("click", () => selectKind(t.dataset.kind));
    t.addEventListener("keydown", (e) => {
      const d = e.key === "ArrowRight" ? 1 : e.key === "ArrowLeft" ? -1 : 0;
      if (d) { const n = tabs[(k + d + tabs.length) % tabs.length]; n.focus(); selectKind(n.dataset.kind); }
    });
  });
  if (tabs.length) selectKind("image");

  // ------------------------------------------------------------------ keyboard section: the keys light up when pressed
  const keys = { Control: $("#k-ctrl"), Alt: $("#k-alt"), " ": $("#k-space"), Shift: $("#shift-key") };
  window.addEventListener("keydown", (e) => {
    keys[e.key]?.classList.add("down");
    if (e.ctrlKey && e.altKey && e.key === " ") $("#combo-ok")?.classList.add("on");
  });
  window.addEventListener("keyup", (e) => keys[e.key]?.classList.remove("down"));
  window.addEventListener("blur", () => Object.values(keys).forEach((k) => k?.classList.remove("down")));

  // ------------------------------------------------------------------ demo videos (light/dark), entrance animations
  const dark = matchMedia("(prefers-color-scheme: dark)");
  const vids = $$("video.demo");
  const pick = () => vids.forEach((v) => { const s = dark.matches ? v.dataset.dark : v.dataset.light; if (v.getAttribute("src") !== s) { v.poster = s.replace(/.mp4$/, ".jpg"); v.src = s; } });
  pick(); dark.addEventListener("change", pick);
  const io = new IntersectionObserver((es) => es.forEach((e) => {
    if (e.target.tagName === "VIDEO") { if (e.isIntersecting && !reduceMotion) e.target.play().catch(() => {}); else e.target.pause(); }
    else if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
  }), { threshold: 0.2 });
  vids.forEach((v) => io.observe(v));
  $$(".reveal").forEach((el) => io.observe(el));

  $$("[data-store]").forEach((a) => { a.href = STORE + a.dataset.store; });
  $("#editor-dialog")?.addEventListener("click", (e) => { if (e.target.id === "editor-dialog" || e.target.closest(".close")) e.currentTarget.close(); });
})();
