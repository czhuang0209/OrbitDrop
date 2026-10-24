// POST /api/waitlist: an email address from orbitdrop.essam.biz/setup, added to the developer's private Resend
// audience ("OrbitDrop waitlist") so they can write when new features and tips are out. Nothing is stored on this site.
//
// Settings (Vercel → Project → Settings → Environment Variables; never in this repository):
//   RESEND_API_KEY      the Resend API key (already set for the feedback form)
//   RESEND_AUDIENCE_ID  the waitlist audience's id (Resend → Audiences)
//
// Defences, as for the feedback form: same-site Origin only, JSON only, a hidden honeypot field, a minimum time on the
// page, per-address and overall rate limits, strict email checks. Every outcome a visitor sees is the same whether the
// address was new or already on the list, so the form can't be used to find out who signed up.
import { clean, validEmail } from "./feedback.js";

const ALLOWED_ORIGINS = new Set(["https://orbitdrop.essam.biz", "http://localhost:4321", "http://localhost:8765"]);
const PER_ADDRESS = { max: 5, windowMs: 10 * 60_000 };
const OVERALL = { max: 200, windowMs: 60 * 60_000 };
const MIN_FILL_MS = 1500;

const recent = new Map();
let overall = [];

function json(res, status, body) {
  res.statusCode = status;
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  res.setHeader("Cache-Control", "no-store");
  res.end(JSON.stringify(body));
}

function limited(key, now) {
  const hits = (recent.get(key) ?? []).filter((t) => now - t < PER_ADDRESS.windowMs);
  overall = overall.filter((t) => now - t < OVERALL.windowMs);
  if (hits.length >= PER_ADDRESS.max || overall.length >= OVERALL.max) { recent.set(key, hits); return true; }
  hits.push(now); overall.push(now); recent.set(key, hits);
  if (recent.size > 5000) recent.clear();
  return false;
}

/** The address to add, or an error for the visitor. */
export function check(body) {
  if (!body || typeof body !== "object") return { error: "Something's missing." };
  const email = clean(body.email, 200).toLowerCase();
  if (!validEmail(email)) return { error: "That email address doesn't look right." };
  return { email };
}

export default async function handler(req, res) {
  if (req.method !== "POST") { res.setHeader("Allow", "POST"); return json(res, 405, { ok: false, error: "Use the form." }); }
  if (!ALLOWED_ORIGINS.has(req.headers.origin ?? "")) return json(res, 403, { ok: false, error: "Use the form on orbitdrop.essam.biz." });
  if (!String(req.headers["content-type"] ?? "").startsWith("application/json")) return json(res, 415, { ok: false, error: "Use the form." });

  let body = req.body;
  if (typeof body === "string") { if (body.length > 2_000) return json(res, 413, { ok: false, error: "That's too long." }); try { body = JSON.parse(body); } catch { body = null; } }
  if (JSON.stringify(body ?? {}).length > 2_000) return json(res, 413, { ok: false, error: "That's too long." });

  if (body?.website || !(Number(body?.elapsed) >= MIN_FILL_MS)) return json(res, 200, { ok: true }); // bots learn nothing

  const address = String(req.headers["x-real-ip"] ?? req.headers["x-forwarded-for"] ?? "").split(",")[0].trim() || "unknown";
  if (limited(address, Date.now())) return json(res, 429, { ok: false, error: "Please try again in a few minutes." });

  const checked = check(body);
  if (checked.error) return json(res, 400, { ok: false, error: checked.error });

  const key = process.env.RESEND_API_KEY, audience = process.env.RESEND_AUDIENCE_ID;
  if (!key || !audience) return json(res, 503, { ok: false, error: "Sign-ups aren't open yet." });

  try {
    const r = await fetch(`https://api.resend.com/audiences/${encodeURIComponent(audience)}/contacts`, {
      method: "POST",
      headers: { Authorization: `Bearer ${key}`, "Content-Type": "application/json" },
      body: JSON.stringify({ email: checked.email, unsubscribed: false }),
      signal: AbortSignal.timeout(10_000),
    });
    // Already on the list counts as success: the visitor sees the same thing either way.
    if (!r.ok && r.status !== 409 && r.status !== 422) { console.error(`Resend refused the contact: HTTP ${r.status}`); return json(res, 502, { ok: false, error: "That didn't go through. Please try again." }); }
    return json(res, 200, { ok: true });
  } catch {
    console.error("Resend could not be reached");
    return json(res, 502, { ok: false, error: "That didn't go through. Please try again." });
  }
}
