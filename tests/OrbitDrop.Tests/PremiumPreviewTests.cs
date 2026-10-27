using OrbitDrop.Core;
using OrbitDrop.Tools;

namespace OrbitDrop.Tests;

/// <summary>The GitHub version shows the Store version's Premium tools with a crown, and only their names.</summary>
public class PremiumPreviewTests
{
    private static OperationRegistry Registry(bool show) =>
        new([new PremiumPreviewProvider(() => show), new FileActionsProvider()], licensed: () => false, showLocked: () => show);

    [Fact]
    public void Selections_are_classified_into_the_kinds_the_catalog_uses()
    {
        Assert.Equal("photo", PremiumCatalog.KindOf(new FileSet([@"C:\x\Mum's photo & friends (1).jpg"])));
        Assert.Equal("photos", PremiumCatalog.KindOf(new FileSet([@"C:\x\a.jpg", @"C:\x\b.png"])));
        Assert.Equal("pdfs", PremiumCatalog.KindOf(new FileSet([@"C:\x\a.pdf", @"C:\x\b.PDF"])));
        Assert.Equal("video", PremiumCatalog.KindOf(new FileSet([@"C:\x\clip.mp4"])));
        Assert.Null(PremiumCatalog.KindOf(new FileSet([@"C:\x\a.jpg", @"C:\x\b.pdf"])));
    }

    [Fact]
    public void Premium_tools_show_with_a_crown_in_their_usual_places()
    {
        var files = new FileSet([@"C:\x\photo.jpg"]);
        var tools = OperationMenu.TopLevel(Registry(show: true).Query(files, OperationMode.Tools));
        var background = Assert.Single(tools, o => o.Label == "Background");
        Assert.True(OperationRegistry.IsLocked(background)); // an all-Premium group is one crowned slice
        Assert.Contains(tools, o => o.Label == "Upscale" && OperationRegistry.IsLocked(o));
        // In Actions, the image searches sit in Share, crowned, next to the free Share.
        var share = OperationMenu.Members(Registry(show: true).Query(files, OperationMode.Actions), FileActionsProvider.ShareGroup.Id);
        Assert.Contains(share, o => o.Label == "Share" && !OperationRegistry.IsLocked(o));
        Assert.Contains(share, o => o.Label == "Search Google" && OperationRegistry.IsLocked(o));
    }

    [Fact]
    public void Turning_Show_Premium_tools_off_hides_them_all()
    {
        var files = new FileSet([@"C:\x\photo.jpg"]);
        Assert.Empty(Registry(show: false).Query(files, OperationMode.Tools));
        Assert.DoesNotContain(Registry(show: false).Query(files, OperationMode.Actions), OperationRegistry.IsLocked);
    }

    [Fact]
    public void Every_entry_has_a_name_and_is_for_some_kind_of_file()
    {
        Assert.NotEmpty(PremiumCatalog.Entries);
        Assert.All(PremiumCatalog.Entries, e => { Assert.False(string.IsNullOrWhiteSpace(e.Label)); Assert.NotEmpty(e.Kinds); });
        Assert.Equal(PremiumCatalog.Entries.Length, PremiumCatalog.Entries.Select(e => e.Id).Distinct().Count());
    }
}
