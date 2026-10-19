using System.Windows;
using OrbitDrop.Core;
using OrbitDrop.Infrastructure;
using OrbitDrop.Tools;
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
        _inputs.Register(FileActionsProvider.CopyToPrefix + FileActionsProvider.ChooseSuffix, (op, files, _) => AskFolderAsync("Copy to", files));
        _inputs.Register(FileActionsProvider.MoveToPrefix + FileActionsProvider.ChooseSuffix, (op, files, _) => AskFolderAsync("Move to", files));
    }

    /// <summary>A folder picker in front of everything; the chosen folder becomes the job's input.</summary>
    private Task<(bool, object?)> AskFolderAsync(string verb, FileSet files)
    {
        // An invisible, activated owner makes the dialog open in front (OrbitDrop has no window of its own).
        var owner = new Window
        {
            Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = true,
            AllowsTransparency = true, Opacity = 0, Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        owner.Show();
        owner.Activate();
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = files.Count == 1 ? $"{verb}: choose a folder for {Path.GetFileName(Path.TrimEndingDirectorySeparator(files[0]))}" : $"{verb}: choose a folder for {files.Count} items",
                InitialDirectory = _settings.RecentFolders.FirstOrDefault(Directory.Exists) ?? Path.GetDirectoryName(files[0]),
            };
            return Task.FromResult<(bool, object?)>(dialog.ShowDialog(owner) == true ? (true, dialog.FolderName) : (false, null));
        }
        finally { owner.Close(); }
    }

    /// <summary>Puts a Copy to / Move to destination at the top of the recent folders.</summary>
    private void RememberFolder(string folder) =>
        Dispatcher.BeginInvoke(() => Update(s => s.RecentFolders = [folder, .. s.RecentFolders]));

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
