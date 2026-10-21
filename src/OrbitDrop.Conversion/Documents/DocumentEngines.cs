using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OrbitDrop.Conversion.Documents;

public enum DocumentKind { Text, Sheet, Slides, Drawing }

/// <summary>
/// The apps that do the actual conversion, found on this PC: Microsoft Word, Excel and PowerPoint (driven through COM,
/// invisibly, files opened read-only), LibreOffice (headless, with its own private profile) and Inkscape (for drawings).
/// Office is preferred for its own formats; Inkscape for drawings. Results go to a private temp folder that is deleted
/// on <see cref="Dispose"/>; the provider copies them to their place with SafeOutput.
/// </summary>
public sealed class DocumentEngines : IDisposable
{
    private static readonly Dictionary<string, DocumentKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        [".doc"] = DocumentKind.Text, [".docx"] = DocumentKind.Text, [".docm"] = DocumentKind.Text, [".rtf"] = DocumentKind.Text,
        [".odt"] = DocumentKind.Text,
        [".xls"] = DocumentKind.Sheet, [".xlsx"] = DocumentKind.Sheet, [".xlsm"] = DocumentKind.Sheet, [".ods"] = DocumentKind.Sheet,
        [".ppt"] = DocumentKind.Slides, [".pptx"] = DocumentKind.Slides, [".pptm"] = DocumentKind.Slides, [".pps"] = DocumentKind.Slides,
        [".ppsx"] = DocumentKind.Slides, [".odp"] = DocumentKind.Slides,
        [".cdr"] = DocumentKind.Drawing,
    };

    /// <summary>Some password is needed so Office fails instead of asking; a file without one ignores it.</summary>
    private const string NoPassword = "orbitdrop-no-password";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private static readonly Lazy<bool> Word = new(() => HasProgId("Word.Application")), Excel = new(() => HasProgId("Excel.Application")),
        PowerPoint = new(() => HasProgId("PowerPoint.Application"));
    private static readonly Lazy<string?> LibreOffice = new(() => FindApp("soffice.exe", @"LibreOffice\program\soffice.exe")),
        Inkscape = new(() => FindApp("inkscape.exe", @"Inkscape\bin\inkscape.exe"));

    public static DocumentKind? KindOf(string path) => Kinds.TryGetValue(Path.GetExtension(path), out var k) ? k : null;

    public static bool CanOpen(DocumentKind kind) => kind switch
    {
        DocumentKind.Text => Word.Value || LibreOffice.Value is not null,
        DocumentKind.Sheet => Excel.Value || LibreOffice.Value is not null,
        DocumentKind.Slides => PowerPoint.Value || LibreOffice.Value is not null,
        _ => Inkscape.Value is not null || LibreOffice.Value is not null,
    };

    /// <summary>The apps found, for Settings → About's capability list.</summary>
    public static string[] Describe() => new (bool Found, string Name)[]
    {
        (Word.Value, "Word"), (Excel.Value, "Excel"), (PowerPoint.Value, "PowerPoint"),
        (LibreOffice.Value is not null, "LibreOffice"), (Inkscape.Value is not null, "Inkscape"),
    }.Where(a => a.Found).Select(a => a.Name).ToArray();

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "OrbitDrop", "documents", Guid.NewGuid().ToString("N"));

    /// <summary>Converts <paramref name="file"/> to <paramref name="extension"/> in a private temp folder; returns the new file.</summary>
    public async Task<string> ConvertToTempAsync(string file, string extension, CancellationToken ct)
    {
        var kind = KindOf(file) ?? throw new NotSupportedException(Path.GetExtension(file));
        if (!File.Exists(file)) throw new FileNotFoundException(null, file);
        string dir = Path.Combine(_temp, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string output = Path.Combine(dir, Path.GetFileNameWithoutExtension(file) + extension);

        string? office = kind switch
        {
            DocumentKind.Text when Word.Value => "Word.Application",
            DocumentKind.Sheet when Excel.Value => "Excel.Application",
            DocumentKind.Slides when PowerPoint.Value => "PowerPoint.Application",
            _ => null,
        };
        if (office is not null)
            await StaWorker.RunAsync(() => ConvertWithOffice(office, kind, file, output), "OrbitDrop Office");
        else if (kind == DocumentKind.Drawing && Inkscape.Value is { } inkscape)
            await RunAsync(inkscape, [file, "--export-filename=" + output], ct);
        else if (LibreOffice.Value is { } soffice)
        {
            // A profile of its own: works while the user has LibreOffice open, and never touches their settings.
            string profile = new Uri(Path.Combine(Path.GetTempPath(), "OrbitDrop", "libreoffice-profile")).AbsoluteUri;
            await RunAsync(soffice, ["--headless", "--norestore", "--nologo", "--nodefault", "--nolockcheck", $"-env:UserInstallation={profile}",
                "--convert-to", LibreOfficeFilter(extension), "--outdir", dir, file], ct);
        }
        else throw new NotSupportedException("No app on this PC can open this file.");

        if (!File.Exists(output) || new FileInfo(output).Length == 0) throw new IOException("The conversion didn't produce a file.");
        return output;
    }

    private static string LibreOfficeFilter(string extension) => extension switch
    {
        ".docx" => "docx:MS Word 2007 XML",
        ".xlsx" => "xlsx:Calc MS Excel 2007 XML",
        ".pptx" => "pptx:Impress MS PowerPoint 2007 XML",
        _ => extension.TrimStart('.'),
    };

    // ------------------------------------------------------------------ Microsoft Office (late-bound COM, on an STA thread)

    private static void ConvertWithOffice(string progId, DocumentKind kind, string file, string output)
    {
        bool pdf = output.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        // PowerPoint runs as one shared instance: if the user has it open, leave it running afterwards.
        bool wasRunning = kind == DocumentKind.Slides && Process.GetProcessesByName("POWERPNT").Length > 0;
        dynamic app = Activator.CreateInstance(Type.GetTypeFromProgID(progId)!)!;
        try
        {
            switch (kind)
            {
                case DocumentKind.Text:
                    app.DisplayAlerts = 0; // wdAlertsNone
                    dynamic doc = app.Documents.Open(file, false, true, false, NoPassword); // ConfirmConversions, ReadOnly, AddToRecentFiles
                    try
                    {
                        if (pdf) doc.ExportAsFixedFormat(output, 17); // wdExportFormatPDF
                        else doc.SaveAs2(output, 16);                   // wdFormatDocumentDefault (.docx)
                    }
                    finally { doc.Close(0); Release(doc); }
                    break;

                case DocumentKind.Sheet:
                    app.DisplayAlerts = false;
                    dynamic book = app.Workbooks.Open(file, 0, true, Missing.Value, NoPassword); // UpdateLinks: no, ReadOnly
                    try
                    {
                        if (pdf) book.ExportAsFixedFormat(0, output); // xlTypePDF
                        else book.SaveAs(output, 51);                  // xlOpenXMLWorkbook (.xlsx)
                    }
                    finally { book.Close(false); Release(book); }
                    break;

                case DocumentKind.Slides:
                    dynamic deck = app.Presentations.Open(file, -1, 0, 0); // ReadOnly, not Untitled, no window
                    try { deck.SaveAs(output, pdf ? 32 : 24); }            // ppSaveAsPDF / ppSaveAsOpenXMLPresentation
                    finally { deck.Close(); Release(deck); }
                    break;
            }
        }
        finally
        {
            try
            {
                if (kind == DocumentKind.Text) app.Quit(0);
                else if (kind == DocumentKind.Sheet) app.Quit();
                else if (!wasRunning && app.Presentations.Count == 0) app.Quit();
            }
            catch (COMException) { /* already gone */ }
            Release(app);
        }
    }

    private static void Release(object o)
    {
        try { Marshal.FinalReleaseComObject(o); } catch (ArgumentException) { }
    }

    private static bool HasProgId(string progId)
    {
        try { return Type.GetTypeFromProgID(progId) is not null; }
        catch { return false; }
    }

    // ------------------------------------------------------------------ LibreOffice and Inkscape (command line)

    /// <summary>Registered under App Paths, or in Program Files. Prefers the console launcher (.com), which waits for the result.</summary>
    private static string? FindApp(string exe, string programFilesPath)
    {
        var candidates = new List<string?>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                candidates.Add(key?.GetValue(null) as string);
            }
            catch { /* no access */ }
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), programFilesPath));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), programFilesPath));
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            string path = c.Trim('"');
            string console = Path.ChangeExtension(path, ".com");
            if (File.Exists(console)) return console;
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static async Task RunAsync(string exe, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo(exe) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) start.ArgumentList.Add(a);
        using var process = Process.Start(start) ?? throw new IOException($"{Path.GetFileName(exe)} didn't start.");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(Timeout);
        try { await process.WaitForExitAsync(limit.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            if (ct.IsCancellationRequested) throw;
            throw new TimeoutException();
        }
        if (process.ExitCode != 0)
        {
            string detail = (await stderr).Trim() is { Length: > 0 } e ? e : (await stdout).Trim();
            throw new IOException($"{Path.GetFileNameWithoutExtension(exe)} couldn't convert it{(detail.Length > 0 ? ": " + detail.Split('\n')[0] : ".")}");
        }
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true); } catch { /* best effort */ }
    }
}
