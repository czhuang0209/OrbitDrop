// Demo videos: the light or dark version to match the system theme, played only while on screen.
// Sections fade in as they scroll into view. Everything still works without JavaScript except the demos.
(() => {
  const dark = window.matchMedia("(prefers-color-scheme: dark)");
  const reduce = window.matchMedia("(prefers-reduced-motion: reduce)");
  const demos = [...document.querySelectorAll("video.demo")];

  const pick = () => {
    for (const v of demos) {
      const src = dark.matches ? v.dataset.dark : v.dataset.light;
      if (v.getAttribute("src") !== src) {
        const playing = !v.paused;
        v.src = src;
        if (playing) v.play().catch(() => {});
      }
    }
  };
  pick();
  dark.addEventListener("change", pick);

  if ("IntersectionObserver" in window) {
    const play = new IntersectionObserver((entries) => {
      for (const e of entries) {
        const v = e.target;
        if (e.isIntersecting && !reduce.matches) { v.preload = "auto"; v.play().catch(() => {}); }
        else v.pause();
      }
    }, { threshold: 0.35 });
    demos.forEach((v) => play.observe(v));

    const show = new IntersectionObserver((entries) => {
      for (const e of entries) if (e.isIntersecting) { e.target.classList.add("in"); show.unobserve(e.target); }
    }, { threshold: 0.12, rootMargin: "0px 0px -40px 0px" });
    document.querySelectorAll(".reveal").forEach((el) => show.observe(el));
  } else {
    document.querySelectorAll(".reveal").forEach((el) => el.classList.add("in"));
    demos.forEach((v) => v.play().catch(() => {}));
  }

  // With reduced motion, show the demos paused on a frame that explains the step; a click plays them.
  if (reduce.matches) demos.forEach((v) => { v.removeAttribute("autoplay"); v.controls = true; });
})();
