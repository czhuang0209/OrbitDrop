using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitDrop.Conversion;
using OrbitDrop.Conversion.Imaging;
using OrbitDrop.Core;

namespace OrbitDrop.Tools;

/// <summary>One-click image tools that need no parameters. Output keeps the source format with a suffix.</summary>
public sealed class ImageToolsProvider : IOperationProvider
{
    public const string ProviderId = "image.tools";
    public string Id => ProviderId;

    private sealed record Tool(string Key, string Label, string Glyph, bool Mirror, string Progress, string Suffix,
        Func<BitmapSource, BitmapSource> Apply);

    private static readonly Tool[] All =
    [
        new("rotate-right", "Rotate right", "", false, "Rotating", "rotated",
            b => WicImageCodec.Transform(b, new RotateTransform(90))),
        new("rotate-left", "Rotate left", "", true, "Rotating", "rotated",
            b => WicImageCodec.Transform(b, new RotateTransform(-90))),
        new("flip", "Flip", "", false, "Flipping", "flipped",
            b => WicImageCodec.Transform(b, new ScaleTransform(-1, 1))),
        new("grayscale", "Grayscale", "", false, "Converting to grayscale", "grayscale",
            WicImageCodec.Grayscale),
    ];

    public IEnumerable<OperationDescriptor> GetOperations(FileSet files, OperationMode mode)
    {
        if (mode != OperationMode.Tools || !files.All(WicImageCodec.IsSupportedInput)) yield break;
        int order = 100;
        foreach (var t in All)
            yield return new OperationDescriptor($"tool.image.{t.Key}", ProviderId, OperationMode.Tools, t.Label, t.Glyph,
                "%1", t.Label, t.Progress, order++, t.Mirror);
    }

    public Task ExecuteAsync(OperationDescriptor operation, FileSet files, OperationContext context,
        IProgress<FileOutcome> progress, CancellationToken ct)
    {
        var tool = All.Single(t => operation.Id == $"tool.image.{t.Key}");
        return StaWorker.RunAsync(() =>
        {
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var format = WicImageCodec.FormatOf(file)!;
                    var result = tool.Apply(WicImageCodec.Load(file));
                    string output = WicImageCodec.WriteOutput(result, file, format, tool.Suffix, context, ct);
                    progress.Report(new FileOutcome(file, OutcomeStatus.Succeeded, output));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    progress.Report(new FileOutcome(file, OutcomeStatus.Failed, Message: ImageConversionProvider.Friendly(ex)));
                }
            }
        }, "OrbitDrop image tools");
    }

    public IEnumerable<(string Category, string Detail)> DescribeCapabilities()
    {
        yield return ("Image tools", string.Join(", ", All.Select(t => t.Label)));
    }
}
