using OrbitDrop.Conversion;
using OrbitDrop.Conversion.Documents;
using OrbitDrop.Conversion.Pdf;
using OrbitDrop.Core;

namespace OrbitDrop.Tests;

/// <summary>Runs only where Microsoft Office is installed (not on CI): the conversions go through the real Office apps.</summary>
public sealed class OfficeFactAttribute : FactAttribute
{
    public OfficeFactAttribute(string progId)
    {
        if (Type.GetTypeFromProgID(progId) is null) Skip = $"{progId} isn't installed";
    }
}

public class DocumentTests
{
    private static async Task<FileOutcome> Run(string id, string file)
    {
        var provider = new DocumentProvider();
        var op = provider.GetOperations(new FileSet([file]), OperationMode.Convert).Single(d => d.Id == id);
        var c = new Collector();
        await provider.ExecuteAsync(op, new FileSet([file]), Ctx.Default(), c, CancellationToken.None);
        return Assert.Single(c.Outcomes);
    }

    [Fact]
    public void Only_documents_get_document_options()
    {
        Assert.Empty(new DocumentProvider().GetOperations(new FileSet([@"C:\x\photo.jpg"]), OperationMode.Convert));
        Assert.Empty(new DocumentProvider().GetOperations(new FileSet([@"C:\x\report.docx"]), OperationMode.Tools));
        Assert.Equal(DocumentKind.Drawing, DocumentEngines.KindOf("logo.CDR"));
    }

    [OfficeFact("Word.Application")]
    public async Task Rtf_becomes_a_pdf_and_a_docx_through_word()
    {
        using var t = new TempFolder();
        string src = t.File("letter.rtf");
        File.WriteAllText(src, @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\f0\fs28 Hello from OrbitDrop.\par}");
        string hash = SafeOutput.Sha256(src);

        var pdf = await Run("convert.document.pdf", src);
        Assert.Equal(OutcomeStatus.Succeeded, pdf.Status);
        Assert.Equal(t.File("letter.pdf"), pdf.Output);
        Assert.Null(PdfProvider.ValidatePdf(pdf.Output!));

        var docx = await Run("convert.document.docx", src);
        Assert.Equal(t.File("letter.docx"), docx.Output);
        using (var zip = System.IO.Compression.ZipFile.OpenRead(docx.Output!)) Assert.NotNull(zip.GetEntry("word/document.xml"));
        Assert.Equal(hash, SafeOutput.Sha256(src));
        Assert.Empty(SafeOutput.LeftoverTemps(t.Path));
    }

    [OfficeFact("Excel.Application")]
    public async Task Xlsx_becomes_a_pdf_through_excel()
    {
        using var t = new TempFolder();
        string src = t.File("sheet.xlsx");
        await StaWorker.RunAsync(() =>
        {
            dynamic xl = Activator.CreateInstance(Type.GetTypeFromProgID("Excel.Application")!)!;
            xl.DisplayAlerts = false;
            dynamic book = xl.Workbooks.Add();
            book.Worksheets[1].Range("A1").Value = "OrbitDrop";
            book.SaveAs(src, 51);
            book.Close(false);
            xl.Quit();
        });
        var pdf = await Run("convert.document.pdf", src);
        Assert.Equal(OutcomeStatus.Succeeded, pdf.Status);
        Assert.Null(PdfProvider.ValidatePdf(pdf.Output!));
    }

    [OfficeFact("PowerPoint.Application")]
    public async Task Pptx_becomes_a_pdf_through_powerpoint()
    {
        using var t = new TempFolder();
        string src = t.File("deck.pptx");
        await StaWorker.RunAsync(() =>
        {
            dynamic pp = Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application")!)!;
            dynamic deck = pp.Presentations.Add(0); // no window
            deck.Slides.Add(1, 1).Shapes[1].TextFrame.TextRange.Text = "OrbitDrop";
            deck.SaveAs(src, 24);
            deck.Close();
            if (pp.Presentations.Count == 0) pp.Quit();
        });
        var pdf = await Run("convert.document.pdf", src);
        Assert.Equal(OutcomeStatus.Succeeded, pdf.Status);
        Assert.Null(PdfProvider.ValidatePdf(pdf.Output!));
    }
}

/// <summary>A real CorelDRAW conversion, when Inkscape or LibreOffice and a sample drawing (ORBITDROP_CDR_SAMPLE) are on this PC.</summary>
public class DrawingTests
{
    private static string? Sample => Environment.GetEnvironmentVariable("ORBITDROP_CDR_SAMPLE") is { } p && File.Exists(p) ? p : null;

    [Fact]
    public void Cdr_offers_every_drawing_format_when_an_app_can_open_it()
    {
        if (!DocumentEngines.CanOpen(DocumentKind.Drawing)) return;
        var labels = new DocumentProvider().GetOperations(new FileSet([@"C:\x\logo.cdr"]), OperationMode.Convert).Select(o => o.Label);
        Assert.Equal(["PDF", "SVG", "PNG", "JPG", "TIFF", "BMP", "EPS"], labels);
    }

    [Theory]
    [InlineData("convert.document.pdf", ".pdf")]
    [InlineData("convert.drawing.svg", ".svg")]
    [InlineData("convert.drawing.eps", ".eps")]
    [InlineData("convert.drawing.png", ".png")]
    [InlineData("convert.drawing.jpg", ".jpg")]
    public async Task Cdr_converts(string id, string ext)
    {
        if (Sample is not { } sample || !DocumentEngines.CanOpen(DocumentKind.Drawing)) return;
        using var t = new TempFolder();
        string src = t.File("drawing.cdr");
        File.Copy(sample, src);
        var provider = new DocumentProvider();
        var op = provider.GetOperations(new FileSet([src]), OperationMode.Convert).Single(d => d.Id == id);
        var c = new Collector();
        await provider.ExecuteAsync(op, new FileSet([src]), Ctx.Default(), c, CancellationToken.None);
        var outcome = Assert.Single(c.Outcomes);
        Assert.True(outcome.Status == OutcomeStatus.Succeeded, outcome.Message);
        string output = outcome.Output!;
        // One page is a file; several are a folder of pages.
        if (Directory.Exists(output)) output = Directory.GetFiles(output)[0];
        Assert.EndsWith(ext, output);
        Assert.True(new FileInfo(output).Length > 100);
    }
}
