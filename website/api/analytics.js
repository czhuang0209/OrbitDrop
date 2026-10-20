// /analytics: a private dashboard for the site's visits, behind a password (ANALYTICS_PASSWORD).
// Signing in sets an HttpOnly cookie for 30 days. Data comes from what /api/track counted.
const crypto = require("node:crypto");
const { configured, pipeline, day } = require("./_store");

const PASSWORD = process.env.ANALYTICS_PASSWORD || "";
const token = () => crypto.createHmac("sha256", PASSWORD).update("orbitdrop-analytics").digest("hex");
const esc = (s) => String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const n = (v) => Number(v || 0).toLocaleString("en-US");

function signedIn(req) {
  const cookie = String(req.headers.cookie || "").split(/;\s*/).find((c) => c.startsWith("od_auth="));
  if (!cookie || !PASSWORD) return false;
  const a = Buffer.from(cookie.slice(8)), b = Buffer.from(token());
  return a.length === b.length && crypto.timingSafeEqual(a, b);
}

async function readBody(req) {
  if (req.body !== undefined) return typeof req.body === "string" ? req.body : new URLSearchParams(req.body).toString();
  return await new Promise((resolve) => { let s = ""; req.on("data", (c) => (s += c)); req.on("end", () => resolve(s)); });
}

module.exports = async (req, res) => {
  res.setHeader("Cache-Control", "no-store");
  res.setHeader("X-Robots-Tag", "noindex, nofollow");
  res.setHeader("Content-Type", "text/html; charset=utf-8");

  if (!PASSWORD) return res.status(503).send(page("Analytics", `<p class="note">Set <code>ANALYTICS_PASSWORD</code> in Vercel → Settings → Environment Variables, then redeploy.</p>`));

  if (req.method === "POST") {
    const form = new URLSearchParams(await readBody(req));
    const given = Buffer.from(String(form.get("password") || "")), want = Buffer.from(PASSWORD);
    if (given.length === want.length && crypto.timingSafeEqual(given, want)) {
      res.setHeader("Set-Cookie", `od_auth=${token()}; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=${30 * 86400}`);
      res.setHeader("Location", "/analytics");
      return res.status(303).end();
    }
    await new Promise((r) => setTimeout(r, 600)); // slow down guessing
    return res.status(401).send(login("That password isn't right."));
  }
  if (!signedIn(req)) return res.status(401).send(login());
  if (!configured()) return res.status(503).send(page("Analytics", `<p class="note">Connect an Upstash Redis store to this project (Vercel → Storage), then redeploy.</p>`));

  const days = [7, 30, 90].includes(Number(req.query?.days)) ? Number(req.query.days) : 30;
  try { return res.status(200).send(await dashboard(days)); }
  catch (e) { return res.status(500).send(page("Analytics", `<p class="note">Couldn't read the data (${esc(e.message)}).</p>`)); }
};

// ------------------------------------------------------------------ data

const LISTS = [
  ["refs", "Where visitors came from"], ["countries", "Countries"], ["devices", "Devices"],
  ["browsers", "Browsers"], ["pages", "Pages"], ["storefrom", "Store clicks by button"],
];

