// Tests for api/waitlist.js: node --test website/tests
import { test } from "node:test";
import assert from "node:assert/strict";
import handler, { check } from "../api/waitlist.js";

function call(body, { origin = "https://orbitdrop.essam.biz", method = "POST", type = "application/json", ip = "198.51.100.1" } = {}) {
  return new Promise((resolve) => {
    const res = { statusCode: 0, headers: {}, setHeader(k, v) { this.headers[k] = v; }, end(s) { resolve({ status: this.statusCode, body: JSON.parse(s) }); } };
    handler({ method, headers: { origin, "content-type": type, "x-real-ip": ip }, body }, res);
  });
}
const good = { email: "Ana@Example.com", elapsed: 5000, website: "" };

test("addresses are checked and lower-cased", () => {
  assert.equal(check({ email: " Ana@Example.com " }).email, "ana@example.com");
  assert.ok(check({ email: "not an email" }).error);
  assert.ok(check({ email: "a@b" }).error);
  assert.ok(check(null).error);
});

test("rejects other sites, other methods and non-JSON", async () => {
  assert.equal((await call(good, { origin: "https://evil.example" })).status, 403);
  assert.equal((await call(good, { method: "GET" })).status, 405);
  assert.equal((await call(good, { type: "text/plain", ip: "198.51.100.2" })).status, 415);
});

test("bots get a fake success and nothing is added", async () => {
  let calls = 0; globalThis.fetch = async () => { calls++; return { ok: true, status: 200 }; };
  process.env.RESEND_API_KEY = "test"; process.env.RESEND_AUDIENCE_ID = "aud";
  assert.deepEqual((await call({ ...good, website: "spam" }, { ip: "198.51.100.3" })).body, { ok: true });
  assert.deepEqual((await call({ ...good, elapsed: 100 }, { ip: "198.51.100.3" })).body, { ok: true });
  assert.equal(calls, 0);
});

test("adds the contact to the audience, and an address already there looks the same", async () => {
  const seen = [];
  process.env.RESEND_API_KEY = "test"; process.env.RESEND_AUDIENCE_ID = "aud-1";
  globalThis.fetch = async (url, init) => { seen.push({ url, body: JSON.parse(init.body) }); return { ok: seen.length === 1, status: seen.length === 1 ? 201 : 409 }; };
  const first = await call(good, { ip: "198.51.100.4" });
  const again = await call(good, { ip: "198.51.100.5" });
  assert.deepEqual(first.body, { ok: true });
  assert.deepEqual(again.body, { ok: true });
  assert.equal(seen[0].url, "https://api.resend.com/audiences/aud-1/contacts");
  assert.equal(seen[0].body.email, "ana@example.com");
});

test("limits sign-ups from one address", async () => {
  globalThis.fetch = async () => ({ ok: true, status: 201 });
  process.env.RESEND_API_KEY = "test"; process.env.RESEND_AUDIENCE_ID = "aud";
  let last;
  for (let i = 0; i < 7; i++) last = await call(good, { ip: "198.51.100.9" });
  assert.equal(last.status, 429);
});

test("says so when it isn't set up", async () => {
  delete process.env.RESEND_AUDIENCE_ID;
  assert.equal((await call(good, { ip: "198.51.100.10" })).status, 503);
});
