using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitDrop.Core;
using OrbitDrop.UI;
using OrbitDrop.UI.Onboarding;
using OrbitDrop.UI.Toasts;
using OrbitDrop.UI.Wheel;

namespace OrbitDrop.App;

/// <summary>
/// Renders the guide's visuals with the real wheel, real options (from the live registry) and the real notification,
/// in the current theme. Renders are offscreen; nothing is shown or captured from the screen.
/// </summary>
internal sealed class GuideAssets(OperationRegistry registry) : IGuideAssets
{
    private const double Scale = 2.0;
    private RadialWheel? _wheel;
    private ImageSource? _backdrop;

    public WheelImage Wheel(WheelShot shot)
    {
        _wheel ??= new RadialWheel();
        _backdrop ??= Backdrop();
        var files = new FileSet(shot.Files.Select(f => Path.Combine(Path.GetTempPath(), f)));
        var flat = registry.Query(files, shot.Mode);
        var items = shot.Group is null ? OperationMenu.TopLevel(flat) : OperationMenu.Members(flat, shot.Group);
        _wheel.ApplyTheme();
        _wheel.SetContent(shot.Mode, items, shot.Value, shot.Detail, animate: false);
        _wheel.SetHighlight(shot.Highlight);
        var image = _wheel.RenderPreview(_backdrop, Scale, wallpaperBehind: false);
        return new WheelImage(image, items.Select(i => i.Label).ToList());
    }

    public BitmapSource Toast(string title, string detail)
    {
        var toast = new ToastWindow();
        toast.SetResult(ToastKind.Success, title, detail, canShow: true, TimeSpan.Zero);
        var root = toast.ContentRoot;
        root.Measure(new Size(372, double.PositiveInfinity));
        root.Arrange(new Rect(0, 0, 372, root.DesiredSize.Height));
        root.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)(372 * Scale), (int)Math.Ceiling(root.DesiredSize.Height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bmp.Render(root);
        bmp.Freeze();
        toast.Close();
        return bmp;
    }

    /// <summary>What the frosted wheel blurs: the guide stage's own background, so the glass reads correctly.</summary>
    private static ImageSource Backdrop()
    {
        double s = WheelGeometry.Extent;
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
            dc.DrawRectangle(new LinearGradientBrush(ThemeManager.Token("SurfaceAlt"), ThemeManager.Token("AccentSubtle"), 30), null, new Rect(0, 0, s, s));
        var bmp = new RenderTargetBitmap((int)s, (int)s, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(v);
        bmp.Freeze();
        return bmp;
    }

    public void Dispose() => _wheel?.Close();
}
