using OrbitDrop.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OrbitDrop.Conversion.Media;

/// <summary>
/// Video and audio conversion through <b>Windows Media Foundation</b> (MediaTranscoder), which is part of Windows.
/// Nothing is bundled or downloaded. Decoding uses whatever codecs Windows has installed; if one is missing, the file
/// fails with a clear message rather than producing a broken output.
/// Outputs: MP4 (H.264/AAC), WMV, M4A (AAC), MP3, WAV, WMA. Tools: compress to 720p, remove audio.
/// </summary>
public sealed class MediaFoundationProvider : IOperationProvider
{
    public const string ProviderId = "media.mf";
    public string Id => ProviderId;

    private static readonly HashSet<string> VideoInputs = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".m4v", ".mov", ".wmv", ".avi", ".mkv", ".webm", ".3gp", ".mts", ".m2ts", ".ts" };
    private static readonly HashSet<string> AudioInputs = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".aac", ".wma", ".flac" };

    private sealed record Target(string Label, string Extension, bool IsVideo);

    private static readonly Target[] VideoTargets = [new("MP4", ".mp4", true), new("WMV", ".wmv", true)];
    private static readonly Target[] AudioTargets = [new("MP3", ".mp3", false), new("M4A", ".m4a", false), new("WAV", ".wav", false), new("WMA", ".wma", false)];

    public static bool IsVideo(string path) => VideoInputs.Contains(Path.GetExtension(path));
    public static bool IsAudio(string path) => AudioInputs.Contains(Path.GetExtension(path));
    private static bool IsMedia(string path) => IsVideo(path) || IsAudio(path);

    public IEnumerable<OperationDescriptor> GetOperations(FileSet files, OperationMode mode)
    {
        if (!files.All(IsMedia)) yield break;
        bool allVideo = files.All(IsVideo);
        var extensions = files.Extensions.ToList();
        bool Same(Target t) => extensions.Count == 1 && (extensions[0] == t.Extension || (t.Extension == ".mp4" && extensions[0] == ".m4v"));

        if (mode == OperationMode.Convert)
        {
            // Video files can become video or audio; audio files only audio (the intersection rule).
            var targets = allVideo ? VideoTargets.Concat(AudioTargets) : AudioTargets;
            int order = 20;
            foreach (var t in targets)
            {
                order++;
                if (Same(t)) continue;
                yield return new($"convert.media{t.Extension}", ProviderId, OperationMode.Convert, t.Label, null,
                    allVideo && !t.IsVideo ? "Extract audio as %1" : "Convert to %1", t.Label,
                    allVideo && !t.IsVideo ? $"Extracting audio as {t.Label}" : $"Converting to {t.Label}", order);
            }
        }
        else if (mode == OperationMode.Tools && allVideo)
        {
            yield return new("tool.video.compress", ProviderId, OperationMode.Tools, "Compress", "",
                "%1", "Compress to 720p", "Compressing video", 120);
            yield return new("tool.video.mute", ProviderId, OperationMode.Tools, "Remove audio", "",
                "%1", "Remove audio", "Removing audio", 121);
        }
    }

    public async Task ExecuteAsync(OperationDescriptor operation, FileSet files, OperationContext context,
        IProgress<FileOutcome> progress, CancellationToken ct)
    {
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (profile, extension, suffix) = await ProfileForAsync(operation, file);
                if (extension == Path.GetExtension(file).ToLowerInvariant() && suffix is null)
                {
                    progress.Report(new FileOutcome(file, OutcomeStatus.Skipped, Message: $"Already {operation.Label}"));
                    continue;
                }
                string output = await Task.Run(() => SafeOutput.WriteToPath(file, context.Output, extension, suffix,
                    writeToTemp: temp => TranscodeAsync(file, temp, profile, context.ReportFileFraction, ct).GetAwaiter().GetResult(),
                    validate: temp => ValidateContainer(temp, extension),
                    ct, hideWhileWriting: false), ct);
                progress.Report(new FileOutcome(file, OutcomeStatus.Succeeded, output));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress.Report(new FileOutcome(file, OutcomeStatus.Failed, Message: Friendly(ex)));
            }
        }
    }

    private static async Task<(MediaEncodingProfile Profile, string Extension, string? Suffix)> ProfileForAsync(OperationDescriptor op, string file)
    {
        switch (op.Id)
        {
            case "convert.media.mp4": return (MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto), ".mp4", null);
            case "convert.media.wmv": return (MediaEncodingProfile.CreateWmv(VideoEncodingQuality.Auto), ".wmv", null);
            case "convert.media.mp3": return (MediaEncodingProfile.CreateMp3(AudioEncodingQuality.High), ".mp3", null);
            case "convert.media.m4a": return (MediaEncodingProfile.CreateM4a(AudioEncodingQuality.High), ".m4a", null);
            case "convert.media.wav": return (MediaEncodingProfile.CreateWav(AudioEncodingQuality.High), ".wav", null);
            case "convert.media.wma": return (MediaEncodingProfile.CreateWma(AudioEncodingQuality.High), ".wma", null);
            case "tool.video.mute":
            {
                var p = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                p.Audio = null;
                return (p, ".mp4", "no audio");
            }
            case "tool.video.compress":
            {
                var p = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                var source = await StorageFile.GetFileFromPathAsync(file);
                var props = await source.Properties.GetVideoPropertiesAsync();
                (uint w, uint h) = FitWithin(props.Width, props.Height, 1280, 720);
                if (w > 0 && h > 0)
                {
                    p.Video.Width = w;
                    p.Video.Height = h;
                    p.Video.Bitrate = (uint)Math.Clamp(w * h * 2.6, 400_000, 2_500_000); // ≈2.4 Mbps at 720p
                }
                if (p.Audio is not null) p.Audio.Bitrate = 128_000;
                return (p, ".mp4", "compressed");
            }
            default: throw new NotSupportedException(op.Id);
        }
    }

    /// <summary>Scales to fit inside a box, keeping aspect ratio, never upscaling, with even dimensions (H.264 requirement).</summary>
    public static (uint W, uint H) FitWithin(uint width, uint height, uint maxW, uint maxH)
    {
        if (width == 0 || height == 0) return (0, 0);
        bool portrait = height > width;
        (uint boxW, uint boxH) = portrait ? (maxH, maxW) : (maxW, maxH);
        double scale = Math.Min(1.0, Math.Min((double)boxW / width, (double)boxH / height));
        uint w = (uint)Math.Round(width * scale) & ~1u, h = (uint)Math.Round(height * scale) & ~1u;
        return (Math.Max(2, w), Math.Max(2, h));
    }

    private static async Task TranscodeAsync(string file, string destinationPath, MediaEncodingProfile profile,
        Action<double>? fraction, CancellationToken ct)
    {
        var source = await StorageFile.GetFileFromPathAsync(file);
        var destination = await StorageFile.GetFileFromPathAsync(destinationPath);
        using IRandomAccessStream input = await source.OpenAsync(FileAccessMode.Read);
        // A native Windows stream: muxers such as ASF (WMV/WMA) need features a wrapped .NET stream lacks.
        using IRandomAccessStream output = await destination.OpenAsync(FileAccessMode.ReadWrite);
        var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
        var prepared = await transcoder.PrepareStreamTranscodeAsync(input, output, profile);
        if (!prepared.CanTranscode)
            throw new MediaFormatException(prepared.FailureReason switch
            {
                TranscodeFailureReason.CodecNotFound => "Windows doesn't have a codec for this file. Installing the matching Video Extension from the Microsoft Store may help.",
                TranscodeFailureReason.InvalidProfile => profile.Audio is not null && profile.Video is null
                    ? "This file has no sound to extract."
                    : "This file can't be converted to that format.",
                _ => "Windows couldn't read this file.",
            });
        var progress = new Progress<double>(p => fraction?.Invoke(p / 100.0));
        await prepared.TranscodeAsync().AsTask(ct, progress);
        await output.FlushAsync();
        fraction?.Invoke(1);
    }

    /// <summary>Checks the container signature, so a transcoder that "succeeds" with garbage can't publish a file.</summary>
    public static string? ValidateContainer(string path, string extension)
    {
        var head = new byte[16];
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (fs.Length < 64) return "the output is too small to be valid";
            fs.ReadExactly(head);
        }
        bool ok = extension switch
        {
            ".mp4" or ".m4a" => head[4] == 'f' && head[5] == 't' && head[6] == 'y' && head[7] == 'p',
            ".wav" => head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'A' && head[10] == 'V' && head[11] == 'E',
            ".mp3" => (head[0] == 'I' && head[1] == 'D' && head[2] == '3') || (head[0] == 0xFF && (head[1] & 0xE0) == 0xE0),
            ".wmv" or ".wma" => head[0] == 0x30 && head[1] == 0x26 && head[2] == 0xB2 && head[3] == 0x75, // ASF header GUID
            _ => true,
        };
        return ok ? null : $"the output isn't a valid {extension.TrimStart('.').ToUpperInvariant()} file";
    }

    private static string Friendly(Exception ex) => ex switch
    {
        MediaFormatException m => m.Message,
        FileNotFoundException => "The file no longer exists.",
        UnauthorizedAccessException => "Windows denied access (read-only folder or file in use).",
        IOException io when io.HResult == unchecked((int)0x80070070) => "There isn't enough disk space.",
        _ when ex.HResult == unchecked((int)0xC00D36C4) => "Windows doesn't support this file's format.", // MF_E_UNSUPPORTED_BYTESTREAM_TYPE
        _ => $"Windows reported an error converting this file (0x{ex.HResult:X8}).",
    };

    public IEnumerable<(string Category, string Detail)> DescribeCapabilities()
    {
        yield return ("Video", "MP4, WMV · extract audio · compress to 720p · remove audio (via Windows Media Foundation)");
        yield return ("Audio", "MP3, M4A, WAV, WMA");
    }

    private sealed class MediaFormatException(string message) : Exception(message);
}
