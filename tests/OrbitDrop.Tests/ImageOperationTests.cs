using System.Windows;
using System.Windows.Media.Imaging;
using OrbitDrop.Conversion;
using OrbitDrop.Conversion.Imaging;
using OrbitDrop.Core;
using OrbitDrop.Tools;

namespace OrbitDrop.Tests;

/// <summary>Integration tests on real files through the real WIC codecs. Output contents are decoded and checked.</summary>
public class ImageOperationTests
{
    private static async Task<List<FileOutcome>> Run(IOperationProvider provider, string id, FileSet files, OperationContext? ctx = null)
    {
        var op = Enum.GetValues<OperationMode>().SelectMany(m => provider.GetOperations(files, m)).Single(d => d.Id == id);
        var c = new Collector();
        await provider.ExecuteAsync(op, files, ctx ?? Ctx.Default(), c, CancellationToken.None);
        return c.Outcomes;
    }

    private static (int W, int H) Dims(string path)
    {
        (int, int) result = default;
        StaWorker.RunAsync(() => { var b = WicImageCodec.Load(path); result = (b.PixelWidth, b.PixelHeight); }).GetAwaiter().GetResult();
        return result;
    }

    [Theory]
    [InlineData("png")]
    [InlineData("bmp")]
    [InlineData("tiff")]
    [InlineData("gif")]
    public async Task Jpg_converts_to_each_format_with_identical_dimensions(string target)
    {
        using var t = new TempFolder();
        string src = t.Image("photo.jpg", 320, 200);
        string hash = SafeOutput.Sha256(src);
        var r = await Run(new ImageConversionProvider(), $"convert.image.{target}", new FileSet([src]));
        var outcome = Assert.Single(r);
        Assert.Equal(OutcomeStatus.Succeeded, outcome.Status);
        Assert.Equal(t.File($"photo.{target}"), outcome.Output);
        Assert.Equal((320, 200), Dims(outcome.Output!));
        Assert.Equal(hash, SafeOutput.Sha256(src));
    }

    [Fact]
    public async Task Transparent_png_to_jpg_is_flattened_onto_white()
    {
        using var t = new TempFolder();
        string src = t.Image("hole.png", 200, 160, hole: true);
        var r = await Run(new ImageConversionProvider(), "convert.image.jpg", new FileSet([src]));
        byte[] px = new byte[4];
        await StaWorker.RunAsync(() => WicImageCodec.Load(r[0].Output!).CopyPixels(new Int32Rect(100, 80, 1, 1), px, 4, 0));
        Assert.All(px.Take(3), v => Assert.True(v > 235, $"channel {v} should be near white"));
    }

    [Fact]
    public async Task Batch_skips_files_already_in_the_target_format()
    {
        using var t = new TempFolder();
        var files = new FileSet([t.Image("a.jpg", 50, 40), t.Image("b.bmp", 50, 40)]);
        var r = await Run(new ImageConversionProvider(), "convert.image.jpg", files);
        Assert.Equal(OutcomeStatus.Skipped, r.Single(o => o.Source.EndsWith("a.jpg")).Status);
        Assert.Equal(OutcomeStatus.Succeeded, r.Single(o => o.Source.EndsWith("b.bmp")).Status);
    }

    [Fact]
    public async Task Corrupt_file_fails_with_a_clear_message_and_others_continue()
    {
        using var t = new TempFolder();
        File.WriteAllText(t.File("broken.png"), "not an image");
        var files = new FileSet([t.File("broken.png"), t.Image("ok.png", 30, 30)]);
        var r = await Run(new ImageConversionProvider(), "convert.image.jpg", files);
        var bad = r.Single(o => o.Source.EndsWith("broken.png"));
        Assert.Equal(OutcomeStatus.Failed, bad.Status);
        Assert.Equal("The file isn't a readable image.", bad.Message);
        Assert.Equal(OutcomeStatus.Succeeded, r.Single(o => o.Source.EndsWith("ok.png")).Status);
        Assert.Empty(SafeOutput.LeftoverTemps(t.Path));
    }

    [Fact]
    public async Task Unicode_names_are_preserved()
    {
        using var t = new TempFolder();
        string src = t.Image("ü nicode 名前.jpg", 40, 30);
        var r = await Run(new ImageConversionProvider(), "convert.image.png", new FileSet([src]));
        Assert.Equal(t.File("ü nicode 名前.png"), r[0].Output);
    }

    [Fact]
    public async Task Rotate_swaps_dimensions_and_keeps_format()
    {
        using var t = new TempFolder();
        string src = t.Image("a.png", 120, 80);
        var r = await Run(new ImageToolsProvider(), "tool.image.rotate-right", new FileSet([src]));
        Assert.Equal(t.File("a (rotated).png"), r[0].Output);
        Assert.Equal((80, 120), Dims(r[0].Output!));
    }

    [Fact]
    public async Task Grayscale_keeps_transparency()
    {
        using var t = new TempFolder();
        string src = t.Image("hole.png", 100, 80, hole: true);
        var r = await Run(new ImageToolsProvider(), "tool.image.grayscale", new FileSet([src]));
        byte[] px = new byte[4];
        await StaWorker.RunAsync(() => WicImageCodec.Load(r[0].Output!).CopyPixels(new Int32Rect(50, 40, 1, 1), px, 4, 0));
        Assert.Equal(0, px[3]);
    }

    [Fact]
    public async Task Copy_path_quotes_paths_with_spaces()
    {
        using var t = new TempFolder();
        var shell = new FakeShell();
        string a = t.Image("my photo.png", 10, 10);
        await Run(new FileActionsProvider(), "action.copy-path", new FileSet([a]), Ctx.Default(shell));
        Assert.Equal($"\"{a}\"", shell.Clipboard);
    }
}
