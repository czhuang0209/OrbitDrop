using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OrbitDrop.UI.Wheel;
using static OrbitDrop.UI.Wheel.WheelGeometry;

namespace OrbitDrop.UI.Settings;

/// <summary>
/// An editable copy of the wheel for Settings → Wheel: the same slices, in the same places, as the real one. Click a
/// slice to hide it (it turns into a faded, dashed outline) or to bring it back.
/// </summary>
public sealed class WheelEditor : FrameworkElement
{
    private IReadOnlyList<WheelEntry> _entries = [];
    private IReadOnlySet<string> _hidden = new HashSet<string>();
    private int _hover = HitOutside;

    /// <summary>A slice was clicked: its key (an option id or a group key).</summary>
    public event Action<string>? Toggled;

    public WheelEditor()
    {
        Cursor = Cursors.Arrow;
        Focusable = false;
    }

    /// <summary>Highlights a slice as if hovered (the guide's demo; real hovering sets this itself).</summary>
    public void SetHover(int index)
    {
        if (_hover == index) return;
        _hover = index;
        InvalidateVisual();
    }

    public void Show(IReadOnlyList<WheelEntry> entries, IReadOnlySet<string> hidden)
    {
        _entries = entries;
        _hidden = hidden;
        InvalidateVisual();
    }

    private double Scale => Math.Min(ActualWidth, ActualHeight) / Disc;

    private int Hit(Point p)
    {
        double k = Scale;
        if (k <= 0) return HitOutside;
        return HitTest(p.X / k - Disc / 2, p.Y / k - Disc / 2, _entries.Count);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int hit = Hit(e.GetPosition(this));
        Cursor = hit >= 0 ? Cursors.Hand : Cursors.Arrow;
        if (hit == _hover) return;
        _hover = hit;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = HitOutside;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        int hit = Hit(e.GetPosition(this));
        if (hit >= 0 && hit < _entries.Count) Toggled?.Invoke(_entries[hit].Choice.Key);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double k = Scale;
        if (k <= 0) return;
        // Draw in the wheel's own units (a 300-unit disc), centred, then scale to the control.
        dc.PushTransform(new ScaleTransform(k, k));
        dc.PushTransform(new TranslateTransform(Disc / 2 - Extent / 2, Disc / 2 - Extent / 2));
        var c = new Point(Extent / 2, Extent / 2);

        Color segment = ThemeManager.Token("WheelSegment"), text = ThemeManager.Token("WheelSegmentText"), accent = ThemeManager.Token("Accent");
        dc.DrawEllipse(new SolidColorBrush(ThemeManager.Token("WheelBase")), new Pen(new SolidColorBrush(Color.FromArgb(0x30, text.R, text.G, text.B)), 1), c, DiscR, DiscR);

        int n = _entries.Count;
        double step = Step(n);
        var display = (FontFamily)FindResource("Orbit.Font.Display");
        var icons = (FontFamily)FindResource("Orbit.Font.Icons");
        for (int i = 0; i < n; i++)
        {
            var entry = _entries[i];
            bool hidden = _hidden.Contains(entry.Choice.Key), hover = i == _hover;
            double mid = MidAngle(i, n);
            var geo = RadialWheel.Sector(mid - step / 2, mid + step / 2, n == 1 ? 0 : Gap);
            Brush fill = hidden ? Brushes.Transparent : new SolidColorBrush(hover ? Blend(segment, accent, 0.18) : segment);
            var outline = hidden
                ? new Pen(new SolidColorBrush(Color.FromArgb(hover ? (byte)0xB0 : (byte)0x66, text.R, text.G, text.B)), 1.5) { DashStyle = new DashStyle([4, 3], 0) }
                : new Pen(new SolidColorBrush(hover ? accent : segment), hover ? 2 : Corner) { LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(fill, outline, geo);

            // Glyph and label at the same radius as the real wheel; hidden slices say so.
            double a = mid * Math.PI / 180;
            var at = new Point(c.X + LabelR * Math.Cos(a), c.Y + LabelR * Math.Sin(a));
            byte alpha = hidden ? (byte)0x70 : (byte)0xFF;
            var ink = new SolidColorBrush(Color.FromArgb(alpha, text.R, text.G, text.B));
            var label = Text(entry.Choice.Label, display, n > 6 ? 10.5 : 11.5, FontWeights.SemiBold, ink, 74);
            var glyph = entry.Choice.Glyph is { } g ? Text(hidden ? "" : g, icons, 17, FontWeights.Normal, ink, 40) : hidden ? Text("", icons, 15, FontWeights.Normal, ink, 40) : null;
            double total = label.Height + (glyph is null ? 0 : glyph.Height + 2);
            double y = at.Y - total / 2;
            if (glyph is not null) { dc.DrawText(glyph, new Point(at.X - 20, y)); y += glyph.Height + 2; }
            dc.DrawText(label, new Point(at.X - 37, y));
            if (entry.Members.Count > 0 && !hidden)
                dc.DrawText(Text("• • •", display, 7, FontWeights.Normal, ink, 40), new Point(at.X - 20, y + label.Height + 1));
        }

        // Hub: how many slices are on.
        dc.DrawEllipse(new SolidColorBrush(ThemeManager.Token("WheelHub")), new Pen(new SolidColorBrush(Color.FromArgb(0x22, text.R, text.G, text.B)), 1), c, HubR, HubR);
        int shown = _entries.Count(e => !_hidden.Contains(e.Choice.Key));
        var ink2 = new SolidColorBrush(text);
        var big = Text(n == 0 ? "—" : $"{shown} of {n}", display, 17, FontWeights.SemiBold, ink2, 100);
        var small = Text(_hover >= 0 && _hover < n ? (_hidden.Contains(_entries[_hover].Choice.Key) ? "Click to show" : "Click to hide") : "on the wheel",
            display, 10, FontWeights.Normal, new SolidColorBrush(Color.FromArgb(0xA0, text.R, text.G, text.B)), 100);
        dc.DrawText(big, new Point(c.X - 50, c.Y - big.Height / 2 - 6));
        dc.DrawText(small, new Point(c.X - 50, c.Y + big.Height / 2 - 4));
        dc.Pop();
        dc.Pop();
    }

    private FormattedText Text(string s, FontFamily family, double size, FontWeight weight, Brush brush, double maxWidth)
    {
        var t = new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        { TextAlignment = TextAlignment.Center, MaxTextWidth = maxWidth, Trimming = TextTrimming.CharacterEllipsis, MaxLineCount = 2 };
        return t;
    }

    private static Color Blend(Color a, Color b, double k) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * k), (byte)(a.G + (b.G - a.G) * k), (byte)(a.B + (b.B - a.B) * k));
}
