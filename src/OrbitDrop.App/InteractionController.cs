using System.Diagnostics;
using System.Windows.Threading;
using OrbitDrop.Core;
using OrbitDrop.Infrastructure;
using OrbitDrop.Jobs;
using OrbitDrop.UI.Toasts;
using OrbitDrop.UI.Wheel;
using OrbitDrop.Windows.DragDrop;
using OrbitDrop.Windows.Interop;
using OrbitDrop.Windows.Shell;

namespace OrbitDrop.App;

/// <summary>
/// The signature interaction, ported from Orbit Probe's validated state machine:
/// drag armed → trigger tap → wheel → OLE DragEnter (files known before the drop) → hover → drop → job → toast.
/// Changes from the probe are only those the feasibility log required (R4, R7, R8, R9).
/// </summary>
public sealed class InteractionController : IDropSink, IDisposable
{
    private sealed class Session
    {
        public FileSet Files = FileSet.Empty;
        public OperationMode Mode = OperationMode.Convert;
        /// <summary>Every option for the current mode; <see cref="Items"/> is what the wheel shows (top level or one group).</summary>
        public IReadOnlyList<OperationDescriptor> Flat = [];
        public IReadOnlyList<OperationDescriptor> Items = [];
        public OperationDescriptor? OpenGroup;
        /// <summary>Opened from the keyboard on selected files: the pointer hovers and clicks instead of dragging.</summary>
        public bool ClickMode;
        public int LastHit = WheelGeometry.HitOutside;
        public bool EnterSeen, OverWheel, Dropped;
        /// <summary>Set when Convert had nothing for these files and the wheel opened in another mode.</summary>
        public string? UnsupportedNote;
        public Win32.POINT Anchor;
        public long OpenedAt;
    }

    private readonly RadialWheel _wheel;
    private readonly DragSessionDetector _detector;
    private readonly OperationRegistry _registry;
    private readonly JobQueue _jobs;
    private readonly ToastManager _toasts;
    private readonly Func<AppSettings> _settings;
    private readonly Func<OperationContext> _context;
    private readonly IShellIntegration _shell;
    private readonly NativeDropTarget _dropTarget;
    private Session? _session;
    private DispatcherTimer? _deferredDismiss;
    private DispatcherTimer? _previewTimer;
    private readonly DispatcherTimer _hover = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly DispatcherTimer _dwell = new() { Interval = TimeSpan.FromMilliseconds(DwellMs) };
    private readonly Func<OperationDescriptor, FileSet, Win32.POINT, Task<(bool Ok, object? Input)>> _askInput;

    /// <summary>How long the pointer rests on a group (or on the centre inside one) before the wheel switches.</summary>
    private const int DwellMs = 380;

    public InteractionController(RadialWheel wheel, DragSessionDetector detector, OperationRegistry registry, JobQueue jobs,
        ToastManager toasts, Func<AppSettings> settings, Func<OperationContext> context, IShellIntegration shell,
        Func<OperationDescriptor, FileSet, Win32.POINT, Task<(bool Ok, object? Input)>> askInput)
    {
        _askInput = askInput;
        _dwell.Tick += (_, _) => OnDwell();
        _hover.Tick += (_, _) => { if (_session is { ClickMode: true }) OnDragOver(ScreenInfo.CursorPosition); };
        _wheel = wheel;
        _detector = detector;
        _registry = registry;
        _jobs = jobs;
        _toasts = toasts;
        _settings = settings;
        _context = context;
        _shell = shell;

        _dropTarget = new NativeDropTarget(wheel.Handle, this);
        int hr = _dropTarget.Register();
        Log.Info($"Wheel drop target registered (hr=0x{hr:X8})");

        _detector.TriggerPressed += OnTrigger;
        _detector.EscapePressed += () => Dismiss(confirmed: false, "Esc");
        _detector.DragButtonReleased += OnDragButtonReleased;
    }

    private string TriggerName => _settings().TriggerKey == TriggerKey.Ctrl ? "Ctrl" : "Shift";

    // ------------------------------------------------------------------ session lifecycle

    private void OnTrigger()
    {
        if (_session is not null) { CycleMode(); return; }
        Open();
    }

    private void Open()
    {
        _previewTimer?.Stop();
        _deferredDismiss?.Stop();
        long t0 = Stopwatch.GetTimestamp();
        var cursor = ScreenInfo.CursorPosition;
        _session = new Session { Anchor = cursor, OpenedAt = t0 };
        _detector.SessionOpen = true;
        _wheel.SetContent(OperationMode.Convert, [], "Move here", "", animate: false);
        _wheel.Open(cursor, WheelGeometry.SizeFactor(_settings().WheelSize), animate: true);
        Log.Info($"Wheel opened in {Stopwatch.GetElapsedTime(t0).TotalMilliseconds:F1} ms");
    }

