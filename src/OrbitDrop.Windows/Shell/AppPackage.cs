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

    /// <summary>OrbitDrop's Microsoft Store product ID.</summary>
    public const string StoreId = "9NJQRMQLL2SZ";

    /// <summary>
    /// The Store's own "rate and review" dialog (Store version only). False if it couldn't be shown, so the caller can
    /// open the Store's review page instead.
    /// </summary>
    public static async Task<bool> RequestRatingAsync(IntPtr owner)
    {
        if (!IsPackaged) return false;
        try
        {
            var context = global::Windows.Services.Store.StoreContext.GetDefault();
            if (owner != IntPtr.Zero) WinRT.Interop.InitializeWithWindow.Initialize(context, owner);
            var result = await context.RequestRateAndReviewAppAsync();
            return result.Status != global::Windows.Services.Store.StoreRateAndReviewStatus.Error;
        }
        catch { return false; }
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
