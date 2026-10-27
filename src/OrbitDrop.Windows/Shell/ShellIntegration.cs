using System.Windows;
using System.Windows.Threading;
using OrbitDrop.Core;
using OrbitDrop.Windows.Interop;

namespace OrbitDrop.Windows.Shell;

/// <summary>Clipboard and "show in folder" for providers. Marshals to the UI thread (the clipboard requires STA).</summary>
public sealed class ShellIntegration(Dispatcher dispatcher) : IShellIntegration
{
    public void SetClipboardText(string text) => dispatcher.Invoke(() =>
    {
        // The clipboard can be briefly locked by another app; retry a few times.
        for (int attempt = 0; ; attempt++)
        {
            try { Clipboard.SetDataObject(text, copy: true); return; }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 5) { Thread.Sleep(30); }
        }
    });

    /// <summary>Shows the floating shelf (set by the app, which owns the window).</summary>
    public Action<IReadOnlyList<string>>? FloatHandler { get; set; }

    public void Float(IReadOnlyList<string> paths) => dispatcher.Invoke(() => (FloatHandler ?? throw new NotSupportedException())(paths));

    /// <summary>Opens the Windows share window at the pointer.</summary>
    public void Share(IReadOnlyList<string> paths) => dispatcher.Invoke(() => ShareSheet.Show(paths, ScreenInfo.CursorPosition));

    public int Recycle(IReadOnlyList<string> paths) => RecycleBin.Send(paths);

    /// <summary>The picture as a bitmap (for apps and web pages) and as PNG (keeps transparency where it's understood).</summary>
    public void CopyImage(byte[] png) => dispatcher.Invoke(() =>
    {
        var bitmap = System.Windows.Media.Imaging.BitmapFrame.Create(new MemoryStream(png), System.Windows.Media.Imaging.BitmapCreateOptions.None,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var data = new DataObject();
        data.SetImage(bitmap);
        data.SetData("PNG", new MemoryStream(png));
        for (int attempt = 0; ; attempt++)
        {
            try { Clipboard.SetDataObject(data, copy: true); return; }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 5) { Thread.Sleep(30); }
        }
    });

    public void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

    /// <summary>Opens each containing folder once and selects the given items (SHOpenFolderAndSelectItems).</summary>
    public void Reveal(IReadOnlyList<string> paths) => dispatcher.Invoke(() =>
    {
        foreach (var group in paths.Where(p => File.Exists(p) || Directory.Exists(p))
                                   .GroupBy(p => Path.GetDirectoryName(p) ?? "", StringComparer.OrdinalIgnoreCase))
        {
            IntPtr folder = Win32.ILCreateFromPath(group.Key);
            var items = group.Select(Win32.ILCreateFromPath).Where(p => p != IntPtr.Zero).ToArray();
            try
            {
                if (folder == IntPtr.Zero || items.Length == 0) continue;
                var children = items.Select(Win32.ILFindLastID).ToArray();
                Win32.SHOpenFolderAndSelectItems(folder, (uint)children.Length, children, 0);
            }
            finally
            {
                if (folder != IntPtr.Zero) Win32.ILFree(folder);
                foreach (var i in items) Win32.ILFree(i);
            }
        }
    });
}
