// Counts a page view or a Store-button click. No cookies and nothing personal is stored: a visitor is a hash of
// (daily secret, IP, browser) that changes every day, kept only inside a HyperLogLog for the unique count.
const crypto = require("node:crypto");
const { configured, pipeline, day } = require("./_store");

const KEEP_DAYS = 400;
const clean = (s, max = 80) => String(s || "").replace(/[^\w.\-/:]/g, "").slice(0, max);

function browserOf(ua) {
  if (/Edg\//.test(ua)) return "Edge";
  if (/OPR\//.test(ua)) return "Opera";
  if (/Firefox\//.test(ua)) return "Firefox";
  if (/Chrome\//.test(ua)) return "Chrome";
  if (/Safari\//.test(ua)) return "Safari";
  return "Other";
}
function osOf(ua) {
  if (/Windows/.test(ua)) return "Windows";
  if (/Android/.test(ua)) return "Android";
  if (/iPhone|iPad/.test(ua)) return "iOS";
  if (/Mac OS X/.test(ua)) return "macOS";
  if (/Linux/.test(ua)) return "Linux";
  return "Other";
}
const BOTS = /bot|crawl|spider|slurp|preview|facebookexternalhit|embedly|headless|lighthouse|monitor/i;

module.exports = async (req, res) => {
  res.setHeader("Cache-Control", "no-store");
  if (req.method !== "POST") return res.status(405).end();
  if (!configured()) return res.status(204).end();

  const ua = String(req.headers["user-agent"] || "");
  if (BOTS.test(ua)) return res.status(204).end();
  let body = req.body;
  if (typeof body === "string") { try { body = JSON.parse(body); } catch { body = {}; } }
  body = body || {};

  const d = day();
  const ttl = ["EXPIRE", "", String(KEEP_DAYS * 86400)];
  const k = (name) => `od:${name}:${d}`;
  const commands = [];
  const add = (cmd, key) => { commands.push([...cmd.slice(0, 1), key, ...cmd.slice(1)]); commands.push([ttl[0], key, ttl[2]]); };

  if (body.e === "store") {
    add(["INCR"], k("store"));
    add(["ZINCRBY", "1", clean(body.cid, 40) || "unknown"], k("storefrom"));
  } else {
    const ip = String(req.headers["x-forwarded-for"] || "").split(",")[0].trim();
    const secret = process.env.ANALYTICS_SALT || process.env.ANALYTICS_PASSWORD || "orbitdrop";
    const visitor = crypto.createHash("sha256").update(`${secret}|${d}|${ip}|${ua}`).digest("hex").slice(0, 16);
    let ref = "Direct";
    try { if (body.r) { const h = new URL(body.r).hostname.replace(/^www\./, ""); if (h && !h.endsWith("essam.biz")) ref = h; } } catch { /* ignore */ }
    const params = new URLSearchParams(String(body.q || ""));
    const source = clean(params.get("utm_source") || params.get("ref") || params.get("cid") || "", 40);
    if (source) ref = source;

    add(["INCR"], k("views"));
    add(["PFADD", visitor], k("visitors"));
    add(["ZINCRBY", "1", clean(body.p, 60) || "/"], k("pages"));
    add(["ZINCRBY", "1", ref], k("refs"));
    add(["ZINCRBY", "1", clean(req.headers["x-vercel-ip-country"], 2) || "??"], k("countries"));
    add(["ZINCRBY", "1", Number(body.w) < 760 ? "Phone" : Number(body.w) < 1100 ? "Tablet" : "Desktop"], k("devices"));
    add(["ZINCRBY", "1", `${browserOf(ua)} · ${osOf(ua)}`], k("browsers"));
  }

  try { await pipeline(commands); } catch { /* analytics must never break the site */ }
  return res.status(204).end();
};
