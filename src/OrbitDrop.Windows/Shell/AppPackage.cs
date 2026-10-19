using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace OrbitDrop.Windows.Shell;

/// <summary>
/// Whether OrbitDrop runs as the Microsoft Store (MSIX) package. The same executable ships both ways; packaged, the
/// Store handles updates and "start at sign-in" uses the package's startup task instead of the Run key.
/// </summary>
public static class AppPackage
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;
    public const string StartupTaskId = "OrbitDropStartup";

    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            int length = 0;
            return GetCurrentPackageFullName(ref length, IntPtr.Zero) != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch { return false; }
    }

    /// <summary>Started by the package's startup task at sign-in (the packaged equivalent of --background).</summary>
    public static bool LaunchedAtSignIn
    {
        get
        {
            if (!IsPackaged) return false;
            try { return AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask; }
            catch { return false; }
        }
    }

    public static bool StartupEnabled
    {
        get
        {
            var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
    }

    public static void SetStartup(bool enabled)
    {
        var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
        if (enabled) task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
        else task.Disable();
    }

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentPackageFullName(ref int length, IntPtr name);
}
