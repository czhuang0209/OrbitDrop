using System.Windows;
using OrbitDrop.Core;
using OrbitDrop.Infrastructure;
using OrbitDrop.Tools.Metadata;
using OrbitDrop.UI.Metadata;
using OrbitDrop.Windows.Interop;

namespace OrbitDrop.App;

/// <summary>The windows and pickers that collect choices for options that need them (see <see cref="InputBroker"/>).</summary>
public partial class App
{
    private void RegisterInputs()
    {
        _inputs.Register(MetadataProvider.InfoId, AskMetadataAsync);
    }

    private async Task<(bool, object?)> AskMetadataAsync(OperationDescriptor op, FileSet files, Win32.POINT anchor)
    {
        var snapshot = await FileProperties.ReadAsync(files[0]);
        var window = new MetadataWindow(snapshot);
        ShowInFront(window);
        var edit = await window.Result;
        return edit is { IsEmpty: false } ? (true, edit) : (false, null);
    }

    /// <summary>
    /// Shows a window centred on the screen and brings it forward. OrbitDrop runs in the background, so Windows
    /// may otherwise open it behind File Explorer.
    /// </summary>
    private static void ShowInFront(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Show();
        window.Topmost = true;
        window.Activate();
        window.Topmost = false;
        Log.Info($"Opened {window.GetType().Name}");
    }
}
