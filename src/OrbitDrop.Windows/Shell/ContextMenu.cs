using System.Runtime.InteropServices;
using Microsoft.Win32;
using OrbitDrop.Windows.Interop;

namespace OrbitDrop.Windows.Shell;

/// <summary>
/// "OrbitDrop" in File Explorer's right-click menu. The entry itself is OrbitDropShell.dll (src/OrbitDrop.ShellMenu):
/// choosing it sends the selected paths to OrbitDrop's message window (see <see cref="TryReadFiles"/>).
/// The Microsoft Store package registers the DLL in its manifest (Windows 11's main menu); the GitHub version
/// registers it here, for the current user only (the classic menu, under "Show more options" on Windows 11).
/// </summary>
public static class ContextMenu
{
    public const string Clsid = "{EF221BF9-D0CA-4698-95F2-5452942E7A5E}";
    public const string DllName = "OrbitDropShell.dll";
    /// <summary>COPYDATASTRUCT.dwData of an "open these files" message from the DLL.</summary>
    public const int OpenFilesMessage = 0x0D0F;
    private const int WM_COPYDATA = 0x004A, MaxItems = 500;

    private const string VerbKey = @"Software\Classes\AllFilesystemObjects\shell\OrbitDrop";
    private const string ClsidKey = @"Software\Classes\CLSID\" + Clsid;
    private const string SettingsKey = @"Software\OrbitDrop";

    /// <summary>
    /// Applies the setting. Always records it where the DLL looks (so the Store version's entry hides itself when off);
    /// on the GitHub version, also adds or removes the registration. Returns false if registering failed.
    /// </summary>
    public static bool Apply(bool enabled, bool packaged, string appDirectory)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey)) key.SetValue("ContextMenu", enabled ? 1 : 0, RegistryValueKind.DWord);
            if (packaged) return true;
            string dll = Path.Combine(appDirectory, DllName);
            if (enabled && File.Exists(dll)) Register(dll);
            else Unregister();
            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Write($"Right-click menu: {ex.Message}");
            return false;
        }
    }

    private static void Register(string dll)
    {
        using (var clsid = Registry.CurrentUser.CreateSubKey(ClsidKey))
        {
            clsid.SetValue("", "OrbitDrop right-click menu");
            using var server = clsid.CreateSubKey("InprocServer32");
            server.SetValue("", dll);
            server.SetValue("ThreadingModel", "Apartment");
        }
        using (var verb = Registry.CurrentUser.CreateSubKey(VerbKey))
        {
            verb.SetValue("ExplorerCommandHandler", Clsid);
            verb.SetValue("Icon", Path.Combine(Path.GetDirectoryName(dll)!, "OrbitDrop.exe") + ",0");
        }
        Notify();
    }

    public static void Unregister()
    {
        bool had = Registry.CurrentUser.OpenSubKey(VerbKey) is { } k && Close(k);
        Registry.CurrentUser.DeleteSubKeyTree(VerbKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(ClsidKey, throwOnMissingSubKey: false);
        if (had) Notify();
    }

    private static bool Close(RegistryKey key) { key.Dispose(); return true; }

    /// <summary>Tells Explorer the menus changed, so the entry appears or goes without a restart.</summary>
    private static void Notify() => SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0, IntPtr.Zero, IntPtr.Zero);

    /// <summary>
    /// The paths in an "open these files" WM_COPYDATA from the DLL, checked: only existing files and folders, at most
    /// 500. Null for any other message.
    /// </summary>
    public static IReadOnlyList<string>? TryReadFiles(int msg, IntPtr lParam)
    {
        if (msg != WM_COPYDATA || lParam == IntPtr.Zero) return null;
        var data = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
        if (data.dwData != OpenFilesMessage || data.lpData == IntPtr.Zero || data.cbData < 2 || data.cbData > 4 * 1024 * 1024) return null;
        string text = Marshal.PtrToStringUni(data.lpData, data.cbData / 2);
        return text.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length < 32_768 && Path.IsPathFullyQualified(p) && (File.Exists(p) || Directory.Exists(p)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxItems).ToList();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
