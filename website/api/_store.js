// Tiny Upstash Redis client over its REST API (no dependencies). Vercel's Upstash integration sets either
// KV_REST_API_URL/KV_REST_API_TOKEN or UPSTASH_REDIS_REST_URL/UPSTASH_REDIS_REST_TOKEN.
const URL_ = process.env.KV_REST_API_URL || process.env.UPSTASH_REDIS_REST_URL;
const TOKEN = process.env.KV_REST_API_TOKEN || process.env.UPSTASH_REDIS_REST_TOKEN;

const configured = () => Boolean(URL_ && TOKEN);

/** Runs several Redis commands in one round trip; returns their results in order. */
async function pipeline(commands) {
  const res = await fetch(`${URL_}/pipeline`, {
    method: "POST",
    headers: { Authorization: `Bearer ${TOKEN}`, "Content-Type": "application/json" },
    body: JSON.stringify(commands),
  });
  if (!res.ok) throw new Error(`Redis ${res.status}`);
  return (await res.json()).map((r) => r.result);
}

/** "2026-10-20" in UTC, for today and the days before it. */
const day = (offset = 0) => new Date(Date.now() - offset * 86400000).toISOString().slice(0, 10);

module.exports = { configured, pipeline, day };
