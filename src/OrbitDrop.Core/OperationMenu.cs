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

/// <summary>
/// The user's wheel customization: options (by id) and whole groups (by <see cref="GroupKey"/>) they turned off.
/// Options whose id carries a parameter (e.g. a folder in "action.copy-to|C:\…") can only be hidden via their group.
/// </summary>
public static class WheelFilter
{
    public static string GroupKey(OperationGroup group) => "group:" + group.Id;

    /// <summary>The key a single option is hidden by, or null when it can only be hidden with its group.</summary>
    public static string? OptionKey(OperationDescriptor op) => op.Id.Contains('|') ? null : op.Id;

    public static bool IsHidden(OperationDescriptor op, IReadOnlySet<string> hidden) =>
        (op.Group is { } g && hidden.Contains(GroupKey(g))) || (OptionKey(op) is { } k && hidden.Contains(k));

    public static IReadOnlyList<OperationDescriptor> Apply(IReadOnlyList<OperationDescriptor> options, IReadOnlySet<string> hidden) =>
        hidden.Count == 0 ? options : options.Where(o => !IsHidden(o, hidden)).ToList();
}
