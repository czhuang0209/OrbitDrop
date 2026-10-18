using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// OrbitDrop brand icon: violet→sky tile, a tilted orbit ring around a document, and a satellite dot.
// At 16–24 px the document is dropped and the ring is thickened so the mark stays legible.

namespace IconGen;

internal static class Program
{
    private static readonly Color Violet = Color.FromRgb(0x7A, 0x5C, 0xFF);
    private static readonly Color Sky = Color.FromRgb(0x3F, 0xA8, 0xFF);
    private static readonly Color Ink = Color.FromRgb(0x4B, 0x3B, 0xD6);

    [STAThread]
    private static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : "assets/brand";
        Directory.CreateDirectory(outDir);

        int[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
        var pngs = icoSizes.Select(s => (Size: s, Data: Png(Render(s)))).ToList();
        File.WriteAllBytes(Path.Combine(outDir, "orbitdrop.ico"), Ico(pngs));
        foreach (int s in new[] { 32, 64, 256, 512 })
            File.WriteAllBytes(Path.Combine(outDir, $"orbitdrop-{s}.png"), Png(Render(s)));

        Console.WriteLine($"Wrote icon set to {Path.GetFullPath(outDir)}");
        return 0;
    }

    private static BitmapSource Render(int px)
    {
        double s = px;
        bool small = px <= 24;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // Tile (Windows-style rounded square with a small inset so it doesn't look oversized in the tray).
            double inset = small ? 0 : s * 0.04;
            var tile = new Rect(inset, inset, s - 2 * inset, s - 2 * inset);
            var gradient = new LinearGradientBrush(Violet, Sky, new Point(0, 0), new Point(1, 1));
            dc.DrawRoundedRectangle(gradient, null, tile, tile.Width * 0.24, tile.Height * 0.24);
            // Soft top highlight for depth.
            var sheen = new LinearGradientBrush(Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90);
            dc.DrawRoundedRectangle(sheen, null, tile, tile.Width * 0.24, tile.Height * 0.24);

            var c = new Point(s / 2, s / 2);
            double rx = s * (small ? 0.36 : 0.37), ry = s * (small ? 0.17 : 0.155), tilt = -24;
            double ringW = s * (small ? 0.09 : 0.05);
            var ringPen = new Pen(Brushes.White, ringW) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var backPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), ringW);
            var rotate = new RotateTransform(tilt, c.X, c.Y);

            // Back half of the orbit (behind the document).
            dc.PushTransform(rotate);
            dc.DrawGeometry(null, backPen, HalfEllipse(c, rx, ry, top: true));
            dc.Pop();

            if (small)
            {
                dc.DrawEllipse(Brushes.White, null, c, s * 0.15, s * 0.15); // planet
            }
            else
            {
                // Document with a folded corner.
                double w = s * 0.30, h = s * 0.38, r = s * 0.035, fold = s * 0.09;
                var doc = new Rect(c.X - w / 2, c.Y - h / 2, w, h);
                var body = new StreamGeometry();
                using (var g = body.Open())
                {
                    g.BeginFigure(new Point(doc.Left + r, doc.Top), true, true);
                    g.LineTo(new Point(doc.Right - fold, doc.Top), true, true);
                    g.LineTo(new Point(doc.Right, doc.Top + fold), true, true);
                    g.LineTo(new Point(doc.Right, doc.Bottom - r), true, true);
                    g.ArcTo(new Point(doc.Right - r, doc.Bottom), new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);
                    g.LineTo(new Point(doc.Left + r, doc.Bottom), true, true);
                    g.ArcTo(new Point(doc.Left, doc.Bottom - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);
                    g.LineTo(new Point(doc.Left, doc.Top + r), true, true);
                    g.ArcTo(new Point(doc.Left + r, doc.Top), new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);
                }
                body.Freeze();
                dc.DrawGeometry(Brushes.White, null, body);
                var corner = new StreamGeometry();
                using (var g = corner.Open())
                {
                    g.BeginFigure(new Point(doc.Right - fold, doc.Top), true, true);
                    g.LineTo(new Point(doc.Right - fold, doc.Top + fold), true, true);
                    g.LineTo(new Point(doc.Right, doc.Top + fold), true, true);
                }
                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0xD9, 0xD3, 0xFF)), null, corner);
                var line = new Pen(new SolidColorBrush(Color.FromArgb(150, Ink.R, Ink.G, Ink.B)), Math.Max(1, s * 0.022))
                    { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                for (int i = 0; i < 3; i++)
                {
                    double y = doc.Top + h * (0.42 + i * 0.16);
                    double len = i == 2 ? w * 0.38 : w * 0.56;
                    dc.DrawLine(line, new Point(doc.Left + w * 0.2, y), new Point(doc.Left + w * 0.2 + len, y));
                }
            }

            // Front half of the orbit and the satellite.
            dc.PushTransform(rotate);
            dc.DrawGeometry(null, ringPen, HalfEllipse(c, rx, ry, top: false));
            double a = 28 * Math.PI / 180; // satellite position on the front arc
            var sat = new Point(c.X + rx * Math.Cos(a), c.Y + ry * Math.Sin(a));
            double satR = s * (small ? 0.085 : 0.06);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), null, sat, satR * 1.9, satR * 1.9);
            dc.DrawEllipse(Brushes.White, null, sat, satR, satR);
            dc.Pop();
        }

        var bmp = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
        RenderOptions.SetEdgeMode(dv, EdgeMode.Unspecified);
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    private static Geometry HalfEllipse(Point c, double rx, double ry, bool top)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(c.X - rx, c.Y), false, false);
            ctx.ArcTo(new Point(c.X + rx, c.Y), new Size(rx, ry), 0, false,
                top ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, true);
        }
        g.Freeze();
        return g;
    }

    private static byte[] Png(BitmapSource b)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(b));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>ICO container with PNG-compressed entries (supported since Windows Vista).</summary>
    private static byte[] Ico(List<(int Size, byte[] Data)> images)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)images.Count);
        int offset = 6 + 16 * images.Count;
        foreach (var (size, data) in images)
        {
            w.Write((byte)(size >= 256 ? 0 : size)); w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((ushort)1); w.Write((ushort)32);
            w.Write(data.Length); w.Write(offset);
            offset += data.Length;
        }
        foreach (var (_, data) in images) w.Write(data);
        return ms.ToArray();
    }
}
