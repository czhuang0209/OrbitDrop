using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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

    public static readonly string[] PageNames = ["General", "Appearance", "Shortcuts", "Wheel", "Conversions", "Advanced", "About"];

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
        if (page == "Wheel") BuildWheelPage(); // built on first visit: the catalog asks every provider
        foreach (UIElement child in Pages.Children)
            child.Visibility = child is FrameworkElement fe && fe.Name == page ? Visibility.Visible : Visibility.Collapsed;
        PageScroller.ScrollToTop();
    }

    // ------------------------------------------------------------------ Wheel page

    private readonly List<(ToggleButton Chip, string Key, ToggleButton? Parent)> _chips = [];

    /// <summary>Builds the Wheel page from the catalog: per kind of file, per mode, a chip for every option.</summary>
    private void BuildWheelPage()
    {
        if (WheelOptions.Children.Count > 0) return;
        foreach (var category in _vm.WheelCatalog)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 18, 0, 8) };
            header.Children.Add(new TextBlock { Text = category.Glyph, Style = (Style)FindResource("Orbit.Glyph"), Margin = new Thickness(0, 0, 10, 0) });
            header.Children.Add(new TextBlock { Text = category.Title, Style = (Style)FindResource("Orbit.Text.BodyStrong") });
            WheelOptions.Children.Add(header);

            var card = new Border { Style = (Style)FindResource("Orbit.Card"), Padding = new Thickness(16, 12, 16, 8) };
            var body = new StackPanel();
            foreach (var section in category.Modes)
            {
                body.Children.Add(new TextBlock
                {
                    Text = section.Mode.ToString().ToUpperInvariant(), Style = (Style)FindResource("Orbit.Text.Caption"),
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, body.Children.Count == 0 ? 0 : 8, 0, 6),
                });
                var plain = new WrapPanel();
                body.Children.Add(plain);
                foreach (var entry in section.Entries)
                {
                    if (entry.Members.Count == 0) { plain.Children.Add(Chip(entry.Choice, null, small: false)); continue; }
                    // A group: its chip, then its choices (disabled while the whole group is off).
                    var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
                    var groupChip = Chip(entry.Choice, null, small: false, isGroup: true);
                    row.Children.Add(groupChip);
                    row.Children.Add(new TextBlock { Text = "›", Style = (Style)FindResource("Orbit.Text.Caption"), Margin = new Thickness(2, 4, 8, 0) });
                    foreach (var m in entry.Members) row.Children.Add(Chip(m, groupChip, small: true));
                    body.Children.Add(row);
                }
                if (plain.Children.Count == 0) body.Children.Remove(plain);
            }
            card.Child = body;
            WheelOptions.Children.Add(card);
        }
        RefreshChips();
    }

    private ToggleButton Chip(WheelChoice choice, ToggleButton? parent, bool small, bool isGroup = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var tick = new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("Orbit.Font.Icons"), FontSize = small ? 9 : 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 6, 0) };
        content.Children.Add(tick);
        if (choice.Glyph is { } g && !small)
            content.Children.Add(new TextBlock { Text = g, FontFamily = (FontFamily)FindResource("Orbit.Font.Icons"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        content.Children.Add(new TextBlock { Text = isGroup ? $"{choice.Label} (all)" : choice.Label, VerticalAlignment = VerticalAlignment.Center });
        var chip = new ToggleButton { Style = (Style)FindResource("WheelChip"), Content = content };
        if (small) { chip.Padding = new Thickness(8, 3, 8, 3); chip.FontSize = 11.5; }
        chip.Tag = tick; // RefreshChips shows the tick only on chips that are on
        System.Windows.Automation.AutomationProperties.SetName(chip, $"Show {choice.Label} on the wheel");
        chip.Click += (_, _) =>
        {
            _vm.SetShown(choice.Key, chip.IsChecked == true);
            RefreshChips();
        };
        _chips.Add((chip, choice.Key, parent));
        return chip;
    }

    /// <summary>Syncs every chip with the settings (the same option can appear under more than one kind of file).</summary>
    private void RefreshChips()
    {
        var hidden = _vm.HiddenOptions;
        foreach (var (chip, key, parent) in _chips)
        {
            chip.IsChecked = !hidden.Contains(key);
            ((TextBlock)chip.Tag).Visibility = chip.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            if (parent is not null) chip.IsEnabled = parent.IsChecked == true;
        }
        // Parents first, then members, so a member's IsEnabled sees its parent's final state.
        foreach (var (chip, _, parent) in _chips) if (parent is not null) chip.IsEnabled = parent.IsChecked == true;
    }

    private void OnShowEverything(object sender, RoutedEventArgs e)
    {
        _vm.ShowEverything();
        RefreshChips();
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
