using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using OrbitDrop.Core;

namespace OrbitDrop.UI.Settings;

/// <summary>
/// Settings → Your actions: the user's own programs and commands on the wheel. Two are free; adding a third without
/// the upgrade opens the Upgrade window ("You've used 2 of 2 free custom actions"), and actions past the free ones
/// wear a crown until it's bought. Each card shows exactly what will run, on a sample photo.
/// </summary>
public partial class SettingsWindow
{
    private List<CustomAction> _actions = [];

    /// <summary>The colour of "what's wrong" text under a card (shared with the shortcut cards).</summary>
    private static readonly System.Windows.Media.Brush ProblemBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC4, 0x2B, 0x1C));

    /// <summary>The sample a card's "Runs:" line fills in.</summary>
    private const string SampleFile = @"C:\Users\You\Pictures\Holiday photo.jpg";

    private void BuildActions()
    {
        _actions = _vm.CustomActions.Select(a => a.Clone()).ToList();
        ActionsHost.Children.Clear();
        for (int i = 0; i < _actions.Count; i++) ActionsHost.Children.Add(ActionCard(i));
        int free = CustomAction.FreeActions;
        ActionsUsage.Text = _vm.Licensed
            ? $"{_actions.Count} of {CustomAction.MaxActions} · unlimited with your upgrade"
            : $"{Math.Min(_actions.Count, free)} of {free} free actions used · more with the upgrade \U0001F451";
        AddActionButton.IsEnabled = _actions.Count < CustomAction.MaxActions;
    }

    private void SaveActions(bool rebuild)
    {
        _vm.SetCustomActions(_actions);
        if (rebuild) BuildActions();
    }

    /// <summary>Add: past the free actions without the upgrade, the Upgrade window; otherwise a menu of ready-made actions.</summary>
    private void OnAddAction(object sender, RoutedEventArgs e)
    {
        if (!_vm.Licensed && _actions.Count >= CustomAction.FreeActions)
        {
            _vm.ShowActionsUpgrade();
            return;
        }
        var menu = new ContextMenu { PlacementTarget = AddActionButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var template in CustomAction.Templates)
        {
            var item = new MenuItem { Header = template.Name };
            item.Click += (_, _) => AddAction(template);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var blank = new MenuItem { Header = "Start from scratch" };
        blank.Click += (_, _) => AddAction(new CustomAction { Name = $"Action {_actions.Count + 1}", Arguments = "{file}" });
        menu.Items.Add(blank);
        menu.IsOpen = true;
    }

    private void AddAction(CustomAction template)
    {
        var action = template.Clone();
        if (action.Name == "Open in Photoshop") { action.Program = FindPhotoshop() ?? ""; action.Arguments = "{files}"; action.PerFile = false; }
        _actions.Add(action);
        SaveActions(rebuild: true);
    }

    /// <summary>Photoshop.exe in Program Files › Adobe (any version), or null.</summary>
    private static string? FindPhotoshop()
    {
        try
        {
            string adobe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Adobe");
            return !Directory.Exists(adobe) ? null : Directory.EnumerateDirectories(adobe, "Adobe Photoshop*")
                .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                .Select(d => Path.Combine(d, "Photoshop.exe")).FirstOrDefault(File.Exists);
        }
        catch (Exception) { return null; }
    }

    private FrameworkElement ActionCard(int index)
    {
        var action = _actions[index];
        bool locked = !_vm.Licensed && index >= CustomAction.FreeActions;
        var card = new Border { Style = (Style)FindResource("Orbit.Card"), Padding = new Thickness(16, 12, 16, 12) };
        var panel = new StackPanel();
        card.Child = panel;

        var problem = new TextBlock { Style = (Style)FindResource("Orbit.Text.Caption"), Foreground = ProblemBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        var runs = new TextBlock { Style = (Style)FindResource("Orbit.Text.Caption"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas") };
        void Refresh()
        {
            string? why = action.Problem();
            problem.Text = why ?? "";
            problem.Visibility = why is null ? Visibility.Collapsed : Visibility.Visible;
            runs.Text = why is null ? $"Runs: {Preview(action)}" : "";
            runs.Visibility = why is null ? Visibility.Visible : Visibility.Collapsed;
        }
        void Changed() { SaveActions(rebuild: false); Refresh(); }

        // Name · (crown) · delete
        var top = new DockPanel();
        var delete = new Button { Style = (Style)FindResource("Orbit.Button.Subtle"), ToolTip = "Delete this action", Margin = new Thickness(8, 0, 0, 0),
            Content = new TextBlock { Style = (Style)FindResource("Orbit.Glyph"), Text = "\uE74D", FontSize = 14 } };
        delete.Click += (_, _) => { _actions.RemoveAt(index); SaveActions(rebuild: true); };
        DockPanel.SetDock(delete, Dock.Right);
        top.Children.Add(delete);
        if (locked)
        {
            var crown = new TextBlock { Text = "\U0001F451 Needs the upgrade", Style = (Style)FindResource("Orbit.Text.Caption"), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0), ToolTip = $"The first {CustomAction.FreeActions} actions are free; more come with the upgrade." };
            DockPanel.SetDock(crown, Dock.Right);
            top.Children.Add(crown);
        }
        var name = new TextBox { Text = action.Name, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, MaxWidth = 280, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 200 };
        AutomationProperties.SetName(name, "Action name");
        name.LostFocus += (_, _) => { if (name.Text.Trim() != action.Name) { action.Name = name.Text.Trim(); Changed(); } };
        top.Children.Add(name);
        panel.Children.Add(top);

        // Program · Browse
        var programRow = Labelled("Program", out var programSlot);
        var program = new TextBox { Text = action.Program, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "A program (Browse), or a name like code or python. Empty: the arguments are a command." };
        AutomationProperties.SetName(program, "Program");
        program.LostFocus += (_, _) => { if (program.Text.Trim() != action.Program) { action.Program = program.Text.Trim(); Changed(); } };
        var browse = new Button { Content = "Browse…", Style = (Style)FindResource("Orbit.Button.Standard"), Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog { Title = "Choose the program", Filter = "Programs (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            program.Text = action.Program = dialog.FileName;
            Changed();
        };
        DockPanel.SetDock(browse, Dock.Right);
        programSlot.Children.Add(browse);
        programSlot.Children.Add(program);
        panel.Children.Add(programRow);

        // Arguments · placeholders
        var argsRow = Labelled("Arguments", out var argsSlot);
        var args = new TextBox { Text = action.Arguments, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas") };
        AutomationProperties.SetName(args, "Arguments");
        args.LostFocus += (_, _) => { if (args.Text != action.Arguments) { action.Arguments = args.Text; Changed(); } };
        argsSlot.Children.Add(args);
        panel.Children.Add(argsRow);
        var chipsRow = Labelled("", out var chipsSlot); // lined up under the arguments box
        chipsRow.Margin = new Thickness(0, 6, 0, 0);
        var chips = new WrapPanel();
        foreach (var (token, tip) in new[] { ("{file}", "The dropped file, in quotes"), ("{files}", "All the dropped files, each in quotes"), ("{folder}", "Their folder, in quotes"), ("{name}", "The file's name, without extension"), ("{ext}", "The file's extension, like jpg") })
        {
            var chip = new Button { Content = token, Style = (Style)FindResource("Orbit.Button.Standard"), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0), FontSize = 12, ToolTip = tip };
            chip.Click += (_, _) =>
            {
                int at = args.CaretIndex;
                args.Text = args.Text.Insert(at, token);
                args.CaretIndex = at + token.Length;
                action.Arguments = args.Text;
                Changed();
            };
            chips.Children.Add(chip);
        }
        chipsSlot.Children.Add(chips);
        panel.Children.Add(chipsRow);

        // Works on · once per file · show its window
        var optionsRow = Labelled("Works on", out var optionsSlot);
        var options = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var kind = new ComboBox { Width = 150, FontSize = 13, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var k in CustomAction.Kinds) kind.Items.Add(new ComboBoxItem { Content = k, Tag = k, IsSelected = k == action.Kind });
        AutomationProperties.SetName(kind, "Works on");
        var extensions = new TextBox { Text = action.Extensions, Width = 130, FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0),
            ToolTip = "File types, like .psd .ai", Visibility = action.Kind == "Chosen types" ? Visibility.Visible : Visibility.Collapsed };
        AutomationProperties.SetName(extensions, "File types");
        extensions.LostFocus += (_, _) => { if (extensions.Text != action.Extensions) { action.Extensions = extensions.Text; Changed(); } };
        kind.SelectionChanged += (_, _) =>
        {
            if (kind.SelectedItem is not ComboBoxItem { Tag: string k } || k == action.Kind) return;
            action.Kind = k;
            extensions.Visibility = k == "Chosen types" ? Visibility.Visible : Visibility.Collapsed;
            Changed();
        };
        var perFile = new CheckBox { Content = "Once per file", IsChecked = action.PerFile, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0),
            ToolTip = "On: runs separately for each file. Off: runs once, with all of them ({files})." };
        perFile.Click += (_, _) => { action.PerFile = perFile.IsChecked == true; Changed(); };
        var show = new CheckBox { Content = "Show its window", IsChecked = action.ShowWindow, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Shows the window and keeps it open when it's done, to read a script's output." };
        show.Click += (_, _) => { action.ShowWindow = show.IsChecked == true; Changed(); };
        options.Children.Add(kind);
        options.Children.Add(extensions);
        options.Children.Add(perFile);
        options.Children.Add(show);
        optionsSlot.Children.Add(options);
        panel.Children.Add(optionsRow);

        panel.Children.Add(problem);
        panel.Children.Add(runs);
        Refresh();
        if (locked) card.Opacity = 0.7;
        return card;
    }

    /// <summary>A row with a fixed-width label and a slot for its controls.</summary>
    private DockPanel Labelled(string label, out DockPanel slot)
    {
        var row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var text = new TextBlock { Text = label, Style = (Style)FindResource("RowHint"), Width = 96, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(text, Dock.Left);
        row.Children.Add(text);
        slot = new DockPanel();
        row.Children.Add(slot);
        return row;
    }

    /// <summary>The command line, as it'll run on the sample photo (the program as typed, so it reads naturally).</summary>
    private static string Preview(CustomAction action)
    {
        string expanded = action.Expand(action.PerFile ? [SampleFile] : [SampleFile, @"C:\Users\You\Pictures\Beach.png"]);
        return action.Program.Length == 0 ? expanded : $"{(action.Program.Contains(' ') ? CustomAction.Quote(action.Program) : action.Program)} {expanded}";
    }
}
