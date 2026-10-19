using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitDrop.Conversion.Imaging;
using OrbitDrop.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace OrbitDrop.Conversion.Media;

/// <summary>
/// Video editing through Windows.Media.Editing (built into Windows): a still frame as PNG, and joining clips into one
/// MP4 in the order they were selected.
/// </summary>
public sealed class MediaEditingProvider : IOperationProvider
{
    public const string ProviderId = "media.editing";
    public string Id => ProviderId;

    public IEnumerable<OperationDescriptor> GetOperations(FileSet files, OperationMode mode)
    {
        if (mode != OperationMode.Tools || !files.All(MediaFoundationProvider.IsVideo)) yield break;
        yield return new("tool.video.snapshot", ProviderId, OperationMode.Tools, "Snapshot", "",
            "Save the middle frame as PNG", "", "Taking a snapshot", 135);
        if (files.Count > 1)
            yield return new("tool.video.join", ProviderId, OperationMode.Tools, "Join", "",
                "Join into one video, in this order", "", "Joining videos", 136);
    }

    public async Task ExecuteAsync(OperationDescriptor operation, FileSet files, OperationContext context,
        IProgress<FileOutcome> progress, CancellationToken ct)
    {
        if (operation.Id == "tool.video.join")
        {
            try
            {
                string output = await JoinAsync(files, context, ct);
                for (int i = 0; i < files.Count; i++) progress.Report(new FileOutcome(files[i], OutcomeStatus.Succeeded, i == 0 ? output : null));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                foreach (var f in files) progress.Report(new FileOutcome(f, OutcomeStatus.Failed, Message: Friendly(ex)));
            }
            return;
        }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try { progress.Report(new FileOutcome(file, OutcomeStatus.Succeeded, await SnapshotAsync(file, context, ct))); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { progress.Report(new FileOutcome(file, OutcomeStatus.Failed, Message: Friendly(ex))); }
        }
    }

    private static async Task<string> SnapshotAsync(string file, OperationContext context, CancellationToken ct)
    {
        var storage = await StorageFile.GetFileFromPathAsync(file);
        var clip = await MediaClip.CreateFromFileAsync(storage);
        var props = await storage.Properties.GetVideoPropertiesAsync();
        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        var at = TimeSpan.FromTicks(clip.OriginalDuration.Ticks / 2);
        int w = (int)(props.Width > 0 ? props.Width : 1280), h = (int)(props.Height > 0 ? props.Height : 720);
        using var thumb = await composition.GetThumbnailAsync(at, w, h, VideoFramePrecision.NearestFrame).AsTask(ct);
        using var source = thumb.AsStreamForRead();
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        byte[] data = buffer.ToArray();

        string output = "";
        await StaWorker.RunAsync(() =>
        {
            using var ms = new MemoryStream(data);
            var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            BitmapSource image = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            output = WicImageCodec.WriteOutput(image, file, WicImageCodec.Png, $"frame {Stamp(at)}", context, ct);
        }, "OrbitDrop snapshot");
        return output;
    }

    private static string Stamp(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\.mm\.ss") : t.ToString(@"m\.ss");

    private static async Task<string> JoinAsync(FileSet files, OperationContext context, CancellationToken ct)
    {
        var composition = new MediaComposition();
        uint width = 0, height = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var storage = await StorageFile.GetFileFromPathAsync(file);
            composition.Clips.Add(await MediaClip.CreateFromFileAsync(storage));
            if (width == 0)
            {
                var props = await storage.Properties.GetVideoPropertiesAsync();
                (width, height) = MediaFoundationProvider.FitWithin(props.Width, props.Height, 3840, 2160);
            }
        }

        // The composition's own profile carries everything its renderer needs (frame rate, pixel aspect …);
        // a hand-built one fails with MF_E_ATTRIBUTENOTFOUND. Only the size and bitrate are adjusted.
        var profile = composition.CreateDefaultEncodingProfile();
        if (width > 0 && height > 0 && profile.Video is not null)
        {
            profile.Video.Width = width;
            profile.Video.Height = height;
            profile.Video.Bitrate = (uint)Math.Clamp(width * height * 4.0, 1_000_000, 16_000_000);
        }

        return await Task.Run(() => SafeOutput.WriteToPath(files[0], context.Output, ".mp4", "joined",
            writeToTemp: temp =>
            {
                var destination = StorageFile.GetFileFromPathAsync(temp).AsTask().GetAwaiter().GetResult();
                var progress = new Progress<double>(p => context.ReportFileFraction?.Invoke(p / 100.0));
                var result = composition.RenderToFileAsync(destination, MediaTrimmingPreference.Precise, profile).AsTask(ct, progress).GetAwaiter().GetResult();
                if (result != TranscodeFailureReason.None) throw new InvalidDataException("Windows couldn't join these videos.");
            },
            validate: temp => MediaFoundationProvider.ValidateContainer(temp, ".mp4"),
            ct, hideWhileWriting: false, tempKeepsExtension: true), ct);
    }

    private static string Friendly(Exception ex) => ex switch
    {
        InvalidDataException => ex.Message,
        FileNotFoundException => "The file no longer exists.",
        UnauthorizedAccessException => "Windows denied access (read-only folder or file in use).",
        _ when ex.HResult == unchecked((int)0xC00D36C4) => "Windows doesn't support this video's format.",
        _ => $"Windows reported an error (0x{ex.HResult:X8}{(string.IsNullOrWhiteSpace(ex.Message) ? "" : ": " + ex.Message.Trim())})",
    };

    public IEnumerable<(string Category, string Detail)> DescribeCapabilities()
    {
        yield return ("Video editing", "Snapshot (middle frame as PNG) · join videos into one MP4");
    }
}
