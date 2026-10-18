using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace OrbitDrop.UI.Settings;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Nav.SelectedIndex = 0;
        ThemeManager.Current.Changed += OnThemeChanged;
        Closed += (_, _) => ThemeManager.Current.Changed -= OnThemeChanged;
    }

    public static readonly string[] PageNames = ["General", "Appearance", "Shortcuts", "Conversions", "Advanced", "About"];

    public void ShowPage(string name)
    {
        int index = Array.IndexOf(PageNames, name);
        if (index >= 0) Nav.SelectedIndex = index;
    }

    /// <summary>The content root, for offscreen design-review renders.</summary>
    public FrameworkElement ContentRoot => (FrameworkElement)Content;

    private void OnThemeChanged() => _vm.Refresh();

    private void OnNavChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not ListBoxItem { Tag: string page }) return;
        foreach (UIElement child in Pages.Children)
            child.Visibility = child is FrameworkElement fe && fe.Name == page ? Visibility.Visible : Visibility.Collapsed;
        PageScroller.ScrollToTop();
    }

    private void OnChooseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose where OrbitDrop saves new files" };
        if (dialog.ShowDialog(this) == true) _vm.ChooseOutputFolder(dialog.FolderName);
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this, "Restore all OrbitDrop settings to their defaults?", "Reset settings",
            MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
        if (answer == MessageBoxResult.OK) _vm.ResetCommand.Execute(null);
    }
}
