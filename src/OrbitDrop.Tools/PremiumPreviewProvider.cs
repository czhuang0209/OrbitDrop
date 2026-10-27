using OrbitDrop.Conversion.Imaging;
using OrbitDrop.Core;

namespace OrbitDrop.Tools;

/// <summary>The Premium options of the Store version, as names and places only (PremiumCatalog.g.cs).</summary>
public static partial class PremiumCatalog
{
    /// <param name="Kinds">The selections it's offered for: "photo", "photos", "pdf", "pdfs", "video", "audio", "document".</param>
    public sealed record Entry(string Id, OperationMode Mode, string Label, string? Glyph, OperationGroup? Group, int Order, string[] Kinds, bool MirrorGlyph = false);

    private static readonly HashSet<string> Videos = [".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp"];
    private static readonly HashSet<string> Audio = [".mp3", ".wav", ".flac", ".m4a", ".aac", ".wma", ".ogg", ".opus"];
    private static readonly HashSet<string> Documents = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf"];

    /// <summary>Which of <see cref="Entry.Kinds"/> a selection is, or null (a mix, folders, other files).</summary>
    public static string? KindOf(FileSet files)
    {
        static string Ext(string f) => Path.GetExtension(f).ToLowerInvariant();
        bool one = files.Count == 1;
        if (files.All(f => Ext(f) == ".pdf")) return one ? "pdf" : "pdfs";
        if (files.All(f => !Directory.Exists(f) && WicImageCodec.IsSupportedInput(f))) return one ? "photo" : "photos";
        if (files.All(f => Videos.Contains(Ext(f)))) return one ? "video" : null;
        if (files.All(f => Audio.Contains(Ext(f)))) return one ? "audio" : null;
        if (files.All(f => Documents.Contains(Ext(f)))) return one ? "document" : null;
        return null;
    }
}

/// <summary>
/// The GitHub version is free and open source; the Premium tools come with the Microsoft Store version. With
/// Settings › General › Show Premium tools on (the default), they still show on the wheel in their usual places, with a
/// crown, so people know they exist. Choosing one opens orbitdrop.essam.biz/upgrade on that tool, which explains how to
/// get it. Nothing here does the tools' work: only their names, icons and places are in this repository.
/// </summary>
public sealed class PremiumPreviewProvider(Func<bool> show) : IOperationProvider
{
    public const string ProviderId = "premium.preview";
    public string Id => ProviderId;

    public IEnumerable<OperationDescriptor> GetOperations(FileSet files, OperationMode mode)
    {
        if (files.IsEmpty || !show() || PremiumCatalog.KindOf(files) is not { } kind) yield break;
        foreach (var e in PremiumCatalog.Entries.Where(e => e.Mode == mode && e.Kinds.Contains(kind)))
            yield return new(e.Id, ProviderId, mode, e.Label, e.Glyph, "%1", e.Label, e.Label, e.Order, Group: e.Group, RequiresLicense: true, MirrorGlyph: e.MirrorGlyph);
    }

    // The registry shows these locked (the app opens the upgrade page instead), so they're never run.
    public Task ExecuteAsync(OperationDescriptor operation, FileSet files, OperationContext context, IProgress<FileOutcome> progress, CancellationToken ct)
    {
        foreach (var f in files) progress.Report(new FileOutcome(f, OutcomeStatus.Failed, Message: "This is part of the Microsoft Store version of OrbitDrop."));
        return Task.CompletedTask;
    }

    public IEnumerable<(string Category, string Detail)> DescribeCapabilities()
    {
        yield return ("Premium (Store version)", "Remove background, upscale, stickers, social sizes and more, shown with a crown");
    }
}
