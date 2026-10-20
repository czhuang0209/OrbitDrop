## What's new in 0.3.0-alpha

- **PDF tools.** Drag PDFs, tap Shift twice for Tools:
  - **Merge** several PDFs into one, in the order you picked.
  - **Split** a PDF into a folder with one PDF per page.
  - **Compress**: the photos inside get smaller; text stays sharp, selectable text.
  - **Rotate pages** right, left or 180°.
- **Ctrl + Alt + Space in VS Code.** Select files in VS Code's Explorer (or just have a file open) and press the shortcut. OrbitDrop uses VS Code's own "Copy Path" to find them, then puts your clipboard back exactly as it was. Also works in Cursor, Windsurf and VSCodium.
- After you've used OrbitDrop a few times, it asks once if you'd like to rate it. It never asks again.

## In 0.2.10-alpha

- **Fixed: dragging from VS Code.** In 0.2.9 the wheel opened but said "No files". It now reads the real file and folder paths VS Code sends, so drag from its Explorer sidebar, tap Shift, and let go on what you want. Results land next to your original, not in a temporary folder.

## In 0.2.9-alpha

- **Works with VS Code:** drag a file or folder from VS Code's Explorer sidebar, tap Shift, and the wheel opens, just like from File Explorer. The same goes for other apps that drag files their own way (many are built like VS Code).

## In 0.2.8-alpha

- **Trimming works on more PCs:** if your graphics hardware can't encode the video, OrbitDrop now finishes the trim in software instead of failing.
- **The guide's demos are easier to read:** the pointer now rests beside each option instead of on top of its label.
- Getting ready for the **Microsoft Store**: the same app can now run as a Store package, where the Store keeps it up to date and Windows' Startup apps handle "Start OrbitDrop when I sign in". The GitHub version works as before.
- A [privacy policy](https://github.com/i3sam/OrbitDrop/blob/main/docs/PRIVACY.md): your files never leave your PC, and OrbitDrop collects no personal data.

## In 0.2.7-alpha

- **Rearrange your wheel:** in Settings → Wheel, drag a slice around the ring. The others make room as you move, and **Save** puts your order on the real wheel. **Reset** brings back the original wheel.
- **The guide keeps one size:** the window no longer jumps between steps. Its "Make the wheel yours" demo now shows dragging too.

## In 0.2.6-alpha

- **A simpler Wheel page.** Settings → Wheel now shows your actual wheel. Pick the kind of file and mode at the top, click a slice to hide it (it turns into a dashed outline), click again to bring it back, then press **Save**. **Undo changes** and **Show all** are right there.
- The guide's "Make the wheel yours" step shows the new page.

## In 0.2.5-alpha

- **Make the wheel yours:** Settings → **Wheel** lists everything OrbitDrop can do, by kind of file. Click what you don't use to turn it off: whole groups (like Resize) or single choices (like Compress 25%). The wheel shows only what's left; **Show everything** brings it all back.
- **Ctrl + Alt + Space, then switch modes:** click the centre of the wheel (it says where it leads, e.g. "Click for Tools"), press the shortcut again, or tap Shift. Esc or a click outside closes it.
- **The guide** shows both: the keyboard step now demonstrates switching modes, and a new step shows customizing the wheel.
- Fix: in the guide, the option under the pointer now lights up correctly (white in dark mode).

## In 0.2.4-alpha

- **Automatic updates.** OrbitDrop now downloads updates quietly, checks them against their published checksum, and installs them the next time you're not using it (no wheel open, nothing converting, no OrbitDrop windows open). It restarts in a few seconds with your settings. Prefer to be asked? Turn off **Install updates automatically** in Settings → About.
- Update checks no longer fail when GitHub limits requests from your network (common on shared office or school connections).

> Coming from 0.2.3 or earlier: click **Install** on the update card once. From then on, updates install themselves.

## In 0.2.3-alpha

- **A brand-new guide** (tray menu → How to use OrbitDrop, or Settings → General): animated, step-by-step demonstrations built from the real wheel, covering dragging and tapping Shift, choosing an option, switching modes, grouped options and the Ctrl + Alt + Space shortcut. It ends with a **hands-on practice**: drag a sample photo, tap Shift, let go on JPG, and the guide confirms when you've done it.
- You can now tap Shift while dragging files **out of the Float shelf** too.

## In 0.2.2-alpha

- **Trim** video and audio: drag the start and end of the part to keep, play it, save a copy.
- **Float**: park files on a small always-on-top shelf and drag them out later, one at a time or all at once (great for email attachments and uploads). Drop more files on it any time.

## In 0.2.1-alpha

- **Ctrl + Alt + Space**: select files in File Explorer or on the desktop, press the shortcut, and click an option in the wheel. No dragging needed. Turn it off in Settings → Shortcuts.
- **Video to GIF**: a looping animated GIF of the first 10 seconds.
- **Normalize audio**: makes quiet recordings as loud as they can be without clipping.

## In 0.2.0-alpha

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

**Easiest: [get OrbitDrop from the Microsoft Store](https://apps.microsoft.com/detail/9NJQRMQLL2SZ).** Free, no security warning, updated by the Store. (Already have the version from here? Uninstall it first, so only one OrbitDrop runs.)

Or download it here:

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
