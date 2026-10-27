using System.Diagnostics;
using System.Text.RegularExpressions;
using OrbitDrop.Infrastructure;
using OrbitDrop.Tools;

namespace OrbitDrop.App;

/// <summary>
/// About › Switch to the Microsoft Store version, for the GitHub version. Installs the Store version first (winget,
/// straight from the Store; the Store page if that isn't possible), and only when it's really there removes this one
/// and opens it. Nothing is lost if it can't finish. Settings come along by themselves: the Store version reads
/// %APPDATA%\OrbitDrop until it saves its own copy, and the silent uninstall keeps them. A note in that folder tells
/// the Store version to say so on its first start.
/// </summary>
internal static partial class StoreSwitch
{
    public const string ProductId = "9NJQRMQLL2SZ";
    public const string PackageFamily = "MohammedEssam.OrbitDrop-DragConvert_n43xg1cshwcyc";
    private const string AppId = "OrbitDrop";
    public const string StorePage = "ms-windows-store://pdp/?ProductId=" + ProductId + "&cid=app_switch";

    /// <summary>The note the Store version looks for (in the settings folder it reads), to say "your settings came with you".</summary>
    public const string NoteName = "switched-from-github.txt";

    /// <summary>Whether the Store version is installed for this user.</summary>
    public static bool StoreVersionInstalled()
    {
        try { return new global::Windows.Management.Deployment.PackageManager().FindPackagesForUser("", PackageFamily).Any(); }
        catch (Exception ex) { Log.Error("Store version check", ex); return false; }
    }

    /// <summary>Windows' package manager, when it's there (Windows 10 and 11 with App Installer).</summary>
    public static string? Winget() => CustomActionsProvider.Resolve("winget");

    /// <summary>
    /// Installs the Store version with winget, reporting 0–1 when winget says how far it is. True once it's installed
    /// (including when it already was); false if winget failed, with its last line for the log.
    /// </summary>
    public static async Task<(bool Ok, string Detail)> InstallAsync(IProgress<double>? progress, CancellationToken ct)
    {
        string? winget = Winget();
        if (winget is null) return (false, "winget isn't available");
        var info = new ProcessStartInfo(winget,
            $"install --id {ProductId} --source msstore --accept-package-agreements --accept-source-agreements --disable-interactivity")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("winget didn't start");
        string last = "";
        var reading = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                if (line.Trim().Length > 0) last = line.Trim();
                if (Percent().Match(line) is { Success: true } m && double.TryParse(m.Groups[1].Value, out double p)) progress?.Report(Math.Clamp(p / 100, 0, 1));
            }
        }, ct);
        await process.WaitForExitAsync(ct);
        await reading;
        Log.Info($"winget install: exit {process.ExitCode}, {last}");
        // Whatever winget's exit code (it has several for "already installed"), what counts is whether it's there now.
        return (StoreVersionInstalled(), last);
    }

    [GeneratedRegex(@"(\d{1,3})\s*%")]
    private static partial Regex Percent();

    /// <summary>
    /// Leaves the note for the Store version, then hands over to a small helper that waits for this OrbitDrop to close,
    /// removes it silently (settings are kept), and opens the Store version. Both are called OrbitDrop.exe, so they must
    /// never run at once: the uninstaller closes every OrbitDrop.exe it finds.
    /// </summary>
    public static void HandOver(AppPaths paths)
    {
        Directory.CreateDirectory(paths.RoamingDirectory);
        File.WriteAllText(Path.Combine(paths.RoamingDirectory, NoteName), DateTimeOffset.Now.ToString("O"));
        string uninstaller = Path.Combine(AppContext.BaseDirectory, "unins000.exe");
        string open = $"Start-Process 'shell:AppsFolder\\{PackageFamily}!{AppId}'";
        string remove = File.Exists(uninstaller)
            ? $"Start-Process -Wait -FilePath '{uninstaller.Replace("'", "''")}' -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART'; "
            : ""; // the portable version has nothing to uninstall: its folder can simply be deleted
        string script = $"Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue; {remove}{open}";
        Log.Info(File.Exists(uninstaller) ? "Switching to the Store version: uninstalling, then opening it" : "Switching to the Store version (portable: opening it)");
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{script}\"")
            { UseShellExecute = false, CreateNoWindow = true });
    }
}
