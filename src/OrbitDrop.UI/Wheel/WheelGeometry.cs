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

    public static double SizeFactor(Infrastructure.WheelSize size) => size switch
    {
        Infrastructure.WheelSize.Compact => 0.86,
        Infrastructure.WheelSize.Large => 1.16,
        _ => 1.0,
    };
}
