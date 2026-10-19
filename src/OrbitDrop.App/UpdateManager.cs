using System.Diagnostics;
using System.Windows.Threading;
using OrbitDrop.Infrastructure;
using OrbitDrop.Infrastructure.Updates;
using OrbitDrop.UI.Toasts;
using OrbitDrop.Windows.Shell;

namespace OrbitDrop.App;

public enum UpdateState { Idle, Checking, UpToDate, Available, Downloading, Installing, Failed }

/// <summary>
/// Finds, downloads and installs new OrbitDrop releases. Automatic checks run at most once a day and only while
/// "Check for updates automatically" is on; manual checks always run. Installed copies update in place via the
/// silent installer (which relaunches OrbitDrop); portable copies open the release page instead.
/// </summary>
public sealed class UpdateManager
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private readonly UpdateService _service = new();
    private readonly ToastManager _toasts;
    private readonly Func<AppSettings> _settings;
    private readonly Action<Action<AppSettings>> _update;
    private readonly Action _quit;
    private readonly Action _releaseInstance;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromHours(1) };
    private string? _notified;

    public UpdateManager(ToastManager toasts, Func<AppSettings> settings, Action<Action<AppSettings>> update, Action releaseInstance, Action quit)
    {
        _toasts = toasts;
        _settings = settings;
        _update = update;
        _quit = quit;
        _releaseInstance = releaseInstance;
        _timer.Tick += (_, _) => _ = CheckIfDueAsync();
    }

    public UpdateState State { get; private set; } = UpdateState.Idle;
    public ReleaseInfo? Available { get; private set; }
    public string? Error { get; private set; }
    public event Action? Changed;

    public static SemVersion Current => UpdateService.Current;

    /// <summary>Installed by OrbitDrop-Setup (the uninstaller sits next to the exe) rather than unzipped.</summary>
    public static bool IsInstalled => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    /// <summary>First automatic check shortly after start-up (so it never slows launch), then hourly "is it due?" ticks.</summary>
    public void Start()
    {
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        first.Tick += (_, _) => { first.Stop(); _ = CheckIfDueAsync(); _timer.Start(); };
        first.Start();
    }

    public void Stop() => _timer.Stop();

    private Task CheckIfDueAsync()
    {
        var s = _settings();
        if (!s.CheckForUpdates) return Task.CompletedTask;
        if (s.LastUpdateCheck is { } last && DateTimeOffset.UtcNow - last < CheckInterval) return Task.CompletedTask;
        return CheckAsync(manual: false);
    }

    /// <summary>Asks GitHub for a newer release. Manual checks report every outcome; automatic ones only a new version.</summary>
    public async Task CheckAsync(bool manual)
    {
        if (State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Installing) return;
        Set(UpdateState.Checking);
        try
        {
            var release = await _service.CheckAsync(Current);
            _update(s => s.LastUpdateCheck = DateTimeOffset.UtcNow);
            Available = release;
            Error = null;
            Set(release is null ? UpdateState.UpToDate : UpdateState.Available);
            Log.Info(release is null ? $"Update check: {Current} is current" : $"Update check: {release.Version} available");

            if (release is not null && (manual || _notified != release.Tag))
            {
                _notified = release.Tag;
                OfferUpdate(release);
            }
            else if (manual)
                _toasts.ShowMessage(ToastKind.Success, "You're up to date", $"OrbitDrop {Current} is the latest version.", ScreenInfo.CursorPosition);
        }
        catch (Exception ex)
        {
            Error = Plain(ex);
            Set(UpdateState.Failed);
            Log.Error("Update check failed", ex);
            if (manual) _toasts.ShowMessage(ToastKind.Warning, "Couldn't check for updates", Error, ScreenInfo.CursorPosition);
        }
    }

    private void OfferUpdate(ReleaseInfo release)
    {
        string detail = IsInstalled
            ? "Takes a few seconds. OrbitDrop restarts and keeps your settings."
            : "This is the portable copy: download the new version from GitHub.";
        _toasts.ShowAction(ToastKind.Info, $"OrbitDrop {release.Version} is available", detail,
            IsInstalled ? "Install" : "Download", () => _ = InstallAsync(), ScreenInfo.CursorPosition, TimeSpan.FromSeconds(20));
    }

    /// <summary>Downloads and verifies the installer, then hands over to it and quits so it can replace the app.</summary>
    public async Task InstallAsync()
    {
        if (Available is not { } release || State is UpdateState.Downloading or UpdateState.Installing) return;
        if (!IsInstalled)
        {
            OpenUrl(release.PageUrl);
            return;
        }

        Set(UpdateState.Downloading);
        var toast = _toasts.ShowProgress(ScreenInfo.CursorPosition);
        toast.SetRunning($"Downloading OrbitDrop {release.Version}", "Verifying as it arrives", 0, indeterminate: true);
        var progress = new Progress<double>(f => toast.SetRunning($"Downloading OrbitDrop {release.Version}", $"{f:P0}", f, indeterminate: false));
        string setup;
        try
        {
            setup = await _service.DownloadAsync(release, progress);
        }
        catch (Exception ex)
        {
            Error = Plain(ex);
            Set(UpdateState.Available);
            Log.Error("Update download failed", ex);
            toast.SetResult(ToastKind.Error, "The update didn't download", Error, canShow: false, TimeSpan.FromSeconds(12));
            return;
        }

        Set(UpdateState.Installing);
        toast.SetRunning($"Installing OrbitDrop {release.Version}", "OrbitDrop will reopen in a moment", 1, indeterminate: true);
        Log.Info($"Installing update {release.Version} from {setup}");
        // The installer refuses to run while the single-instance mutex is held (Inno AppMutex), so let go of it first.
        _releaseInstance();
        try
        {
            // /RELAUNCH=1 makes the installer start the new OrbitDrop once it has finished (see OrbitDrop.iss).
            Process.Start(new ProcessStartInfo(setup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RELAUNCH=1")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(setup)!,
            });
        }
        catch (Exception ex)
        {
            Error = Plain(ex);
            Set(UpdateState.Available);
            Log.Error("Could not start the installer", ex);
            toast.SetResult(ToastKind.Error, "The update couldn't start", Error, canShow: false, TimeSpan.FromSeconds(12));
            return;
        }
        await Task.Delay(400); // let the toast paint before the app goes away
        _quit();
    }

    private void Set(UpdateState state)
    {
        State = state;
        Changed?.Invoke();
    }

    private static string Plain(Exception ex) => ex switch
    {
        // A status code means GitHub answered: it's busy or limiting requests, not that the PC is offline.
        System.Net.Http.HttpRequestException { StatusCode: not null } => "GitHub didn't answer the update check just now. OrbitDrop will try again later.",
        System.Net.Http.HttpRequestException or TaskCanceledException => "OrbitDrop couldn't reach GitHub. Check your connection and try again.",
        _ => ex.Message,
    };

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error($"Could not open {url}", ex); }
    }
}
