# Third-party notices

OrbitDrop is MIT-licensed (see `LICENSE`). This file lists everything a **release build** redistributes that isn't OrbitDrop's own code. It's updated whenever a dependency is added; each addition needs a licence review first (see `docs/research/00-initial-assessment.md` §6.2).

## Redistributed in release builds (v0.1.0-alpha)

| Component | Version | Licence | Why |
|---|---|---|---|
| .NET Runtime and Windows Desktop (WPF) runtime | 10.0.x | MIT, © .NET Foundation and Contributors | The self-contained build bundles the runtime so users don't need to install .NET. Source: https://github.com/dotnet/runtime, https://github.com/dotnet/wpf. Its own third-party notices: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT |

## Used from Windows, not redistributed

- **Windows Imaging Component (WIC):** image decoding and encoding.
- **Windows Media Foundation:** video and audio transcoding (H.264/AAC, WMV/WMA, MP3, WAV). Codecs come from Windows; HEVC, VP9 and similar depend on the Microsoft Store video extensions the user has installed.
- **Segoe UI Variable, Segoe Fluent Icons and Segoe MDL2 Assets fonts:** referenced by name and installed with Windows. OrbitDrop doesn't ship these fonts.
- **OLE drag-and-drop, Shell and DWM APIs:** documented Windows APIs.

## Development only (not shipped)

| Component | Licence |
|---|---|
| xUnit, xunit.runner.visualstudio | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| Inno Setup (installer compiler) | Inno Setup License (modified BSD-style) |

## Brand assets

The OrbitDrop icon is original work generated from vectors by `tools/IconGen` and covered by this repository's MIT licence. No assets from Tangerine or any other product are used.