    /// <summary>Next mode that has options for these files (empty modes are skipped once the files are known).</summary>
    private void CycleMode()
    {
        if (_session is not { } s) return;
        var next = NextMode(s, s.Mode, includeCurrent: false);
        if (next == s.Mode && s.EnterSeen) return; // only one mode has options: nothing to switch to
        s.Mode = next;
        s.OpenGroup = null;
        Refresh(animate: true);
    }

    private OperationMode NextMode(Session s, OperationMode from, bool includeCurrent)
    {
        for (int i = includeCurrent ? 0 : 1; i <= 3; i++)
        {
            var m = (OperationMode)(((int)from + i) % 3);
            if (!s.EnterSeen || _registry.Query(s.Files, m).Count > 0) return m;
        }
        return includeCurrent ? from : (OperationMode)(((int)from + 1) % 3); // nothing anywhere
    }

    private void Refresh(bool animate)
    {
        if (_session is not { } s) return;
        s.Flat = _registry.Query(s.Files, s.Mode);
        s.Items = s.OpenGroup is { } g ? OperationMenu.Members(s.Flat, g.Group!.Id) : OperationMenu.TopLevel(s.Flat);
        s.LastHit = WheelGeometry.HitOutside;
        _dwell.Stop();
        var (value, detail) = Summary(s);
        _wheel.SetContent(s.Mode, s.Items, value, detail, animate);
    }

    private (string Value, string Detail) Summary(Session s)
    {
        if (!s.EnterSeen) return ("Move here", "");
        if (s.OpenGroup is { } g) return (g.Label, s.ClickMode ? "Click the centre to go back" : "Rest on the centre to go back");
        if (s.Files.IsEmpty) return ("No files", "Only files can be dropped here");
        if (s.Items.Count == 0) return ("Not supported", $"{KindSummary(s.Files)} isn't supported yet");
        if (s.Files.Count > 1) return ($"{s.Files.Count} files", s.UnsupportedNote ?? KindSummary(s.Files));
        string size;
        try { size = HumanSize(new FileInfo(s.Files[0]).Length); } catch { size = "1 file"; }
        return (size, s.UnsupportedNote ?? Path.GetFileName(s.Files[0]));
    }

    private void OnDragButtonReleased()
    {
        if (_session is not { } s) return;
        if (s.ClickMode) { OnClick(s); return; }
        // If the pointer is over the wheel, OLE's Drop may still be on its way: wait briefly for it.
        if (s.OverWheel && !s.Dropped)
        {
            _deferredDismiss?.Stop();
            _deferredDismiss = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _deferredDismiss.Tick += (_, _) => { _deferredDismiss?.Stop(); if (_session == s) Dismiss(false, "released, no drop"); };
            _deferredDismiss.Start();
        }
        else Dismiss(confirmed: false, "released");
    }

    private void Dismiss(bool confirmed, string reason)
    {
        _deferredDismiss?.Stop();
        _dwell.Stop();
        _hover.Stop();
        if (_session is null && !_wheel.IsVisible) return;
        _session = null;
        _detector.SessionOpen = false;
        _wheel.Dismiss(confirmed);
        Log.Info($"Wheel closed ({reason})");
    }

    // ------------------------------------------------------------------ OLE drop target

    public void OnDragEnter(string[] files, Win32.POINT pt)
    {
        if (_session is not { } s) return;
        s.OverWheel = true;
        if (!s.EnterSeen)
        {
            s.EnterSeen = true;
            s.Files = new FileSet(files);
            Log.Info($"DragEnter after {Stopwatch.GetElapsedTime(s.OpenedAt).TotalMilliseconds:F1} ms: {s.Files.Count} file(s)");
            // Never open on an empty ring: start in the first mode that has options and say why.
            var start = NextMode(s, OperationMode.Convert, includeCurrent: true);
            if (start != OperationMode.Convert && !s.Files.IsEmpty)
                s.UnsupportedNote = $"No conversions for {KindSummary(s.Files)} yet";
            s.Mode = start;
            Refresh(animate: false);
        }
        OnDragOver(pt);
    }