async function dashboard(range) {
  const dates = Array.from({ length: range }, (_, i) => day(range - 1 - i));
  const cmds = [];
  for (const d of dates) cmds.push(["GET", `od:views:${d}`], ["PFCOUNT", `od:visitors:${d}`], ["GET", `od:store:${d}`]);
  cmds.push(["PFCOUNT", ...dates.map((d) => `od:visitors:${d}`)]);
  for (const [name] of LISTS) for (const d of dates) cmds.push(["ZRANGE", `od:${name}:${d}`, "0", "49", "REV", "WITHSCORES"]);
  const out = await pipeline(cmds);

  const series = dates.map((d, i) => ({ d, views: +out[i * 3] || 0, visitors: +out[i * 3 + 1] || 0, store: +out[i * 3 + 2] || 0 }));
  const unique = +out[dates.length * 3] || 0;
  let at = dates.length * 3 + 1;
  const lists = LISTS.map(([, title]) => {
    const totals = new Map();
    for (let i = 0; i < dates.length; i++, at++) {
      const flat = out[at] || [];
      for (let j = 0; j < flat.length; j += 2) totals.set(flat[j], (totals.get(flat[j]) || 0) + Number(flat[j + 1]));
    }
    return { title, rows: [...totals].sort((a, b) => b[1] - a[1]).slice(0, 10) };
  });

  const views = series.reduce((s, x) => s + x.views, 0), store = series.reduce((s, x) => s + x.store, 0);
  const rate = unique ? `${((store / unique) * 100).toFixed(1)}%` : "–";
  const today = series[series.length - 1];

  const tabs = [7, 30, 90].map((r) => `<a href="/analytics?days=${r}"${r === range ? ' aria-current="page"' : ""}>${r} days</a>`).join("");
  const tiles = [
    ["Visitors", n(unique), `${n(today.visitors)} today`],
    ["Page views", n(views), `${n(today.views)} today`],
    ["Store clicks", n(store), `${n(today.store)} today`],
    ["Visitors who clicked Store", rate, "Store clicks ÷ visitors"],
  ].map(([label, value, sub]) => `<div class="tile"><span class="label">${label}</span><strong>${value}</strong><span class="sub">${sub}</span></div>`).join("");

  const tables = lists.map(({ title, rows }) => {
    const max = rows[0]?.[1] || 1;
    const body = rows.length ? rows.map(([k, v]) => `<li><span class="bar" style="--w:${(v / max) * 100}%"></span><span class="k">${esc(title === "Countries" ? flag(k) : k)}</span><span class="v">${n(v)}</span></li>`).join("") : `<li class="empty">No data yet</li>`;
    return `<section class="list"><h2>${title}</h2><ol>${body}</ol></section>`;
  }).join("");

  const daily = series.slice().reverse().map((x) => `<tr><td>${x.d}</td><td>${n(x.visitors)}</td><td>${n(x.views)}</td><td>${n(x.store)}</td></tr>`).join("");

  return page("OrbitDrop analytics", `
    <header class="top"><div><h1>Analytics</h1><p class="sub">orbitdrop.essam.biz · last ${range} days (UTC)</p></div><nav class="range">${tabs}</nav></header>
    <div class="tiles">${tiles}</div>
    <section class="chart-card"><h2>Visitors per day</h2>${chart(series)}</section>
    <div class="lists">${tables}</div>
    <details class="table"><summary>Daily numbers</summary><table><thead><tr><th>Day</th><th>Visitors</th><th>Page views</th><th>Store clicks</th></tr></thead><tbody>${daily}</tbody></table></details>
    <p class="note">No cookies on the public site and nothing personal is stored: a visitor is a hash that changes every day. Bots are skipped.</p>`, true);
}

const regions = new Intl.DisplayNames(["en"], { type: "region" });
const flag = (cc) => { if (!/^[A-Z]{2}$/.test(cc)) return "Unknown"; try { return regions.of(cc) || cc; } catch { return cc; } };

