# OrbitDrop website

A static site: plain HTML, CSS and a little JavaScript. No build step and no dependencies.

| File | What |
|---|---|
| `index.html` | The home page |
| `privacy.html` | The privacy policy (served at `/privacy`) |
| `styles.css`, `main.js` | Look and behaviour (light and dark follow the system; demos play only while on screen) |
| `assets/demo/*.mp4` | The guide's demos as small looping videos, light and dark (`OrbitDrop.exe --render-guide-frames`, then ffmpeg) |
| `assets/film.mp4` | The launch film, web size |
| `vercel.json` | Clean URLs and caching for Vercel |
| `dev-server.mjs` | Local preview (not deployed; see `.vercelignore`) |

## Preview locally

```powershell
node website/dev-server.mjs        # → http://localhost:4321
```

## Put it online with Vercel (free)

**Option A: from GitHub (recommended; every push redeploys).**
1. Sign in at <https://vercel.com> with your GitHub account.
2. **Add New → Project**, then pick the `i3sam/OrbitDrop` repository.
3. Set **Root Directory** to `website`. Leave the framework as **Other**, with no build command and no output directory.
4. Click **Deploy**. You'll get an address like `orbitdrop.vercel.app`.

**Option B: from your PC.**
```powershell
cd website
npx vercel        # first time: log in, accept the defaults
npx vercel --prod
```

## A custom domain (optional)

Buy a domain such as `orbitdrop.app` from any registrar. Then in Vercel go to **Project → Settings → Domains**, add it, and follow the DNS steps shown.

Once it's live:
- Add the address to the **Website** field of the Store listing in Partner Center.
- Add it to the GitHub repository's **About** box.
- Optionally use `https://<your-domain>/privacy` as the Store privacy URL.

## Rebuilding the demo videos

```powershell
dotnet build src/OrbitDrop.App -c Release
src\OrbitDrop.App\bin\Release\net10.0-windows10.0.19041.0\OrbitDrop.exe --render-guide-frames C:\temp\frames
# then for each folder in C:\temp\frames (hero, hero-dark, drag, ...):
ffmpeg -framerate 30 -i C:\temp\frames\hero\%04d.png -vf format=yuv420p -c:v libx264 -preset slow -crf 24 -movflags +faststart -an website\assets\demo\hero.mp4
```
