using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using OrbitDrop.Conversion.Imaging;
using OrbitDrop.Conversion.Media;
using OrbitDrop.Core;
using OrbitDrop.Infrastructure;
using OrbitDrop.Jobs;
using OrbitDrop.Tools;
using OrbitDrop.UI;
using OrbitDrop.UI.Onboarding;
using OrbitDrop.UI.Settings;
using OrbitDrop.UI.Toasts;
using OrbitDrop.UI.Tray;
using OrbitDrop.UI.Wheel;
using OrbitDrop.Windows.DragDrop;
using OrbitDrop.Windows.Shell;

namespace OrbitDrop.App;

/// <summary>Composition root and lifecycle. OrbitDrop lives in the notification area; it has no main window.</summary>
public partial class App : Application, ISettingsHost, ITrayCommands
{
    private readonly AppPaths _paths = new();
    private SingleInstance? _instance;
    private SettingsStore _store = null!;
    private AppSettings _settings = new();
    private OperationRegistry _registry = null!;
    private MessageWindow? _messages;
    private SystemTheme? _systemTheme;
    private TrayIcon? _tray;
    private DragSessionDetector? _detector;
    private InteractionController? _interaction;
    private ShellIntegration _shell = null!;
    private ToastManager _toasts = null!;
    private SettingsWindow? _settingsWindow;
    private OnboardingWindow? _onboarding;
    private DispatcherTimer? _saveDebounce;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string[] args = e.Args;

        ThemeManager.Current.Initialize(this);

