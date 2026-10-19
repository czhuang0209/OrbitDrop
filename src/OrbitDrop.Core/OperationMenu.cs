namespace OrbitDrop.Core;

/// <summary>Turns a flat option list into what the wheel shows: grouped options collapse into one header segment.</summary>
public static class OperationMenu
{
    /// <summary>
    /// Top level: ungrouped options, plus one header per group in the position of its first option. A group with a
    /// single option shows that option directly.
    /// </summary>
    public static IReadOnlyList<OperationDescriptor> TopLevel(IReadOnlyList<OperationDescriptor> flat)
    {
        var result = new List<OperationDescriptor>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var op in flat)
        {
            if (op.Group is not { } g) { result.Add(op); continue; }
            if (!seen.Add(g.Id)) continue;
            var members = Members(flat, g.Id);
            result.Add(members.Count == 1 ? members[0] : Header(g, members[0]));
        }
        return result;
    }

    /// <summary>The options inside group <paramref name="groupId"/>, default first.</summary>
    public static IReadOnlyList<OperationDescriptor> Members(IReadOnlyList<OperationDescriptor> flat, string groupId) =>
        flat.Where(o => o.Group?.Id == groupId).ToList();

    /// <summary>The option a drop on a group header runs.</summary>
    public static OperationDescriptor DefaultFor(IReadOnlyList<OperationDescriptor> flat, OperationDescriptor header) =>
        Members(flat, header.Group!.Id)[0];

    private static OperationDescriptor Header(OperationGroup g, OperationDescriptor first) =>
        first with
        {
            Id = OperationDescriptor.GroupPrefix + g.Id,
            Label = g.Label,
            Glyph = g.Glyph,
            MirrorGlyph = g.MirrorGlyph,
            DropText = "%1",
            DropInsert = g.Label,
            NeedsInput = first.NeedsInput,
        };
}
