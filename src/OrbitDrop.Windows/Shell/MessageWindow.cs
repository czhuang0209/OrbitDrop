using System.Windows.Interop;

namespace OrbitDrop.Windows.Shell;

/// <summary>
/// Hidden top-level window that receives tray callbacks and broadcast messages
/// (TaskbarCreated, WM_SETTINGCHANGE). A message-only window would miss broadcasts.
/// </summary>
public sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;

    public MessageWindow()
    {
        var p = new HwndSourceParameters("OrbitDrop.MessageWindow") { Width = 0, Height = 0, WindowStyle = 0 };
        _source = new HwndSource(p);
        _source.AddHook(Hook);
    }

    public IntPtr Handle => _source.Handle;

    /// <summary>(msg, wParam, lParam) → return true if handled.</summary>
    public event Func<int, IntPtr, IntPtr, bool>? Message;

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (Message is { } handlers)
            foreach (Func<int, IntPtr, IntPtr, bool> h in handlers.GetInvocationList())
                if (h(msg, wParam, lParam)) { handled = true; break; }
        return IntPtr.Zero;
    }

    public void Dispose() => _source.Dispose();
}
