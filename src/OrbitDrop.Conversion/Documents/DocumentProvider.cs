using OrbitDrop.Conversion.Imaging;
using OrbitDrop.Conversion.Pdf;
using OrbitDrop.Core;

namespace OrbitDrop.Conversion.Documents;

/// <summary>
/// Documents (Word, Excel, PowerPoint, OpenDocument) to PDF, and old formats to the current ones; CorelDRAW drawings to
/// PDF, SVG, EPS and images. Nothing is bundled: the conversion runs in an app already on this PC (Microsoft Office,
/// LibreOffice or Inkscape, see <see cref="DocumentEngines"/>), and options only appear when one of them can open the file.
/// </summary>
public sealed class DocumentProvider : IOperationProvider
{
    public const string ProviderId = "document";
    public const string GetInkscapeId = "convert.drawing.get-inkscape";
    public string Id => ProviderId;

    private sealed record Target(string Name, string Extension);

    private static readonly Target Pdf = new("PDF", ".pdf"), Docx = new("DOCX", ".docx"), Xlsx = new("XLSX", ".xlsx"),
        Pptx = new("PPTX", ".pptx"), Svg = new("SVG", ".svg"), Eps = new("EPS", ".eps"), Png = new("PNG", ".png"),
        Jpg = new("JPG", ".jpg"), Tiff = new("TIFF", ".tiff"), Bmp = new("BMP", ".bmp");

    /// <summary>Wheel order of every target, across kinds.</summary>
    private static readonly Target[] All = [Pdf, Docx, Xlsx, Pptx, Svg, Png, Jpg, Tiff, Bmp, Eps];

    /// <summary>Drawing targets made by rendering the drawing's PDF (sharp at any size, unlike the apps' own bitmap export).</summary>
    private static readonly Dictionary<Target, ImageFormat> Rendered = new()
    {
        [Png] = WicImageCodec.Png, [Jpg] = WicImageCodec.Jpg, [Tiff] = WicImageCodec.Tiff, [Bmp] = WicImageCodec.Bmp,
    };

    private static IEnumerable<Target> TargetsFor(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (DocumentEngines.KindOf(path) is not { } kind || !DocumentEngines.CanOpen(kind)) return [];
        return kind switch
        {
            DocumentKind.Text => ext == ".docx" ? [Pdf] : [Pdf, Docx],
            DocumentKind.Sheet => ext == ".xlsx" ? [Pdf] : [Pdf, Xlsx],
            DocumentKind.Slides => ext == ".pptx" ? [Pdf] : [Pdf, Pptx],
            DocumentKind.Drawing => [Pdf, Svg, Png, Jpg, Tiff, Bmp, Eps],
            DocumentKind.Pdf => [Docx],
            _ => [],
        };
    }

    public IEnumerable<OperationDescriptor> GetOperations(FileSet files, OperationMode mode)
    {
        if (mode != OperationMode.Convert || files.IsEmpty || !files.All(f => DocumentEngines.KindOf(f) is not null)) yield break;
        // CorelDRAW files on a PC with nothing that can open them: offer the free app that can, instead of nothing.
        if (files.All(f => DocumentEngines.KindOf(f) == DocumentKind.Drawing) && !DocumentEngines.CanOpen(DocumentKind.Drawing))
        {
            yield return new OperationDescriptor(GetInkscapeId, ProviderId, OperationMode.Convert, "Get Inkscape", "",
                "Free · opens CorelDRAW files", "", "Opening the Microsoft Store", 1, DoneTitle: "Opening the Microsoft Store");
            yield break;
        }
        // Only what every file can become (a mixed selection usually leaves just PDF).
        var common = files.Select(f => TargetsFor(f).ToHashSet()).Aggregate((a, b) => { a.IntersectWith(b); return a; });
        int order = 0;
        foreach (var t in All)
        {
            order++;
            if (!common.Contains(t)) continue;
            string id = Rendered.ContainsKey(t) || t == Svg || t == Eps ? $"convert.drawing.{t.Name.ToLowerInvariant()}" : $"convert.document.{t.Name.ToLowerInvariant()}";
            yield return new OperationDescriptor(id, ProviderId, OperationMode.Convert, t.Name, null, "Convert to %1", t.Name,
                $"Converting to {t.Name}", order);
        }
    }

    public async Task ExecuteAsync(OperationDescriptor operation, FileSet files, OperationContext context,
        IProgress<FileOutcome> progress, CancellationToken ct)
    {
        if (operation.Id == GetInkscapeId)
        {
            // Inkscape's own page in the Microsoft Store (free). Once it's installed, OrbitDrop finds it within seconds.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DocumentEngines.InkscapeStoreLink) { UseShellExecute = true });
            foreach (var file in files) progress.Report(new FileOutcome(file, OutcomeStatus.Succeeded));
            return;
        }
        var target = All.Single(t => operation.Id.EndsWith("." + t.Name.ToLowerInvariant(), StringComparison.Ordinal));
        using var engines = new DocumentEngines();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                string output;
                if (Rendered.TryGetValue(target, out var image))
                {
                    // Drawing → PDF in a private temp folder → each page rendered by Windows' PDF engine.
                    string pdf = await engines.ConvertToTempAsync(file, ".pdf", ct);
                    output = await PdfProvider.RenderPagesAsync(pdf, file, image, context, ct);
                }
                else
                {
                    string temp = await engines.ConvertToTempAsync(file, target.Extension, ct);
                    output = SafeOutput.Write(file, context.Output, target.Extension, null,
                        s => { using var src = File.OpenRead(temp); src.CopyTo(s); },
                        check => target == Pdf ? PdfProvider.ValidatePdf(check) : null, ct);
                }
                progress.Report(new FileOutcome(file, OutcomeStatus.Succeeded, output));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress.Report(new FileOutcome(file, OutcomeStatus.Failed, Message: Friendly(ex)));
            }
        }
    }

    public IEnumerable<(string Category, string Detail)> DescribeCapabilities()
    {
        var apps = DocumentEngines.Describe();
        if (apps.Length == 0)
            yield return ("Documents", "Word, Excel, PowerPoint and CorelDRAW files convert when Microsoft Office, LibreOffice or Inkscape is installed");
        else
            yield return ("Documents", $"Documents to PDF (and DOCX, XLSX, PPTX) · PDF to Word{(DocumentEngines.CanOpen(DocumentKind.Drawing) ? " · CorelDRAW to PDF, SVG, EPS, PNG, JPG, TIFF, BMP" : "")} · using {string.Join(", ", apps)}");
    }

    private static string Friendly(Exception ex) => ex switch
    {
        FileNotFoundException => "The file no longer exists.",
        UnauthorizedAccessException => "Windows denied access (read-only folder or file in use).",
        TimeoutException => "The conversion took too long and was stopped.",
        System.Runtime.InteropServices.COMException com when com.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
            => "The file is password-protected.",
        System.Runtime.InteropServices.COMException com => $"Office couldn't convert this file: {com.Message.Trim()}",
        _ => ex.Message,
    };
}
