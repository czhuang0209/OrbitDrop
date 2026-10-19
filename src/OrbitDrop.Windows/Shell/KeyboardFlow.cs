using System.Runtime.InteropServices;
using System.Text;

namespace OrbitDrop.Windows.Shell;

/// <summary>A system-wide shortcut registered on the message window (RegisterHotKey). Disposing unregisters it.</summary>
public sealed class GlobalHotkey : IDisposable
{
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;
    public const uint VK_SPACE = 0x20;
    private const int WM_HOTKEY = 0x0312;
    private static int _nextId = 0x0D10;

    private readonly MessageWindow _window;
    private readonly int _id = Interlocked.Increment(ref _nextId);
    private readonly Action _pressed;

    public GlobalHotkey(MessageWindow window, uint modifiers, uint key, Action pressed)
    {
        _window = window;
        _pressed = pressed;
        Registered = RegisterHotKey(window.Handle, _id, modifiers | MOD_NOREPEAT, key);
        if (Registered) window.Message += OnMessage;
    }

    /// <summary>False when another app already owns the combination.</summary>
    public bool Registered { get; }

    private bool OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_HOTKEY || wParam.ToInt32() != _id) return false;
        _pressed();
        return true;
    }

    public void Dispose()
    {
        if (!Registered) return;
        _window.Message -= OnMessage;
        UnregisterHotKey(_window.Handle, _id);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}

/// <summary>
/// The files selected in the foreground File Explorer window or on the desktop, via the Shell automation objects
/// (Shell.Application). Must run on an STA thread. Returns an empty list when nothing usable is selected.
/// </summary>
public static class ExplorerSelection
{
    public static IReadOnlyList<string> FromForegroundWindow()
    {
        IntPtr foreground = GetForegroundWindow();
        string cls = ClassOf(foreground);
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return [];
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic? view = null;

            if (cls is "Progman" or "WorkerW")
            {
                // The desktop is a shell view too: SWC_DESKTOP (8) with SWFO_NEEDDISPATCH (1).
                object location = 0, root = Type.Missing;
                view = shell.Windows().FindWindowSW(ref location, ref root, 8, out int _, 1);
            }
            else if (cls == "CabinetWClass")
            {
                // Windows 11 tabs share one window handle: pick the tab whose folder name starts the window title.
                string title = TitleOf(foreground);
                var matches = new List<dynamic>();
                foreach (dynamic w in shell.Windows())
                {
                    try { if (new IntPtr((long)w.HWND) == foreground) matches.Add(w); } catch { /* not an Explorer view */ }
                }
                view = matches.Count switch
                {
                    0 => null,
                    1 => matches[0],
                    _ => matches.FirstOrDefault(m => title.StartsWith((string)m.LocationName, StringComparison.OrdinalIgnoreCase)) ?? matches[0],
                };
            }
            if (view is null) return [];

            var paths = new List<string>();
            foreach (dynamic item in view.Document.SelectedItems())
            {
                string? path = item.Path;
                if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path))) paths.Add(path);
            }
            return paths;
        }
        catch
        {
            return [];
        }
    }

    private static string ClassOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    private static string TitleOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
}
