namespace OrbitDrop.Core;

/// <summary>Single source of truth for what the wheel may offer. The UI never hard-codes options.</summary>
public sealed class OperationRegistry
{
    private readonly Dictionary<string, IOperationProvider> _providers;
    private readonly Func<bool> _licensed, _showLocked;
    private readonly Func<string, int> _freeTries;

    /// <summary>Ids of the locked copies of paid options (shown with a crown); choosing one opens the upgrade.</summary>
    public const string LockedPrefix = "locked.";

    /// <summary>How many times each paid option can be used for free before it locks.</summary>
    public const int FreeTries = 3;

    /// <summary>A free try works on at most this many files at once (more than that, and the option shows as locked).</summary>
    public const int FreeTryMaxFiles = 5;

    public static bool IsLocked(OperationDescriptor op) => op.Id.StartsWith(LockedPrefix, StringComparison.Ordinal);

    /// <param name="licensed">Whether this copy is unlocked; options that need a license are left out until it is.</param>
    /// <param name="showLocked">
    /// While locked, show the paid options anyway, in their usual places, as <see cref="LockedPrefix"/> options that ask
    /// for input, so the app can open the upgrade instead of running them. A group that's all paid ("Upscale": 2×, 4×)
    /// becomes one locked slice; a paid option in a group with free ones (All details in Metadata) stays in that group.
    /// </param>
    /// <param name="freeTries">
    /// Free tries left for a paid option (by id) while locked. An option with tries left is offered for real, on up to
    /// <see cref="FreeTryMaxFiles"/> files, and says so when dropping ("free try, 2 left"); with none left it's locked.
    /// </param>
    public OperationRegistry(IEnumerable<IOperationProvider> providers, Func<bool>? licensed = null, Func<bool>? showLocked = null,
        Func<string, int>? freeTries = null)
    {
        _providers = providers.ToDictionary(p => p.Id, StringComparer.Ordinal);
        _licensed = licensed ?? (() => false);
        _showLocked = showLocked ?? (() => false);
        _freeTries = freeTries ?? (_ => 0);
    }

    public IReadOnlyCollection<IOperationProvider> Providers => _providers.Values;

    /// <summary>All options valid for every file in the set, in display order.</summary>
    public IReadOnlyList<OperationDescriptor> Query(FileSet files, OperationMode mode)
    {
        if (files.IsEmpty) return [];
        var all = _providers.Values.SelectMany(p => p.GetOperations(files, mode)).Where(d => d.Mode == mode).ToList();
        bool licensed = _licensed();
        int TriesLeft(OperationDescriptor d) => !licensed && d.RequiresLicense && files.Count <= FreeTryMaxFiles ? Math.Max(0, _freeTries(d.Id)) : 0;
        bool Locked(OperationDescriptor d) => d.RequiresLicense && !licensed && TriesLeft(d) == 0;
        var result = all.Where(d => !Locked(d))
            .Select(d => TriesLeft(d) is var n and > 0 ? d with { DropText = $"{d.DropText} · free try, {n} left" } : d).ToList();
        if (!licensed && _showLocked())
        {
            // A group shows as one locked slice only when every option in it is paid and none has a free try left.
            var paidGroups = all.Where(d => d.Group is not null).GroupBy(d => d.Group!.Id)
                .Where(g => g.All(Locked)).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in all.Where(Locked).OrderBy(d => d.Order))
            {
                bool whole = d.Group is { } g && paidGroups.Contains(g.Id);
                if (whole && !seen.Add(d.Group!.Id)) continue;
                string label = whole ? d.Group!.Label : d.Label;
                result.Add(d with
                {
                    Id = LockedPrefix + d.Id, Label = label,
                    Glyph = whole ? d.Group!.Glyph : d.Glyph, MirrorGlyph = whole ? d.Group!.MirrorGlyph : d.MirrorGlyph,
                    Group = whole ? null : d.Group, NeedsInput = true, RequiresLicense = false,
                    DropText = "%1 · part of the upgrade", DropInsert = label, DoneTitle = null,
                });
            }
        }
        return result.OrderBy(d => d.Order).ThenBy(d => d.Label, StringComparer.CurrentCulture).ToList();
    }

    public IOperationProvider ProviderFor(OperationDescriptor descriptor) =>
        _providers.TryGetValue(descriptor.ProviderId, out var p)
            ? p
            : throw new KeyNotFoundException($"No provider '{descriptor.ProviderId}' for '{descriptor.Id}'");
}
