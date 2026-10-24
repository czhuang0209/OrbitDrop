// The setup page: ticked tips are remembered in this browser only, and the waitlist form posts to /api/waitlist.
(() => {
  "use strict";
  const KEY = "orbitdrop.setup.done";
  let done = [];
  try { done = JSON.parse(localStorage.getItem(KEY) || "[]"); } catch { done = []; }
  document.querySelectorAll("[data-tip]").forEach((box) => {
    box.checked = done.includes(box.dataset.tip);
    box.addEventListener("change", () => {
      done = done.filter((t) => t !== box.dataset.tip);
      if (box.checked) done.push(box.dataset.tip);
      try { localStorage.setItem(KEY, JSON.stringify(done)); } catch { /* private mode: ticks just aren't kept */ }
    });
  });

  const form = document.getElementById("wl-form");
  if (!form) return;
  const status = document.querySelector(".wl-status");
  const button = form.querySelector('button[type="submit"]');
  const opened = Date.now();
  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const email = form.elements.email;
    if (!email.value.trim() || !email.checkValidity()) { status.textContent = "That email address doesn't look right."; email.focus(); return; }
    button.disabled = true;
    status.textContent = "Joining…";
    try {
      const r = await fetch("/api/waitlist", {
        method: "POST", headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: email.value, website: form.elements.website.value, elapsed: Date.now() - opened }),
      });
      const result = await r.json().catch(() => ({}));
      if (r.ok && result.ok) { form.hidden = true; status.textContent = "You're in. Watch your inbox for the next part."; }
      else status.textContent = result.error || "That didn't go through. Please try again.";
    } catch {
      status.textContent = "That didn't go through. Check your connection and try again.";
    } finally {
      button.disabled = false;
    }
  });
})();