        // Design-review renders run alongside a live instance and never touch settings or the screen.
        int film = Array.IndexOf(args, "--render-film");
        if (film >= 0)
        {
            string dir = film + 1 < args.Length ? args[film + 1] : Path.Combine(Environment.CurrentDirectory, "film-assets");
            int code;
            try { code = FilmAssets.Run(dir, BuildRegistry()); }
            catch (Exception ex) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString()); code = 1; }
            Shutdown(code);
            return;
        }

        int render = Array.IndexOf(args, "--render-ui");
        if (render >= 0)
        {
            string dir = render + 1 < args.Length ? args[render + 1] : Path.Combine(Environment.CurrentDirectory, "ui-review");
            ThemeManager.Current.Apply(AppTheme.Light, false, false);
            int code;
            try { code = UiReview.Run(dir, BuildRegistry()); }
            catch (Exception ex) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString()); code = 1; }
            Shutdown(code);
            return;
        }

        _instance = new SingleInstance();
        if (!_instance.IsFirst)
        {
            _instance.SignalFirstInstance(); // the running copy opens Settings
            Shutdown(0);
            return;
        }

        Log.Initialize(_paths.LogsDirectory);
        _store = new SettingsStore(_paths.SettingsFile);
        _settings = _store.Load();
        Log.Detailed = _settings.DetailedLogging;
        OrbitDrop.Windows.Diagnostics.Sink = Log.Info;
        Log.Info($"OrbitDrop {VersionText} starting | {Environment.OSVersion} | .NET {Environment.Version}");

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("Unhandled UI exception", ex.Exception);
            ex.Handled = true; // keep the tray app alive; the failure is logged locally
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Error("Unhandled exception", ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Log.Error("Unobserved task exception", ex.Exception); ex.SetObserved(); };

        ThemeManager.Current.Apply(_settings.Theme, _settings.UseWindowsAccent, _settings.ReduceMotion);

        _registry = BuildRegistry();
        var jobs = new JobQueue(_registry, maxConcurrentJobs: 2);
        _shell = new ShellIntegration(Dispatcher);
        _toasts = new ToastManager { NotifyOnCompletion = () => _settings.ShowCompletionNotifications };

        _messages = new MessageWindow();
        _systemTheme = new SystemTheme(_messages);
        _systemTheme.Changed += () => Dispatcher.BeginInvoke(ApplyAppearance);

        _detector = new DragSessionDetector();
        _detector.Start();
        ApplyInteractionSettings();

        var wheel = new RadialWheel();
        wheel.Prewarm();
        _interaction = new InteractionController(wheel, _detector, _registry, jobs, _toasts, () => _settings, CreateContext, _shell);

        var iconStream = GetResourceStream(new Uri("pack://application:,,,/OrbitDrop.UI;component/Assets/orbitdrop.ico"))!.Stream;
        using (var ms = new MemoryStream()) { iconStream.CopyTo(ms); _tray = new TrayIcon(_messages, ms.ToArray(), TrayTooltip); }
        _tray.Invoked += pt => ShowTrayMenu(pt);
        _tray.ContextMenuRequested += pt => ShowTrayMenu(pt);
        _tray.Show();

        _instance.ListenForActivation(() => Dispatcher.BeginInvoke(() => OpenSettings()));

        bool background = args.Contains("--background");
        if (!_settings.OnboardingCompleted && !background) ShowOnboarding();
        else if (!background)
            _toasts.ShowMessage(ToastKind.Info, "OrbitDrop is running",
                $"Drag files and tap {TriggerKeyName}. Find OrbitDrop in the notification area.", ScreenInfo.CursorPosition);

        Log.Info("Ready");
    }

    private static OperationRegistry BuildRegistry() =>
        new([new ImageConversionProvider(), new MediaFoundationProvider(), new ImageToolsProvider(), new FileActionsProvider()]);

    private OperationContext CreateContext() => new()
    {
        Output = new OutputOptions(_settings.OutputLocation, _settings.OutputFolder),
        JpegQuality = _settings.JpegQuality,
        Shell = _shell,
    };

    private string TrayTooltip => Paused ? "OrbitDrop (paused)" : $"OrbitDrop · drag files and tap {TriggerKeyName}";

    private void ApplyAppearance()
    {
        ThemeManager.Current.Apply(_settings.Theme, _settings.UseWindowsAccent, _settings.ReduceMotion);
    }

    private void ApplyInteractionSettings()
    {
        if (_detector is null) return;
        _detector.SetTriggerKey(_settings.TriggerKey == TriggerKey.Ctrl);
        _detector.SetExtendedDetection(_settings.ExtendedDragDetection);
    }

    private void ShowTrayMenu(OrbitDrop.Windows.Interop.Win32.POINT anchor) => new TrayFlyout(this).ShowAt(anchor);

    // ------------------------------------------------------------------ ITrayCommands

    public bool Paused
    {
        get => _detector is { Enabled: false };
        set
        {
            if (_detector is null) return;
            _detector.Enabled = !value;
            _tray?.SetTooltip(TrayTooltip);
            Log.Info(value ? "Paused" : "Resumed");
        }
    }

    public string TriggerKeyName => _settings.TriggerKey == TriggerKey.Ctrl ? "Ctrl" : "Shift";

    public void OpenSettings(string? page = null)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(new SettingsViewModel(this));
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        if (page is not null) _settingsWindow.ShowPage(page);
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    public void ShowOnboarding()
    {
        if (_onboarding is not null) { _onboarding.Activate(); return; }
        _onboarding = new OnboardingWindow(TriggerKeyName);
        _onboarding.Completed += () => Update(s => s.OnboardingCompleted = true);
        _onboarding.Closed += (_, _) => _onboarding = null;
        _onboarding.Show();
        _onboarding.Activate();
    }

    public void Quit()
    {
        Log.Info("Exiting");
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _saveDebounce?.Stop();
        if (_store is not null && _instance?.IsFirst == true) SaveNow();
        _interaction?.Dispose();
        _detector?.Dispose();
        _tray?.Dispose();
        _systemTheme?.Dispose();
        _messages?.Dispose();
        _instance?.Dispose();
        Log.Shutdown();
        base.OnExit(e);
    }

    // ------------------------------------------------------------------ ISettingsHost

    AppSettings ISettingsHost.Settings => _settings;

    public void Update(Action<AppSettings> change)
    {
        var before = _settings.Clone();
        change(_settings);
        _settings.Normalize();

        if (before.Theme != _settings.Theme || before.UseWindowsAccent != _settings.UseWindowsAccent || before.ReduceMotion != _settings.ReduceMotion)
            ApplyAppearance();
        if (before.TriggerKey != _settings.TriggerKey || before.ExtendedDragDetection != _settings.ExtendedDragDetection)
        {
            ApplyInteractionSettings();
            _tray?.SetTooltip(TrayTooltip);
        }
        Log.Detailed = _settings.DetailedLogging;
        ScheduleSave();
    }

    public bool LaunchAtStartup
    {
        get { try { return StartupRegistration.IsEnabled; } catch { return false; } }
        set
        {
            try { StartupRegistration.Set(value); Log.Info($"Launch at sign-in {(value ? "on" : "off")}"); }
            catch (Exception ex) { Log.Error("Could not change startup registration", ex); }
        }
    }

    public bool AccentFallbackActive => ThemeManager.Current.AccentFallbackActive;

    public IEnumerable<(string Category, string Detail)> Capabilities => _registry.Providers.SelectMany(p => p.DescribeCapabilities());

    public string VersionText =>
        $"Version {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev"}";

    public string SettingsPath => _paths.SettingsFile;
    public string LogsPath => _paths.LogsDirectory;

    public string? LicensePath
    {
        get
        {
            string p = Path.Combine(AppContext.BaseDirectory, "LICENSE.txt");
            return File.Exists(p) ? p : null;
        }
    }

    public void PreviewWheel() => _interaction?.Preview();

    public void OpenPath(string path)
    {
        try
        {
            if (Directory.Exists(path) || File.Exists(path))
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else if (Path.GetDirectoryName(path) is { } dir && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error($"Could not open {path}", ex); }
    }

    public void ResetSettings()
    {
        bool onboarded = _settings.OnboardingCompleted;
        _settings = new AppSettings { OnboardingCompleted = onboarded };
        ApplyAppearance();
        ApplyInteractionSettings();
        Log.Detailed = false;
        SaveNow();
        Log.Info("Settings reset to defaults");
    }

    private void ScheduleSave()
    {
        _saveDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _saveDebounce.Tick -= OnSaveTick;
        _saveDebounce.Tick += OnSaveTick;
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void OnSaveTick(object? sender, EventArgs e)
    {
        _saveDebounce?.Stop();
        SaveNow();
    }

    private void SaveNow()
    {
        try { _store.Save(_settings); }
        catch (Exception ex) { Log.Error("Could not save settings", ex); }
    }
}