    public DragFeedback OnDragOver(Win32.POINT pt)
    {
        if (_session is not { } s) return new(false, null, null);
        int hit = _wheel.HitTest(pt);
        _wheel.SetHighlight(hit);
        if (hit != s.LastHit)
        {
            s.LastHit = hit;
            _dwell.Stop();
            // Resting on a group opens it; resting on the centre inside a group goes back.
            bool group = hit >= 0 && hit < s.Items.Count && s.Items[hit].IsGroupHeader;
            if (group || (hit == WheelGeometry.HitHub && s.OpenGroup is not null)) _dwell.Start();
        }
        if (hit >= 0 && hit < s.Items.Count)
        {
            var item = s.Items[hit];
            if (item.IsGroupHeader)
            {
                var first = OperationMenu.DefaultFor(s.Flat, item);
                _wheel.SetHub(item.Label, s.ClickMode ? "Click for options" : $"Hold for options · drop: {first.Label}");
                return new(true, first.DropText, first.DropInsert);
            }
            // Formats read "Convert to PNG"; tools and actions already say what they do, so show the file instead.
            string line = item.DropText == "%1" ? Summary(s).Detail : item.DropText.Replace("%1", item.DropInsert);
            _wheel.SetHub(item.Label, line);
            return new(true, item.DropText, item.DropInsert);
        }
        var (value, detail) = Summary(s);
        if (hit == WheelGeometry.HitHub && s.Items.Count > 0)
        {
            _wheel.SetHub(value, (s.OpenGroup is null, s.ClickMode) switch
            {
                (true, true) => "Click to close",
                (true, false) => "Release to cancel",
                (false, true) => "Click to go back",
                _ => "Going back…",
            });
            return new(false, "Cancel", null);
        }
        _wheel.SetHub(value, detail);
        return new(false, null, null);
    }

    /// <summary>The pointer rested long enough: open the group under it, or leave the open group from the centre.</summary>
    private void OnDwell()
    {
        _dwell.Stop();
        if (_session is not { } s) return;
        int hit = s.LastHit;
        if (hit >= 0 && hit < s.Items.Count && s.Items[hit].IsGroupHeader)
        {
            s.OpenGroup = s.Items[hit];
            Refresh(animate: true);
        }
        else if (hit == WheelGeometry.HitHub && s.OpenGroup is not null)
        {
            s.OpenGroup = null;
            Refresh(animate: true);
        }
        // The pointer hasn't moved, so OLE won't call DragOver: re-evaluate what is under it now.
        if (_session is { OverWheel: true }) OnDragOver(ScreenInfo.CursorPosition);
    }

    public void OnDragLeave()
    {
        _dwell.Stop();
        if (_session is not { } s) return;
        s.OverWheel = false;
        _wheel.SetHighlight(WheelGeometry.HitOutside);
        var (value, detail) = Summary(s);
        _wheel.SetHub(value, detail);
    }

    public void OnDrop(string[] files, Win32.POINT pt)
    {
        if (_session is not { } s) return;
        int hit = _wheel.IsVisible ? _wheel.HitTest(pt) : _wheel.Highlight;
        s.Dropped = true;
        var item = hit >= 0 && hit < s.Items.Count ? s.Items[hit] : null;
        if (item is { IsGroupHeader: true }) item = OperationMenu.DefaultFor(s.Flat, item);
        var set = files.Length > 0 ? new FileSet(files) : s.Files;
        Dismiss(confirmed: item is not null, item is null ? "dropped on centre" : "dropped");
        if (item is null || set.IsEmpty) return;
        if (item.NeedsInput) _ = RunWithInputAsync(item, set, s.Anchor);
        else Run(item, set, s.Anchor, null);
    }

    /// <summary>Asks for the option's choices first (a window or picker); runs only if the user confirms.</summary>
    private async Task RunWithInputAsync(OperationDescriptor item, FileSet set, Win32.POINT anchor)
    {
        try
        {
            var (ok, input) = await _askInput(item, set, anchor);
            if (ok) Run(item, set, anchor, input);
            else Log.Info($"{item.Id} canceled before running");
        }
        catch (Exception ex)
        {
            Log.Error($"Could not prepare {item.Id}", ex);
            _toasts.ShowMessage(ToastKind.Error, "That didn't work", ex.Message, anchor);
        }
    }

