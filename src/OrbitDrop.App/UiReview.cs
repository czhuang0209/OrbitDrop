using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitDrop.Core;
using OrbitDrop.Infrastructure;
using OrbitDrop.UI;
using OrbitDrop.UI.Onboarding;
using OrbitDrop.UI.Settings;
using OrbitDrop.UI.Toasts;
using OrbitDrop.UI.Tray;
using OrbitDrop.UI.Wheel;

namespace OrbitDrop.App;

/// <summary>
/// <c>OrbitDrop.exe --render-ui &lt;folder&gt;</c>: renders every surface offscreen, in light and dark, at 100% and 150%,
/// using the real registry and real view models. No screen capture, no settings written. Used for design review.
/// </summary>
internal static class UiReview
{
    public static int Run(string dir, OperationRegistry registry)
    {
        Directory.CreateDirectory(dir);
        int count = 0;
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
        {
            ThemeManager.Current.Apply(theme, useWindowsAccent: false, reduceMotion: true);
            string t = theme.ToString().ToLowerInvariant();
            var bg = (Brush)Application.Current.FindResource("Orbit.SurfaceBase");

            // --- wheel on a bright and a dark wallpaper
            foreach (var (wallName, wall) in new[] { ("bright", Wallpaper(false)), ("dark", Wallpaper(true)) })
            {
                var cases = new (string Name, OperationMode Mode, string[] Files, int Hi, string Value, string Detail)[]
                {
                    ("convert", OperationMode.Convert, ["photo.jpg"], 1, "BMP", "Convert to BMP"),
                    ("convert-batch", OperationMode.Convert, ["a.jpg", "b.png", "c.gif"], -2, "3 files", "JPG · PNG · GIF"),
                    ("tools", OperationMode.Tools, ["photo.jpg"], 0, "Rotate right", "Rotate right"),
                    ("actions", OperationMode.Actions, ["report.pdf"], 2, "Show in folder", "Show in folder"),
                    ("video", OperationMode.Convert, ["clip.mp4"], 1, "MP3", "Extract audio as MP3"),
                    ("video-tools", OperationMode.Tools, ["clip.mp4"], 0, "Compress", "clip.mp4"),
                    ("unsupported", OperationMode.Convert, ["report.pdf"], -2, "No conversions yet", "Tap Shift for more"),
                };
                foreach (var c in cases)
                {
                    var wheel = new RadialWheel();
                    var items = registry.Query(new FileSet(c.Files.Select(f => Path.Combine(Path.GetTempPath(), f))), c.Mode);
                    wheel.SetContent(c.Mode, items, c.Value, c.Detail, animate: false);
                    wheel.SetHighlight(c.Hi);
                    foreach (double scale in new[] { 1.0, 1.5 })
                        Save(wheel.RenderPreview(wall, scale), Path.Combine(dir, $"wheel-{c.Name}-{wallName}-{t}-{scale * 100:F0}.png"), ref count);
                    wheel.Close();
                }
            }

            // --- toasts
            var toastStates = new (string Name, Action<ToastWindow> Setup)[]
            {
                ("running", w => w.SetRunning("Converting to PNG", "2 of 5 · holiday-photo.jpg", 0.4, false)),
                ("success", w => w.SetResult(ToastKind.Success, "Saved holiday-photo.png", "In Pictures", true, TimeSpan.Zero)),
                ("partial", w => w.SetResult(ToastKind.Warning, "4 saved · 1 failed", "scan.tif: The file isn't a readable image.", true, TimeSpan.Zero)),
                ("error", w => w.SetResult(ToastKind.Error, "That didn't work", "notes.png: Windows denied access (read-only folder or file in use).", false, TimeSpan.Zero)),
                ("canceled", w => w.SetResult(ToastKind.Info, "Canceled", "3 of 8 finished before you canceled; nothing partial was left behind.", false, TimeSpan.Zero)),
            };
            foreach (var (name, setup) in toastStates)
            {
                var toast = new ToastWindow();
                setup(toast);
                RenderElement(toast.ContentRoot, 372, double.NaN, bg, dir, $"toast-{name}-{t}", ref count);
                toast.Close();
            }

            // --- settings pages
            var host = new PreviewHost(registry);
            foreach (var page in SettingsWindow.PageNames)
            {
                var window = new SettingsWindow(new SettingsViewModel(host));
                window.ShowPage(page);
                RenderElement(window.ContentRoot, 880, 620, bg, dir, $"settings-{page.ToLowerInvariant()}-{t}", ref count);
                window.Close();
            }

            // --- onboarding
            for (int step = 0; step < 3; step++)
            {
                var onboarding = new OnboardingWindow("Shift");
                onboarding.ShowStep(step);
                RenderElement(onboarding.ContentRoot, 560, 470, bg, dir, $"onboarding-{step + 1}-{t}", ref count);
                onboarding.Close();
            }

            // --- tray flyout
            var flyout = new TrayFlyout(new PreviewTray());
            RenderElement(flyout.ContentRoot, 300, double.NaN, bg, dir, $"tray-{t}", ref count);
            flyout.Close();
        }
        File.WriteAllText(Path.Combine(dir, "README.txt"), $"{count} renders · {DateTime.Now:yyyy-MM-dd HH:mm}\r\n");
        return 0;
    }

