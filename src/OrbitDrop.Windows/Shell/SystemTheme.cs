using System.Runtime.InteropServices;
using System.Windows.Media;
using Microsoft.Win32;

namespace OrbitDrop.Windows.Shell;

/// <summary>Reads the Windows app theme and accent colour, and reports changes (WM_SETTINGCHANGE "ImmersiveColorSet").</summary>
public sealed class SystemTheme : IDisposable
{
    private const int WM_SETTINGCHANGE = 0x001A, WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320, WM_SYSCOLORCHANGE = 0x0015;
    private readonly MessageWindow _window;

    public SystemTheme(MessageWindow window)
    {
        _window = window;
        _window.Message += OnMessage;
    }

    /// <summary>Raised on the UI thread when the light/dark preference, accent or contrast settings change.</summary>
    public event Action? Changed;

    public static bool IsDark
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }
    }

    public static Color? AccentColor
    {
        get
        {
            try
            {
                var c = new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
                return Color.FromRgb(c.R, c.G, c.B);
            }
            catch { return null; }
        }
    }

    private bool OnMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_SETTINGCHANGE && lParam != IntPtr.Zero && Marshal.PtrToStringUni(lParam) == "ImmersiveColorSet"
            || msg == WM_DWMCOLORIZATIONCOLORCHANGED || msg == WM_SYSCOLORCHANGE)
        {
            Changed?.Invoke();
        }
        return false;
    }

    public void Dispose() => _window.Message -= OnMessage;
}