    private void Run(OperationDescriptor item, FileSet set, Win32.POINT anchor, object? input)
    {
        Log.Info($"Run {item.Id} on {set.Count} file(s)");
        var job = _jobs.Enqueue(item, set, _context() with { Input = input });
        _toasts.ShowJob(job, anchor, _shell.Reveal);
        job.Changed += j =>
        {
            if (!j.IsFinished) return;
            Log.Info($"Job {j.Id} {j.State}: {j.Succeeded} ok, {j.Skipped} skipped, {j.Failed} failed");
            foreach (var o in j.Outcomes.Where(o => o.Status == OutcomeStatus.Failed))
                Log.Warn($"  {Log.Path(o.Source)}: {o.Message}");
            var outputs = j.Outcomes.Where(o => o.Output is not null).Select(o => o.Output!).ToList();
            if (_settings().RevealWhenDone && outputs.Count > 0) _shell.Reveal(outputs);
        };
    }

    // ------------------------------------------------------------------ keyboard flow (selected files + shortcut)

    /// <summary>
    /// Opens the wheel at the pointer for files selected in File Explorer. The pointer hovers, a click picks;
    /// clicking a group opens it, clicking the centre goes back (or closes); Shift switches mode, Esc closes.
    /// </summary>
    public void OpenForSelection(FileSet files)
    {
        Dismiss(false, "reopen");
        var cursor = ScreenInfo.CursorPosition;
        var s = new Session { Files = files, EnterSeen = true, ClickMode = true, OverWheel = true, Anchor = cursor, OpenedAt = Stopwatch.GetTimestamp() };
        _session = s;
        s.Mode = NextMode(s, OperationMode.Convert, includeCurrent: true);
        if (s.Mode != OperationMode.Convert) s.UnsupportedNote = $"No conversions for {KindSummary(files)} yet";
        _detector.SessionOpen = true;
        _detector.EnsurePolling();
        Refresh(animate: false);
        _wheel.Open(cursor, WheelGeometry.SizeFactor(_settings().WheelSize), animate: true);
        _hover.Start();
        Log.Info($"Wheel opened from the keyboard for {files.Count} item(s)");
    }

    private void OnClick(Session s)
    {
        var at = ScreenInfo.CursorPosition;
        int hit = _wheel.HitTest(at);
        if (hit >= 0 && hit < s.Items.Count)
        {
            var item = s.Items[hit];
            if (item.IsGroupHeader)
            {
                s.OpenGroup = item;
                Refresh(animate: true);
                return;
            }
            Dismiss(confirmed: true, "clicked");
            if (item.NeedsInput) _ = RunWithInputAsync(item, s.Files, s.Anchor);
            else Run(item, s.Files, s.Anchor, null);
        }
        else if (hit == WheelGeometry.HitHub && s.OpenGroup is not null)
        {
            s.OpenGroup = null;
            Refresh(animate: true);
        }
        else Dismiss(confirmed: false, hit == WheelGeometry.HitHub ? "clicked centre" : "clicked outside");
    }

    // ------------------------------------------------------------------ preview (Settings → Appearance)

    /// <summary>Shows the wheel with sample options at the pointer for a few seconds (no drag involved; Esc closes).</summary>
    public void Preview()
    {
        Dismiss(false, "preview");
        var sample = new FileSet([Path.Combine(Path.GetTempPath(), "sample.jpg")]);
        var flat = _registry.Query(sample, OperationMode.Convert);
        var items = OperationMenu.TopLevel(flat);
        _session = new Session { Files = sample, Flat = flat, Items = items, EnterSeen = true, Anchor = ScreenInfo.CursorPosition };
        _detector.SessionOpen = true;
        _detector.EnsurePolling();
        _wheel.SetContent(OperationMode.Convert, items, "Preview", "Sample options", animate: false);
        _wheel.Open(_session.Anchor, WheelGeometry.SizeFactor(_settings().WheelSize), animate: true);
        if (items.Count > 0) { _wheel.SetHighlight(0); _wheel.SetHub(items[0].Label, items[0].DropText.Replace("%1", items[0].DropInsert)); }
        _previewTimer?.Stop();
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _previewTimer.Tick += (_, _) => { _previewTimer?.Stop(); Dismiss(false, "preview ended"); };
        _previewTimer.Start();
    }

    // ------------------------------------------------------------------ helpers

    private static string KindSummary(FileSet files)
    {
        var exts = files.Extensions.Select(e => e.TrimStart('.').ToUpperInvariant()).ToList();
        return exts.Count <= 3 ? string.Join(" · ", exts) : $"{exts.Count} file types";
    }

    private static string HumanSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:F1} GB",
    };

    public void Dispose()
    {
        _dropTarget.Revoke();
        _deferredDismiss?.Stop();
        _previewTimer?.Stop();
    }
}