    private static void RenderElement(FrameworkElement element, double width, double height, Brush background, string dir, string name, ref int count)
    {
        element.Measure(new Size(width, double.IsNaN(height) ? double.PositiveInfinity : height));
        double h = double.IsNaN(height) ? element.DesiredSize.Height : height;
        double w = double.IsNaN(width) ? element.DesiredSize.Width : width;
        element.Arrange(new Rect(0, 0, w, h));
        element.UpdateLayout();
        foreach (double scale in new[] { 1.0, 1.5 })
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) dc.DrawRectangle(background, null, new Rect(0, 0, w, h));
            var bmp = new RenderTargetBitmap((int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Render(element); // direct render keeps layout offsets (a VisualBrush would stretch to content bounds)
            Save(bmp, Path.Combine(dir, $"{name}-{scale * 100:F0}.png"), ref count);
        }
    }

    private static void Save(BitmapSource bmp, string path, ref int count)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
        count++;
    }

    /// <summary>Synthetic wallpapers: a busy bright gradient and a deep dark one, to check legibility on both.</summary>
    private static ImageSource Wallpaper(bool dark)
    {
        var dv = new DrawingVisual();
        double s = WheelGeometry.Extent;
        using (var dc = dv.RenderOpen())
        {
            var a = dark ? Color.FromRgb(0x10, 0x1A, 0x2E) : Color.FromRgb(0xFF, 0xC8, 0x5A);
            var b = dark ? Color.FromRgb(0x3A, 0x1F, 0x4F) : Color.FromRgb(0x4F, 0xC3, 0xF7);
            dc.DrawRectangle(new LinearGradientBrush(a, b, 35), null, new Rect(0, 0, s, s));
            dc.DrawEllipse(new SolidColorBrush(dark ? Color.FromArgb(120, 0x2E, 0x8B, 0x57) : Color.FromArgb(150, 0xFF, 0x4F, 0x81)), null, new Point(s * 0.3, s * 0.35), s * 0.22, s * 0.22);
            dc.DrawEllipse(new SolidColorBrush(dark ? Color.FromArgb(90, 0xFF, 0xFF, 0xFF) : Color.FromArgb(160, 0xFF, 0xFF, 0xFF)), null, new Point(s * 0.72, s * 0.7), s * 0.16, s * 0.16);
        }
        var bmp = new RenderTargetBitmap((int)s, (int)s, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    private sealed class PreviewHost(OperationRegistry registry) : ISettingsHost
    {
        public AppSettings Settings { get; } = new() { OutputFolder = @"C:\Users\You\Pictures\Converted" };
        public void Update(Action<AppSettings> change) => change(Settings);
        public bool LaunchAtStartup { get; set; } = true;
        public bool AccentFallbackActive => false;
        public IEnumerable<(string Category, string Detail)> Capabilities => registry.Providers.SelectMany(p => p.DescribeCapabilities());
        public string VersionText => "Version 0.1.0-alpha (design review)";
        public string SettingsPath => @"C:\Users\You\AppData\Roaming\OrbitDrop\settings.json";
        public string LogsPath => @"C:\Users\You\AppData\Local\OrbitDrop\logs";
        public string? LicensePath => "LICENSE.txt";
        public void PreviewWheel() { }
        public void ShowOnboarding() { }
        public void OpenPath(string path) { }
        public void ResetSettings() { }
    }

    private sealed class PreviewTray : ITrayCommands
    {
        public bool Paused { get; set; }
        public string TriggerKeyName => "Shift";
        public void OpenSettings(string? page = null) { }
        public void ShowOnboarding() { }
        public void Quit() { }
    }
}
