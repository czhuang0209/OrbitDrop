namespace OrbitDrop.UI.Wheel;

/// <summary>
/// Wheel layout in base units (Standard size). Rendering scales uniformly to the window, so the same numbers apply at
/// every DPI and wheel size. Pure math, unit-tested.
/// </summary>
public static class WheelGeometry
{
    public const double Disc = 300, Margin = 28, Extent = Disc + 2 * Margin;
    public const double DiscR = Disc / 2, OuterR = 140, InnerR = 66, HubR = 56, Gap = 6, Corner = 10;
    public const double LabelR = (OuterR + InnerR) / 2, HoverLift = 3;
    public const int HitHub = -1, HitOutside = -2;

    public static double Step(int count) => 360.0 / Math.Max(1, count);

    /// <summary>Segment centre angle in degrees (0 = 3 o'clock, clockwise); the first segment is at 12 o'clock.</summary>
    public static double MidAngle(int index, int count) => -90 + index * Step(count);

    /// <summary>
    /// Maps a point relative to the wheel centre (base units) to a segment index, <see cref="HitHub"/> or
    /// <see cref="HitOutside"/>. The rim beyond the ring still counts as its segment, which makes aiming forgiving.
    /// </summary>
    public static int HitTest(double dx, double dy, int count)
    {
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > DiscR) return HitOutside;
        if (dist < InnerR - Gap / 2 || count <= 0) return HitHub;
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        double step = Step(count);
        double rel = ((angle + 90 + step / 2) % 360 + 360) % 360; // 0 at 12 o'clock, clockwise
        return Math.Min(count - 1, (int)(rel / step));
    }

    /// <summary>Clearance kept between a segment label and the ring's inner and outer edges.</summary>
    public const double LabelInset = 6;

    /// <summary>
    /// True when a <paramref name="w"/>×<paramref name="h"/> box centred at (<paramref name="cx"/>, <paramref name="cy"/>)
    /// (relative to the wheel centre) stays inside the ring, so a label can't run into the hub or off the disc.
    /// </summary>
    public static bool LabelFits(double cx, double cy, double w, double h)
    {
        double rMin = InnerR + LabelInset, rMax = OuterR - LabelInset;
        // Nearest point of the box to the centre (clamped), and its farthest corner.
        double nx = Math.Clamp(0, cx - w / 2, cx + w / 2), ny = Math.Clamp(0, cy - h / 2, cy + h / 2);
        double fx = Math.Max(Math.Abs(cx - w / 2), Math.Abs(cx + w / 2)), fy = Math.Max(Math.Abs(cy - h / 2), Math.Abs(cy + h / 2));
        return Math.Sqrt(nx * nx + ny * ny) >= rMin && Math.Sqrt(fx * fx + fy * fy) <= rMax;
    }

    /// <summary>The most the wheel grows for a crowded page (see <see cref="Density"/>).</summary>
    public const double MaxDensity = 1.55;

    /// <summary>
    /// How much bigger the wheel opens when a page has many options: 1 up to 6, then 8% more per option, at most
    /// <see cref="MaxDensity"/>. Labels keep their on-screen size, so the extra room becomes breathing space in the slices.
    /// </summary>
    public static double Density(int count) => count <= 6 ? 1 : Math.Min(MaxDensity, 1 + 0.08 * (count - 6));

    /// <summary>Clearance kept between a label and the sides of its slice, in degrees (the gap between slices is about 1.7° each side there).</summary>
    public const double LabelSideInset = 2.5;

    /// <summary>
    /// True when the box fits inside its slice: within the ring (<see cref="LabelFits(double, double, double, double)"/>)
    /// and between the slice's two sides (centred on <paramref name="mid"/>, <paramref name="step"/> wide, in degrees).
    /// Slices are convex for up to two options' width, so checking the four corners is enough.
    /// </summary>
    public static bool LabelFits(double cx, double cy, double w, double h, double mid, double step)
    {
        if (!LabelFits(cx, cy, w, h)) return false;
        if (step >= 180) return true; // one or two big slices: the ring is all that limits a label
        double half = step / 2 - LabelSideInset;
        foreach (var (x, y) in new[] { (cx - w / 2, cy - h / 2), (cx + w / 2, cy - h / 2), (cx - w / 2, cy + h / 2), (cx + w / 2, cy + h / 2) })
        {
            double angle = Math.Atan2(y, x) * 180 / Math.PI;
            double off = ((angle - mid) % 360 + 540) % 360 - 180; // -180…180 from the slice's centre line
            if (Math.Abs(off) > half) return false;
        }
        return true;
    }

    public static double SizeFactor(Infrastructure.WheelSize size) => size switch
    {
        Infrastructure.WheelSize.Compact => 0.86,
        Infrastructure.WheelSize.Large => 1.16,
        _ => 1.0,
    };
}