/** One series, so no legend: the title names it. Bars with 4px rounded tops, a 2px gap, a recessive baseline, hover tooltips. */
function chart(series) {
  const W = 960, H = 240, top = 16, bottom = 26, left = 36, right = 8;
  const max = Math.max(4, ...series.map((x) => x.visitors));
  const step = niceStep(max), yMax = Math.ceil(max / step) * step;
  const slot = (W - left - right) / series.length, bw = Math.max(2, slot - 2);
  const y = (v) => top + (H - top - bottom) * (1 - v / yMax);
  const grid = [];
  for (let v = 0; v <= yMax; v += step) grid.push(`<line x1="${left}" x2="${W - right}" y1="${y(v)}" y2="${y(v)}" class="${v ? "grid" : "base"}"/><text x="${left - 8}" y="${y(v) + 4}" class="tick" text-anchor="end">${v}</text>`);
  const bars = series.map((x, i) => {
    const h = y(0) - y(x.visitors), bx = left + i * slot + 1, r = Math.min(4, bw / 2, h);
    const path = h <= 0 ? "" : `M${bx} ${y(0)}V${y(x.visitors) + r}Q${bx} ${y(x.visitors)} ${bx + r} ${y(x.visitors)}H${bx + bw - r}Q${bx + bw} ${y(x.visitors)} ${bx + bw} ${y(x.visitors) + r}V${y(0)}Z`;
    return `<g class="col" data-d="${x.d}" data-v="${x.visitors}" data-p="${x.views}" data-s="${x.store}"><rect x="${left + i * slot}" y="${top}" width="${slot}" height="${H - top - bottom}" class="hit"/>${path ? `<path d="${path}" class="mark"/>` : ""}</g>`;
  }).join("");
  const every = Math.ceil(series.length / 8);
  const labels = series.map((x, i) => (i % every === 0 && series.length - 1 - i >= every / 2) || i === series.length - 1 ? `<text x="${left + i * slot + slot / 2}" y="${H - 6}" class="tick" text-anchor="middle">${x.d.slice(5)}</text>` : "").join("");
  return `<div class="chart"><svg viewBox="0 0 ${W} ${H}" role="img" aria-label="Visitors per day">${grid.join("")}${bars}${labels}</svg><div class="tip" hidden></div></div>`;
}
const niceStep = (max) => { const raw = max / 4, p = Math.pow(10, Math.floor(Math.log10(raw))); return [1, 2, 5, 10].map((m) => m * p).find((s) => s >= raw); };

// ------------------------------------------------------------------ pages

function login(error = "") {
  return page("Sign in · Analytics", `
    <form class="login" method="post" action="/analytics">
      <img src="/assets/icon-256.png" alt="" width="64" height="64">
      <h1>Analytics</h1>
      <p class="sub">Enter the password to see who visits the site.</p>
      <input type="password" name="password" placeholder="Password" autocomplete="current-password" autofocus required>
      <button type="submit">Sign in</button>
      ${error ? `<p class="error">${esc(error)}</p>` : ""}
    </form>`);
}

