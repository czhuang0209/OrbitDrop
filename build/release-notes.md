## What's new in 0.1.2-alpha

- **In-app updates:** OrbitDrop now tells you when a new version is out. Click **Install** and it downloads the new installer, checks it against the published SHA-256 checksum, updates in place and reopens, keeping your settings. You can also use **Check for updates** in the tray menu or Settings → About.
- **You stay in control:** the check runs at most once a day and makes one anonymous request to GitHub Releases. Nothing about you or your files is sent, and nothing installs until you click. Turn it off in Settings → About.

> **Updating from 0.1.0 or 0.1.1:** those versions can't update themselves yet, so download and run **OrbitDrop-Setup.exe** below one last time. It closes the running copy, upgrades it in place and keeps your settings. Later updates will appear inside the app.

## Download

| File | What it is |
|---|---|
| **OrbitDrop-Setup.exe** | **Recommended.** Installer: no admin rights, adds a Start Menu shortcut, optional start at sign-in, clean uninstall, in-app updates |
| **OrbitDrop-Portable.zip** | No install: unzip and run `OrbitDrop.exe` (the app tells you about new versions; you download them yourself) |
| SHA256SUMS.txt | Checksums for both |

Nothing else to install: .NET is included. Windows 10 (version 2004 or later) or Windows 11, 64-bit.

> **Windows SmartScreen:** this alpha isn't code-signed yet, so Windows may show "Windows protected your PC". Click **More info → Run anyway**. Signing is planned before 1.0.

## How to use

Start dragging any file in File Explorer or on the desktop, **tap Shift**, and release on an option in the wheel. The result appears next to the original, which is never changed. Tap Shift again for Tools and Actions; press Esc to cancel.

## What's in this alpha

- **Images:** PNG, JPG, BMP, TIFF, GIF conversion; rotate, flip, grayscale
- **Video and audio** (via Windows Media Foundation): MP4, WMV; extract audio as MP3, M4A, WAV, WMA; compress to 720p; remove audio
- **Actions:** copy path, copy name, show in folder (any file)
- Light, dark and system themes, tray menu, settings, first-run guide, in-app updates

This is an early alpha: so far it's been tested on Windows 11 with a single monitor at 100% scale. Please report problems in [Issues](../../issues).
