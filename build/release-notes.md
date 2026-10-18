## What's new in 0.1.1-alpha

- **New look:** a new OrbitDrop icon (a droplet in orbit) and a clean black-and-white design across the wheel, Settings, tray menu, notifications and welcome guide, in both light and dark themes.
- **Smarter wheel:** if a file can't be converted, the wheel opens straight into Actions instead of showing an empty ring, and Shift skips empty modes.
- **Video and audio** conversion via Windows Media Foundation (added since the first alpha).

## Download

| File | What it is |
|---|---|
| **OrbitDrop-Setup.exe** | **Recommended.** Installer: no admin rights, adds a Start Menu shortcut, optional start at sign-in, clean uninstall |
| **OrbitDrop-Portable.zip** | No install: unzip and run `OrbitDrop.exe` |
| SHA256SUMS.txt | Checksums for both |

Nothing else to install: .NET is included. Windows 10 (version 2004 or later) or Windows 11, 64-bit.

> **Windows SmartScreen:** this alpha isn't code-signed yet, so Windows may show "Windows protected your PC". Click **More info → Run anyway**. Signing is planned before 1.0.

## How to use

Start dragging any file in File Explorer or on the desktop, **tap Shift**, and release on an option in the wheel. The result appears next to the original, which is never changed. Tap Shift again for Tools and Actions; press Esc to cancel.

## What's in this alpha

- **Images:** PNG, JPG, BMP, TIFF, GIF conversion; rotate, flip, grayscale
- **Video and audio** (via Windows Media Foundation): MP4, WMV; extract audio as MP3, M4A, WAV, WMA; compress to 720p; remove audio
- **Actions:** copy path, copy name, show in folder (any file)
- Light, dark and system themes, tray menu, settings, first-run guide

This is an early alpha: so far it's been tested on Windows 11 with a single monitor at 100% scale. Please report problems in [Issues](../../issues).
