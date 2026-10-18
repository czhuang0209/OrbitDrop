using System.Windows;
using OrbitDrop.Infrastructure;
using OrbitDrop.UI.Wheel;
using OrbitDrop.Windows.DragDrop;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace OrbitDrop.Tests;

public class WheelGeometryTests
{
    // Points 100 units from the centre, inside the ring (66–140).
    private static int At(double angleDeg, int count, double r = 100) =>
        WheelGeometry.HitTest(r * Math.Cos(angleDeg * Math.PI / 180), r * Math.Sin(angleDeg * Math.PI / 180), count);

    [Theory]
    [InlineData(-90, 0)]   // 12 o'clock → first
    [InlineData(0, 1)]     // 3 o'clock
    [InlineData(90, 2)]    // 6 o'clock
    [InlineData(180, 3)]   // 9 o'clock
    [InlineData(-40, 1)]   // just past the 12/3 boundary (-45°)
    [InlineData(-50, 0)]   // just before it
    [InlineData(-140, 3)]  // just past the 9/12 boundary (-135°)
    public void Four_segments_map_clockwise_from_the_top(double angle, int expected) => Assert.Equal(expected, At(angle, 4));

    [Fact]
    public void Five_segments_cover_the_full_circle_once()
    {
        var hits = Enumerable.Range(0, 360).Select(a => At(a - 90, 5)).ToList();
        Assert.Equal([0, 1, 2, 3, 4], hits.Distinct().Order());
        Assert.Equal(0, hits[0]);
    }

    [Fact]
    public void Centre_is_the_hub_and_beyond_the_disc_is_outside()
    {
        Assert.Equal(WheelGeometry.HitHub, WheelGeometry.HitTest(0, 0, 4));
        Assert.Equal(WheelGeometry.HitHub, WheelGeometry.HitTest(40, 30, 4));
        Assert.Equal(WheelGeometry.HitOutside, WheelGeometry.HitTest(160, 0, 4));
    }

    [Fact]
    public void Rim_beyond_the_ring_still_counts_as_the_segment()
    {
        Assert.Equal(1, At(0, 4, r: 146));
    }

    [Fact]
    public void Empty_wheel_never_returns_a_segment() => Assert.Equal(WheelGeometry.HitHub, At(0, 0));
}

public class SettingsStoreTests
{
    [Fact]
    public void Round_trips_all_values()
    {
        using var t = new TempFolder();
        var store = new SettingsStore(t.File("settings.json"));
        store.Save(new AppSettings { Theme = AppTheme.Dark, JpegQuality = 75, TriggerKey = TriggerKey.Ctrl, WheelSize = WheelSize.Large });
        var loaded = store.Load();
        Assert.Equal((AppTheme.Dark, 75, TriggerKey.Ctrl, WheelSize.Large), (loaded.Theme, loaded.JpegQuality, loaded.TriggerKey, loaded.WheelSize));
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        using var t = new TempFolder();
        var s = new SettingsStore(t.File("none.json")).Load();
        Assert.Equal(AppTheme.System, s.Theme);
        Assert.Equal(TriggerKey.Shift, s.TriggerKey);
    }

    [Fact]
    public void Corrupt_file_is_backed_up_and_defaults_are_used()
    {
        using var t = new TempFolder();
        File.WriteAllText(t.File("settings.json"), "{ this is not json");
        var s = new SettingsStore(t.File("settings.json")).Load();
        Assert.Equal(AppTheme.System, s.Theme);
        Assert.Single(Directory.GetFiles(t.Path, "settings.json.corrupt-*"));
    }

    [Fact]
    public void Hand_edited_out_of_range_values_are_normalised()
    {
        using var t = new TempFolder();
        File.WriteAllText(t.File("settings.json"), """{ "jpegQuality": 7, "outputLocation": "folder" }""");
        var s = new SettingsStore(t.File("settings.json")).Load();
        Assert.Equal(50, s.JpegQuality);
        Assert.Equal(Core.OutputLocation.BesideOriginal, s.OutputLocation); // folder chosen but no folder set
    }
}

public class DataObjectTests
{
    [Fact]
    public async Task Reads_unicode_paths_from_CF_HDROP_through_the_COM_interface()
    {
        string[] files = [@"C:\x\a.jpg", @"C:\x\ü nicode 名前.png"];
        string[]? read = null;
        await Conversion.StaWorker.RunAsync(() =>
        {
            var com = (ComIDataObject)new DataObject(DataFormats.FileDrop, files);
            read = DataObjectReader.ReadFileDrop(com);
        });
        Assert.Equal(files, read);
    }
}
