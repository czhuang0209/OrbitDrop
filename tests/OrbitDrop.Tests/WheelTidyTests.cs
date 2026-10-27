using OrbitDrop.Core;
using OrbitDrop.Infrastructure;
using OrbitDrop.Tools;

namespace OrbitDrop.Tests;

/// <summary>0.5.4's tidier wheel: one size per drag, and Actions in seven slices.</summary>
public class WheelTidyTests
{
    private static OperationDescriptor Op(string id, OperationGroup? group = null) =>
        new(id, "test", OperationMode.Actions, id, null, id, "", id, 0, Group: group);

    [Fact]
    public void The_busiest_view_counts_the_top_level_or_the_biggest_group()
    {
        var g = new OperationGroup("g", "G", null);
        Assert.Equal(3, OperationMenu.Busiest([Op("a"), Op("b"), Op("c", g), Op("d", g)])); // a · b · G
        Assert.Equal(4, OperationMenu.Busiest([Op("a"), Op("c", g), Op("d", g), Op("e", g), Op("f", g)])); // G opened: 4
    }

    [Fact]
    public void A_photo_s_actions_fit_in_seven_slices_with_share_and_open_grouped()
    {
        using var t = new TempFolder();
        string photo = t.Image("holiday & Mum's photo.jpg", 400, 300);
        var files = new FileSet([photo]);
        var flat = new IOperationProvider[] { new FileActionsProvider(), new PremiumPreviewProvider(() => true), new OrbitDrop.Tools.ImageTextProvider() }
            .SelectMany(p => p.GetOperations(files, OperationMode.Actions)).OrderBy(o => o.Order).ToList();
        var top = OperationMenu.TopLevel(flat).Select(o => o.Label).ToList();
        Assert.Equal(["Open", "Rename", "Copy to", "Move to", "Float", "Share"], top); // Rename: the Store version's, shown here as a preview
        Assert.Equal(["Show in folder", "Copy path", "Copy name", "Terminal"],
            OperationMenu.Members(flat, FileActionsProvider.OpenGroup.Id).Select(o => o.Label));
        Assert.Equal(["Share", "Search Google", "Search Bing", "Search TinEye", "Wallpaper"],
            OperationMenu.Members(flat, FileActionsProvider.ShareGroup.Id).Select(o => o.Label));
    }

    [Fact]
    public void A_label_must_stay_between_its_slice_s_sides()
    {
        // 13 slices (a photo's Tools page) are under 28° each: a box much wider than "Watermark" runs into the next one.
        double step = 360.0 / 13, r = OrbitDrop.UI.Wheel.WheelGeometry.LabelR;
        Assert.True(OrbitDrop.UI.Wheel.WheelGeometry.LabelFits(0, -r, 70, 14)); // the ring alone would allow it
        Assert.False(OrbitDrop.UI.Wheel.WheelGeometry.LabelFits(0, -r, 70, 14, -90, step));
        Assert.True(OrbitDrop.UI.Wheel.WheelGeometry.LabelFits(0, -r, 30, 14, -90, step));
        Assert.True(OrbitDrop.UI.Wheel.WheelGeometry.LabelFits(0, -r, 70, 14, -90, 180)); // two slices: only the ring limits it
    }

    [Fact]
    public void Hidden_old_groups_stay_hidden_as_their_options()
    {
        var s = new AppSettings { HiddenOptions = ["group:actions.copy", "group:image.search", "convert.image.bmp"] };
        s.Normalize();
        Assert.Equal(["action.copy-path", "action.copy-name", "action.image-search.google", "action.image-search.bing",
            "action.image-search.tineye", "convert.image.bmp"], s.HiddenOptions);
    }
}
