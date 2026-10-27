using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitDrop.Conversion.Pdf;
using OrbitDrop.Core;
using OrbitDrop.Tools;

namespace OrbitDrop.Tests;

/// <summary>Your actions (2 free, more with the upgrade) and Print.</summary>
public class CustomActionsTests
{
    private const string Tricky = @"C:\Users\Ana\Pictures\Mum's photo & friends (1).jpg";

    [Fact]
    public void Placeholders_are_filled_in_quoted_so_awkward_names_pass_whole()
    {
        var action = new CustomAction { Arguments = "--in {file} --all {files} --dir {folder} --name {name}.{ext}" };
        Assert.Equal("--in \"C:\\Users\\Ana\\Pictures\\Mum's photo & friends (1).jpg\" --all \"C:\\Users\\Ana\\Pictures\\Mum's photo & friends (1).jpg\" \"D:\\b.png\""
            + " --dir \"C:\\Users\\Ana\\Pictures\" --name Mum's photo & friends (1).jpg", action.Expand([Tricky, @"D:\b.png"]));
        Assert.Equal("\"C:\\\\\"", CustomAction.Quote(@"C:\")); // a trailing backslash can't escape the closing quote
    }

    [Fact]
    public void Actions_are_offered_for_their_kind_of_file()
    {
        Assert.True(new CustomAction { Kind = "Photos" }.Accepts(Tricky));
        Assert.False(new CustomAction { Kind = "Photos" }.Accepts(@"C:\a\clip.mp4"));
        Assert.True(new CustomAction { Kind = "PDFs" }.Accepts(@"C:\a\REPORT.PDF"));
        var chosen = new CustomAction { Kind = "Chosen types", Extensions = "psd, *.AI;.xd" };
        Assert.Equal([".ai", ".psd", ".xd"], chosen.ChosenExtensions().Order());
        Assert.True(chosen.Accepts(@"C:\a\logo.ai"));
        Assert.False(chosen.Accepts(@"C:\a\logo.png"));
        Assert.True(new CustomAction().Accepts(Path.GetTempPath())); // Everything includes folders
    }

    [Fact]
    public void The_first_two_actions_are_free_and_the_rest_need_the_upgrade()
    {
        var actions = Enumerable.Range(1, 4).Select(i => new CustomAction { Name = $"A{i}", Program = "notepad", Arguments = "{file}" }).ToList();
        actions.Add(new CustomAction { Name = "", Arguments = "{file}" }); // not ready (no name): left off the wheel
        var ops = new CustomActionsProvider(() => actions).GetOperations(new FileSet([Tricky]), OperationMode.Actions).ToList();
        Assert.Equal(["A1", "A2", "A3", "A4"], ops.Select(o => o.Label));
        Assert.Equal([false, false, true, true], ops.Select(o => o.RequiresLicense));
        Assert.All(ops, o => Assert.Equal(CustomActionsProvider.Group, o.Group));
        Assert.Empty(new CustomActionsProvider(() => actions).GetOperations(new FileSet([Tricky]), OperationMode.Tools));
    }

    [Fact]
    public void Programs_run_directly_and_commands_go_through_cmd()
    {
        string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var (file, args) = CustomActionsProvider.CommandLine(new CustomAction { Program = "cmd" }, "/c ver");
        Assert.Equal(cmd, file, ignoreCase: true); // an .exe on the PATH: no extra command prompt
        Assert.Equal("/c ver", args);
        (file, args) = CustomActionsProvider.CommandLine(new CustomAction { Program = "" }, "echo hi");
        Assert.Equal(cmd, file, ignoreCase: true);
        Assert.Equal("/d /s /c \"echo hi\"", args);
        (file, args) = CustomActionsProvider.CommandLine(new CustomAction { Program = "", ShowWindow = true }, "echo hi");
        Assert.Equal("/d /s /k \"echo hi\"", args); // the window stays open to read the output
        Assert.Null(CustomActionsProvider.Resolve("definitely-not-a-program-od"));
    }

    [Fact]
    public async Task A_command_really_runs_on_a_file_with_an_awkward_name()
    {
        using var t = new TempFolder();
        string photo = t.Image("Mum's photo & friends (1).jpg", 40, 30);
        var action = new CustomAction { Name = "Copy it", Program = "", Arguments = "copy /y {file} \"{name} (copy).{ext}\"" };
        var outcome = Assert.Single(await Runner.Run(new CustomActionsProvider(() => [action]), CustomActionsProvider.Prefix + "0", photo));
        Assert.True(outcome.Status == OutcomeStatus.Succeeded, outcome.Message);
        Assert.True(File.Exists(t.File("Mum's photo & friends (1) (copy).jpg")));
    }

    [Fact]
    public async Task A_program_that_isnt_there_says_so()
    {
        using var t = new TempFolder();
        string photo = t.Image("a.jpg", 40, 30);
        var action = new CustomAction { Name = "Broken", Program = "definitely-not-a-program-od", Arguments = "{file}" };
        var outcome = Assert.Single(await Runner.Run(new CustomActionsProvider(() => [action]), CustomActionsProvider.Prefix + "0", photo));
        Assert.Equal(OutcomeStatus.Failed, outcome.Status);
        Assert.Contains("Broken", outcome.Message);
    }

    [Fact]
    public void Print_is_offered_for_photos_and_PDFs_only()
    {
        var print = new PrintProvider();
        Assert.Equal("Print", Assert.Single(print.GetOperations(new FileSet([Tricky, @"C:\a\report.pdf"]), OperationMode.Actions)).Label);
        Assert.Empty(print.GetOperations(new FileSet([Tricky, @"C:\a\notes.txt"]), OperationMode.Actions));
        Assert.Empty(print.GetOperations(new FileSet([Tricky]), OperationMode.Convert));
    }

    [Fact]
    public async Task Print_draws_every_page_of_photos_and_PDFs_and_keeps_to_a_page_range()
    {
        using var t = new TempFolder();
        string photo = t.Image("Mum's photo & friends (1).jpg", 600, 300);
        var images = Enumerable.Range(1, 3).Select(i => t.Image($"p{i}.jpg", 300, 400)).ToArray();
        string made = (await Runner.Run(new PdfProvider(), "tool.image.pdf-combine", images)).First(o => o.Output is not null).Output!;
        var job = await PrintProvider.PrepareAsync([photo, made]);
        Assert.Empty(job.Failed);
        Assert.Equal(4, job.Pages.Count); // the photo, then the PDF's three pages

        double inkAll = 0, inkRange = 0;
        int rangeCount = 0;
        var thread = new Thread(() =>
        {
            var paper = new Size(816, 1056); // Letter at 96 dpi
            var all = job.Paginator(paper);
            for (int i = 0; i < all.PageCount; i++) inkAll += Ink(all.GetPage(i).Visual, paper);
            var range = job.Paginator(paper, 2, 3);
            rangeCount = range.PageCount;
            inkRange = Ink(range.GetPage(0).Visual, paper);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        Assert.True(inkAll > 4 * 0.05, $"pages drew too little ({inkAll:F2})"); // every page has a picture on it
        Assert.Equal(2, rangeCount);
        Assert.True(inkRange > 0.05);
    }

    /// <summary>The share of a page that isn't white, from a small render of it.</summary>
    private static double Ink(Visual page, Size paper)
    {
        var bmp = new RenderTargetBitmap((int)(paper.Width / 4), (int)(paper.Height / 4), 24, 24, PixelFormats.Pbgra32);
        var white = new DrawingVisual();
        using (var dc = white.RenderOpen()) dc.DrawRectangle(Brushes.White, null, new Rect(paper));
        bmp.Render(white);
        bmp.Render(page);
        var px = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];
        bmp.CopyPixels(px, bmp.PixelWidth * 4, 0);
        int inked = 0;
        for (int i = 0; i < px.Length; i += 4) if (px[i] < 240 || px[i + 1] < 240 || px[i + 2] < 240) inked++;
        return inked / (double)(bmp.PixelWidth * bmp.PixelHeight);
    }
}
