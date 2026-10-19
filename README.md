<p align="center"><img src="src/OrbitDrop.UI/Assets/orbitdrop-256.png" width="96" alt="OrbitDrop icon"></p>

<h1 align="center">OrbitDrop</h1>
<p align="center"><b>Your files. Your flow.</b><br>Convert, edit and manage files without opening an app. For Windows 10 (19041+) and 11.</p>

## Download

**[⬇ Download OrbitDrop for Windows (installer)](https://github.com/i3sam/OrbitDrop/releases/download/v0.2.1-alpha/OrbitDrop-Setup.exe)** · [portable .zip](https://github.com/i3sam/OrbitDrop/releases/download/v0.2.1-alpha/OrbitDrop-Portable.zip) · [all releases](https://github.com/i3sam/OrbitDrop/releases)

Run the installer: no admin rights or .NET needed. This alpha isn't code-signed yet, so if Windows shows "Windows protected your PC", click **More info → Run anyway**.

> **Status: 0.2.1-alpha.** The core gesture is validated on Windows 11 (single monitor, 100% scale). Multi-monitor, mixed-DPI and Windows 10 sign-off are still pending; see [the feasibility report](docs/research/02-feasibility-report.md).

## How it works

1. Start dragging one or more files in File Explorer or on the desktop.
2. **Tap Shift** while you're still holding the mouse button. A radial wheel opens around the pointer.
3. Release on an option. The result appears **next to the original**, which is never changed.

Tap Shift again to switch **Convert → Tools → Actions**. Release on the centre or press **Esc** to cancel.

| Mode | 0.2 capabilities |
|---|---|
| Convert | **Images:** PNG, JPG, BMP, TIFF, GIF, PDF (reads WebP, HEIC, AVIF, JPEG XL and camera RAW when Windows has the codec) · **PDF:** pages to PNG or JPG · **Video:** MP4, WMV, animated GIF, or extract audio as MP3 / M4A / WAV / WMA / FLAC · **Audio:** MP3, M4A, WAV, WMA, FLAC |
| Tools | **Images:** edit (crop, adjust, markup, redact), compress (by % or under 1 MB / 500 KB / 200 KB), resize, rotate & flip, grayscale, copy text (OCR), combine into PDF · **Video:** compress (720p / 1080p / 480p / half size), remove audio, snapshot, join · **Audio:** compress, normalize, mono / stereo · **Archives:** extract ZIP, TAR, TAR.GZ, GZ · **Any file:** info, edit details, remove metadata or location |
| Actions | Copy path or name, show in folder, copy to / move to (recent and common folders), share (Nearby Share), zip, terminal, set as wallpaper. Works on folders too |

Related options are grouped: rest on a segment with dots (for example **Compress**) to open its choices, rest on the centre to go back, or drop straight on it for the most common choice.

**No drag needed:** select files in File Explorer or on the desktop and press **Ctrl + Alt + Space**, then click an option.

**Private by design:** everything runs locally. No uploads, no account, no telemetry. The only network request is an optional once-a-day update check against GitHub Releases (Settings → About; turn it off any time).

## Build from source

Requirements: Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download). For the installer, add [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
dotnet build OrbitDrop.slnx -c Release
dotnet test tests/OrbitDrop.Tests
dotnet run --project src/OrbitDrop.App          # runs from source (lives in the notification area)
./build/package.ps1                             # → artifacts/: setup.exe, portable.zip, SHA256SUMS.txt
```

More detail: [docs/development/building.md](docs/development/building.md).

## Repository map

| Path | What |
|---|---|
| `src/OrbitDrop.App` | Composition root, tray lifecycle, interaction controller |
| `src/OrbitDrop.UI` | Design system, radial wheel, toasts, Settings, onboarding |
| `src/OrbitDrop.Windows` | Win32/COM: drag detection, OLE drop target, tray, theme, shell |
| `src/OrbitDrop.Core` | Operation contracts, registry, collision-safe atomic output |
| `src/OrbitDrop.Conversion` / `Tools` | Operation providers (add capabilities here) |
| `src/OrbitDrop.Jobs` / `Infrastructure` | Background jobs, settings, local logs |
| `experiments/OrbitProbe` | Feasibility probe, kept as a regression and diagnostics tool |
| `docs/` | Research, architecture, design system, milestones |

## Documentation

- [Initial assessment](docs/research/00-initial-assessment.md) · [Probe test sheet](docs/research/01-orbit-probe-test-sheet.md) · [Feasibility report](docs/research/02-feasibility-report.md)
- [Architecture & milestones](docs/architecture/03-architecture-and-milestones.md) · [Design system](docs/design/design-system.md) · [Milestone 1 summary](docs/milestones/M1-foundation.md)

## License

MIT. See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). OrbitDrop is inspired by the interaction of Tangerine for Mac; it shares no code, artwork or branding with it.
