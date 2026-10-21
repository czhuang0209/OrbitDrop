# Third-party notices

OrbitDrop is MIT-licensed (see `LICENSE`). This file lists everything a **release build** redistributes that isn't OrbitDrop's own code. It's updated whenever a dependency is added; each addition needs a licence review first (see `docs/research/00-initial-assessment.md` §6.2).

## Redistributed in release builds

| Component | Version | Licence | Why |
|---|---|---|---|
| .NET Runtime and Windows Desktop (WPF) runtime | 10.0.x | MIT, © .NET Foundation and Contributors | The self-contained build bundles the runtime so users don't need to install .NET. Source: https://github.com/dotnet/runtime, https://github.com/dotnet/wpf. Its own third-party notices: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT |
| PDFsharp | 6.2.4 | MIT, © empira Software GmbH | PDF tools: merge, split, rotate, compress. Pinned to 6.2.4, the last MIT release (6.5 and later are AGPL-3.0). Source: https://github.com/empira/PDFsharp |
| Microsoft.Extensions.Logging.Abstractions, System.Security.Cryptography.Pkcs | 8.0.x | MIT, © .NET Foundation and Contributors | Dependencies of PDFsharp |
| SkiaSharp, SkiaSharp.NativeAssets.Win32 | 3.119.2 | MIT, © Microsoft Corporation | WebP output, reading WebP without Microsoft's WebP extension, and drawing SVGs. The native libSkiaSharp.dll contains Skia (BSD-3-Clause, © Google), libwebp (BSD-3-Clause, © Google) and the other libraries listed in https://github.com/mono/SkiaSharp/blob/main/THIRD-PARTY-NOTICES.txt. Source: https://github.com/mono/SkiaSharp |
| Svg.Skia, Svg.Model, Svg.SceneGraph, Svg.Animation, ShimSkiaSharp | 4.9.1 | MIT, © Wiesław Šoltés | Reading SVG drawings (SVG to PNG, JPG, WebP …). Source: https://github.com/wieslawsoltes/Svg.Skia |
| Svg.Custom | 4.9.1 | MS-PL (Microsoft Public License), © SVG.NET contributors | The SVG parser Svg.Skia builds on (a build of SVG.NET). Source: https://github.com/svg-net/SVG |
| ExCSS | 4.3.1 | MIT, © Tyler Brinks | CSS inside SVG files |
| HarfBuzzSharp, HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | MIT, © Microsoft Corporation (HarfBuzz: MIT, © HarfBuzz contributors) | Text inside SVG files |

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
