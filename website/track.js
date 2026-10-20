// Counts this visit and clicks on the Microsoft Store buttons (see /api/track: no cookies, nothing personal kept).
// Skipped for local previews and for browsers that ask not to be tracked.
(() => {
  if (location.hostname === "localhost" || location.hostname === "127.0.0.1" || navigator.doNotTrack === "1") return;
  const send = (data) => {
    const body = JSON.stringify(data);
    if (!(navigator.sendBeacon && navigator.sendBeacon("/api/track", body)))
      fetch("/api/track", { method: "POST", body, keepalive: true }).catch(() => {});
  };
  send({ p: location.pathname, r: document.referrer, q: location.search, w: innerWidth });
  document.addEventListener("click", (e) => {
    const a = e.target.closest("a[href*='apps.microsoft.com'], a[href^='ms-windows-store:']");
    if (!a) return;
    send({ e: "store", cid: a.dataset.store || new URL(a.href).searchParams.get("cid") || "link" });
  });
})();
