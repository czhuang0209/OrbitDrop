// /upgrade, tailored to what was tried in the GitHub version of OrbitDrop:
//   ?tool=tool.image.remove-background&name=Remove   (a crowned Premium tool on the wheel)
//   ?reason=actions&used=2&limit=2                   (adding a third of your own actions)
// Only text is ever set (never HTML), so nothing in the link can change the page.
(() => {
  const q = new URLSearchParams(location.search);
  const $ = (id) => document.getElementById(id);

  // What each Premium tool is called here, a line about it, and a real result to show (if there is one).
  const tools = [
    ["remove-background", "Remove background", "Cut out the subject of any photo as a transparent PNG, on your PC.", "cutout.png"],
    ["bg-white", "White background", "Your subject on clean white: product photos and profile pictures in one drag.", "white-bg.jpg"],
    ["bg-blur", "Blur background", "Portrait mode for any photo: the background melts away, the subject stays sharp.", "blur-bg.jpg"],
    ["sticker", "Make a sticker", "Anyone or anything, with a white outline, ready for WhatsApp.", "sticker.png"],
    ["upscale", "Upscale", "Make small or blurry pictures 2× or 4× bigger and sharper, on your PC.", "upscale-after.png"],
    ["blur.", "Blur faces and text", "Hide every face, number plate and bit of text before you share a photo.", "blur-after.jpg"],
    ["image.watermark", "Watermark", "Your name or website on one photo or a hundred, in a corner or all over.", null],
    ["beautify", "Beautify screenshots", "A plain screenshot becomes a beautiful one: soft gradient, rounded corners, a shadow.", "beautify-after.png"],
    ["social.square", "Square posts", "The whole photo as a perfect 1080 × 1080 post, nothing cropped.", "social-square.jpg"],
    ["social.story", "Stories", "Any photo as a 1080 × 1920 story, nothing cropped.", "social-story.jpg"],
    ["social.thumbnail", "YouTube thumbnails", "A 1280 × 720 thumbnail from any picture.", "social-thumbnail.jpg"],
    ["enhance", "Auto-enhance", "Dull, washed-out photos get their colour and contrast back in one drag.", "enhance-after.jpg"],
    ["palette", "Colour palettes", "The main colours of any picture, with their hex codes copied for you.", "palette.jpg"],
    ["image.icon", "Make icons", "A Windows icon (.ico) with every size from 16 to 256 pixels.", null],
    ["collage", "Collages", "Drag a few photos and get one neat collage.", "collage.jpg"],
    ["pdf.protect", "PDF passwords", "Lock a PDF with a password, or remove one you know.", null],
    ["pdf.unlock", "PDF passwords", "Lock a PDF with a password, or remove one you know.", null],
    ["page-numbers", "PDF page numbers", "Number every page, or stamp CONFIDENTIAL across a whole PDF.", null],
    ["pdf.watermark", "PDF watermarks", "Your name or a stamp across every page of a PDF.", null],
    ["rename", "Rename many files", "Rename a whole folder at once, with a preview before anything changes.", null],
    ["image-search", "Search the web", "Find where a picture appears online with Google, Bing or TinEye.", null],
  ];

  const store = (cid) => `https://apps.microsoft.com/detail/9NJQRMQLL2SZ?mode=direct&cid=${encodeURIComponent(cid)}`;
  const tool = q.get("tool") || "";
  const reason = q.get("reason") || "";

  if (reason === "actions") {
    // Good news, not a wall: they've clearly made OrbitDrop theirs, and there's more.
    const limit = Math.max(1, Math.min(99, parseInt(q.get("limit"), 10) || 2));
    $("up-eyebrow").textContent = `🎉 You've made the most of your ${limit} free actions`;
    $("up-title").textContent = "Unlock unlimited actions ✨";
    $("up-lead").textContent = "You're clearly making OrbitDrop yours. With the upgrade, add as many actions as you like, open your files in every app you use, and get remove background, upscale, stickers and 24 more Premium tools too. One payment, yours forever.";
    $("up-img").src = "/assets/upgrade/your-actions.gif";
    $("up-img").alt = "Your actions in OrbitDrop: two free, then unlimited with the upgrade";
    $("up-caption").textContent = "Two actions free, as many as you like with the upgrade.";
    $("up-shot").hidden = false;
    $("up-store").href = store("web_upgrade_actions");
    document.title = "Unlimited custom actions · OrbitDrop";
  } else if (tool) {
    const match = tools.find(([key]) => tool.includes(key));
    const name = match ? match[1] : (q.get("name") || "").slice(0, 60);
    if (name) {
      $("up-eyebrow").textContent = "OrbitDrop Premium";
      $("up-title").textContent = `Want to use ${name}?`;
      $("up-lead").textContent = `${match ? match[2] + " " : ""}The GitHub version of OrbitDrop is free and open source, and it stays that way. ${name} and the other Premium tools come with OrbitDrop from the Microsoft Store: one payment, no subscription.`;
      document.title = `${name} · OrbitDrop Premium`;
    }
    if (match && match[3]) {
      $("up-img").src = `/assets/upgrade/${match[3]}`;
      $("up-img").alt = `${match[1]}: a real result from OrbitDrop`;
      $("up-caption").textContent = "A real result, made by OrbitDrop on a PC.";
      $("up-shot").hidden = false;
    }
    const short = tool.replace(/^tool\.|^action\./, "").replace(/[^a-z0-9]+/gi, "_").slice(0, 40);
    $("up-store").href = store(`web_upgrade_${short}`);
  }
})();