function page(title, body, withChart = false) {
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex, nofollow"><title>${esc(title)}</title><link rel="icon" href="/assets/favicon-64.png">
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
<style>
:root { --bg:#F5F5F7; --card:#FFFFFF; --ink:#1D1D1F; --ink-2:#6E6E73; --ink-3:#A1A1A6; --line:rgba(0,0,0,.08); --mark:#1D1D1F; --mark-hover:#4A4A50; --track:rgba(0,0,0,.05); color-scheme: light dark; }
@media (prefers-color-scheme: dark) { :root { --bg:#0B0B0D; --card:#17171A; --ink:#F5F5F7; --ink-2:#A1A1A6; --ink-3:#6E6E73; --line:rgba(255,255,255,.10); --mark:#E8E8ED; --mark-hover:#FFFFFF; --track:rgba(255,255,255,.06); } }
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--ink); font: 400 15px/1.5 Inter, "Segoe UI", system-ui, sans-serif; -webkit-font-smoothing: antialiased; }
main { width: min(1100px, 100% - 32px); margin: 0 auto; padding: 40px 0 60px; }
h1 { margin: 0; font-size: 32px; letter-spacing: -.03em; } h2 { margin: 0 0 14px; font-size: 15px; font-weight: 600; letter-spacing: -.01em; }
.sub { color: var(--ink-2); margin: 4px 0 0; } .note { color: var(--ink-3); font-size: 13px; margin-top: 28px; }
code { background: var(--card); padding: 2px 6px; border-radius: 6px; }
.top { display: flex; justify-content: space-between; align-items: end; gap: 16px; flex-wrap: wrap; margin-bottom: 24px; }
.range { display: flex; gap: 4px; background: var(--card); padding: 4px; border-radius: 999px; border: 1px solid var(--line); }
.range a { padding: 7px 14px; border-radius: 999px; color: var(--ink-2); text-decoration: none; font-weight: 500; font-size: 14px; }
.range a[aria-current] { background: var(--ink); color: var(--bg); }
.tiles { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
.tile { background: var(--card); border: 1px solid var(--line); border-radius: 18px; padding: 18px 20px; display: flex; flex-direction: column; gap: 2px; }
.tile .label { color: var(--ink-2); font-size: 13px; font-weight: 500; } .tile strong { font-size: 34px; letter-spacing: -.03em; line-height: 1.15; } .tile .sub { font-size: 13px; color: var(--ink-3); margin: 0; }
.chart-card, .list, .table { background: var(--card); border: 1px solid var(--line); border-radius: 18px; padding: 20px; margin-top: 12px; }
.chart { position: relative; } .chart svg { width: 100%; height: auto; display: block; }
.grid { stroke: var(--line); stroke-width: 1; } .base { stroke: var(--ink-3); stroke-width: 1; } .tick { fill: var(--ink-3); font-size: 11px; }
.mark { fill: var(--mark); } .hit { fill: transparent; } .col:hover .mark { fill: var(--mark-hover); } .col:hover .hit { fill: var(--track); }
.tip { position: absolute; pointer-events: none; background: var(--ink); color: var(--bg); padding: 8px 10px; border-radius: 10px; font-size: 12.5px; line-height: 1.45; white-space: nowrap; transform: translate(-50%, calc(-100% - 10px)); box-shadow: 0 8px 24px rgba(0,0,0,.2); }
.lists { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; } .lists .list { margin-top: 12px; }
.list ol { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.list li { position: relative; display: flex; justify-content: space-between; gap: 12px; padding: 6px 10px; border-radius: 8px; font-size: 14px; }
.list .bar { position: absolute; inset: 0 auto 0 0; width: var(--w); background: var(--track); border-radius: 8px; }
.list .k, .list .v { position: relative; } .list .k { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; } .list .v { color: var(--ink-2); font-variant-numeric: tabular-nums; }
.list .empty { color: var(--ink-3); }
.table summary { cursor: pointer; font-weight: 600; } .table table { width: 100%; border-collapse: collapse; margin-top: 12px; font-variant-numeric: tabular-nums; }
.table th, .table td { text-align: right; padding: 6px 8px; border-bottom: 1px solid var(--line); } .table th:first-child, .table td:first-child { text-align: left; }
.login { width: min(360px, 100%); margin: 12vh auto 0; background: var(--card); border: 1px solid var(--line); border-radius: 22px; padding: 32px; text-align: center; display: grid; gap: 12px; }
.login img { margin: 0 auto 4px; border-radius: 14px; } .login .sub { margin-bottom: 8px; }
.login input { font: inherit; padding: 12px 14px; border-radius: 12px; border: 1px solid var(--line); background: var(--bg); color: var(--ink); }
.login button { font: inherit; font-weight: 600; padding: 12px; border-radius: 12px; border: 0; background: var(--ink); color: var(--bg); cursor: pointer; }
.error { color: #D70015; margin: 0; font-size: 14px; }
@media (max-width: 860px) { .tiles { grid-template-columns: repeat(2, 1fr); } .lists { grid-template-columns: 1fr; } }
</style></head><body><main>${body}</main>
${withChart ? `<script>
(() => {
  const chart = document.querySelector(".chart"), tip = chart && chart.querySelector(".tip");
  if (!chart) return;
  chart.querySelectorAll(".col").forEach((g) => {
    g.addEventListener("pointerenter", () => {
      const r = g.getBoundingClientRect(), c = chart.getBoundingClientRect();
      tip.innerHTML = "<b>" + g.dataset.d + "</b><br>" + g.dataset.v + " visitors · " + g.dataset.p + " views · " + g.dataset.s + " Store clicks";
      tip.hidden = false;
      const half = tip.offsetWidth / 2, x = r.left - c.left + r.width / 2;
      tip.style.left = Math.min(Math.max(x, half), c.width - half) + "px"; tip.style.top = (g.querySelector(".mark")?.getBoundingClientRect().top ?? r.bottom) - c.top + "px";
      tip.hidden = false;
    });
    g.addEventListener("pointerleave", () => { tip.hidden = true; });
  });
})();
</script>` : ""}</body></html>`;
}
