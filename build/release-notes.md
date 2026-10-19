## What's new in 0.2.0-alpha

The biggest update yet: OrbitDrop now does a lot more than convert.

**A smarter wheel**
- Related options are grouped. Segments with three dots (like **Compress**) open their choices when you rest on them for a moment; rest on the centre to go back. Dropping straight on the segment runs the most common choice.

**Photos**
- **Edit**: crop (free or fixed shapes), adjust brightness, contrast, saturation and warmth, mark up with pen, arrows, boxes and text, and redact with black boxes or pixelation. Saves a copy; your original is untouched.
- **Compress** to 50 / 75 / 25 % of the size, or under 1 MB / 500 KB / 200 KB.
- **Resize** to 50 %, 1920, 1280 or 800 px.
- **Rotate & flip** (now with vertical flip and 180°), **Copy text** from an image (offline text recognition), **Set as wallpaper**.
- **PDF**: turn images into a PDF, or combine several into one; turn PDF pages into PNG or JPG.
- Reads **WebP, HEIC, AVIF, JPEG XL and camera RAW** if Windows has the codec. Phone photos now always come out upright.

**Metadata**
- **Info** for any file: camera, exposure, location, size, length and more. Edit the title, tags, rating, artist, album and so on, into a copy.
- **Remove all metadata** (photos, PNG, MP3, FLAC) or just the **location** from photos, without re-compressing.

**Video and audio**
- Compress video to **720p, 1080p, 480p or half size**; take a **snapshot**; **join** clips into one video.
- Compress audio (128 / 96 / 64 kbps), switch to **mono or stereo**, convert to **FLAC**.

**Actions** (work on folders too)
- **Copy to / Move to** a folder you choose or a recent one (never overwrites), **Share** (Nearby Share and apps), **Zip**, **Terminal**, plus copy path/name and show in folder.
- **Extract** ZIP, TAR, TAR.GZ and GZ archives.

> **Updating:** if you have 0.1.2 or later, OrbitDrop will offer this update itself (or choose **Check for updates** in the tray menu). From 0.1.0 or 0.1.1, download **OrbitDrop-Setup.exe** below once.

## Download

| File | What it is |
|---|---|
| **OrbitDrop-Setup.exe** | **Recommended.** Installer: no admin rights, adds a Start Menu shortcut, optional start at sign-in, clean uninstall, in-app updates |
| **OrbitDrop-Portable.zip** | No install: unzip and run `OrbitDrop.exe` (the app tells you about new versions; you download them yourself) |
| SHA256SUMS.txt | Checksums for both |

Nothing else to install: .NET is included. Windows 10 (version 2004 or later) or Windows 11, 64-bit.

> **Windows SmartScreen:** this alpha isn't code-signed yet, so Windows may show "Windows protected your PC". Click **More info → Run anyway**. Updates installed from inside OrbitDrop don't show this.

## How to use

Start dragging any file in File Explorer or on the desktop, **tap Shift**, and release on an option in the wheel. The result appears next to the original, which is never changed. Tap Shift again for Tools and Actions; press Esc to cancel.

This is an early alpha, tested on Windows 11 with a single monitor at 100% scale. Please report problems in [Issues](../../issues).
