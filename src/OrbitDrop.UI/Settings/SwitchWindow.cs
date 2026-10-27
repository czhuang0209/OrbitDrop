using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitDrop.UI.Settings;

/// <summary>
/// "Switch to the Microsoft Store version" (GitHub version, Settings › About): says what changes, installs the Store
/// version with a progress bar, then hands over. If installing isn't possible it says so and offers the Store page;
/// nothing is removed until the Store version is really there.
/// </summary>
public sealed class SwitchWindow : Window
{
    private readonly Func<IProgress<double>, CancellationToken, Task<(bool Ok, string Detail)>> _install;
    private readonly Action _handOver, _openStorePage;
    private readonly StackPanel _body = new() { Margin = new Thickness(28, 24, 28, 24) };
    private CancellationTokenSource? _cancel;

    /// <param name="install">Installs the Store version (0–1 progress); true once it's on the PC.</param>
    /// <param name="handOver">Closes this OrbitDrop, removes it and opens the Store version.</param>
    /// <param name="openStorePage">OrbitDrop's page in the Microsoft Store app (the way when installing here isn't possible).</param>
    /// <param name="portable">The portable version: nothing to uninstall, its folder can be deleted.</param>
    public SwitchWindow(Func<IProgress<double>, CancellationToken, Task<(bool, string)>> install, Action handOver, Action openStorePage, bool portable)
    {
        _install = install;
        _handOver = handOver;
        _openStorePage = openStorePage;
        Title = "Switch to the Microsoft Store version";
        Width = 520; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; UseLayoutRounding = true;
        Icon = new BitmapImage(new Uri("pack://application:,,,/OrbitDrop.UI;component/Assets/orbitdrop.ico"));
        SetResourceReference(BackgroundProperty, "Orbit.SurfaceBase");
        SetResourceReference(ForegroundProperty, "Orbit.TextPrimary");
        FontFamily = (FontFamily)FindResource("Orbit.Font.Text");
        Content = _body;
        Closing += (_, _) => _cancel?.Cancel();
        Confirm(portable);
    }

    /// <summary>For design-review renders: 0 asking, 1 installing (at <paramref name="progress"/>), 2 installed, 3 couldn't install.</summary>
    public void ReviewState(int state, double progress = 0.42)
    {
        if (state == 0) Confirm(portable: false);
        else if (state == 2) Done();
        else if (state == 3) Failed();
        else
        {
            _ = InstallAsync();
            var bar = _body.Children.OfType<ProgressBar>().First();
            bar.IsIndeterminate = false;
            bar.Value = progress;
            ((TextBlock)_body.Children[1]).Text = $"Getting OrbitDrop from the Store… {progress:P0}";
        }
    }

    private void Confirm(bool portable)
    {
        _body.Children.Clear();
        _body.Children.Add(Heading("Switch to the Microsoft Store version?"));
        _body.Children.Add(Paragraph("The same OrbitDrop, with:"));
        foreach (var point in new[]
        {
            "Updates that install themselves, through the Store",
            "No “Windows protected your PC” warning",
            "Remove background, upscale, stickers and more, one click away (try each 3 times free)",
            "Your settings, wheel and own actions come with you",
        })
            _body.Children.Add(Paragraph("✓  " + point, 0, 4));
        _body.Children.Add(Paragraph(portable
            ? "OrbitDrop installs the Store version, then opens it. You can delete this portable folder afterwards."
            : "OrbitDrop installs the Store version first, and only when it's there removes this one and opens it. If anything goes wrong, you keep the version you have.", 14, 0, secondary: true));
        var buttons = Row();
        buttons.Children.Add(Button("Switch now", accent: true, () => _ = InstallAsync()));
        buttons.Children.Add(Button("Not now", accent: false, Close));
        _body.Children.Add(buttons);
    }

    private async Task InstallAsync()
    {
        _body.Children.Clear();
        _body.Children.Add(Heading("Installing from the Microsoft Store…"));
        var status = Paragraph("Getting OrbitDrop from the Store. This takes a minute or two.");
        _body.Children.Add(status);
        var bar = new ProgressBar { Height = 6, Margin = new Thickness(0, 16, 0, 0), IsIndeterminate = true, Maximum = 1 };
        _body.Children.Add(bar);
        var buttons = Row();
        buttons.Children.Add(Button("Cancel", accent: false, () => { _cancel?.Cancel(); Close(); }));
        _body.Children.Add(buttons);
        _cancel = new CancellationTokenSource();
        var progress = new Progress<double>(p => { bar.IsIndeterminate = false; bar.Value = p; status.Text = $"Getting OrbitDrop from the Store… {p:P0}"; });
        (bool ok, string detail) result;
        try { result = await _install(progress, _cancel.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { result = (false, ex.Message); }
        if (result.ok) Done();
        else Failed();
    }

    private void Done()
    {
        _body.Children.Clear();
        _body.Children.Add(Heading("The Store version is installed 🎉"));
        _body.Children.Add(Paragraph("OrbitDrop now closes, removes this version and opens the Store one, with your settings. It takes a few seconds."));
        var buttons = Row();
        buttons.Children.Add(Button("Continue", accent: true, () => { _handOver(); Close(); }));
        _body.Children.Add(buttons);
    }

    private void Failed()
    {
        _body.Children.Clear();
        _body.Children.Add(Heading("It couldn't be installed from here"));
        _body.Children.Add(Paragraph("Nothing has changed: you still have this version. You can install it from the Microsoft Store app instead (your settings come with you there too), then uninstall this one in Settings › Apps."));
        var buttons = Row();
        buttons.Children.Add(Button("Open the Store page", accent: true, () => { _openStorePage(); Close(); }));
        buttons.Children.Add(Button("Close", accent: false, Close));
        _body.Children.Add(buttons);
    }

    private TextBlock Heading(string text) =>
        new() { Text = text, Style = (Style)FindResource("Orbit.Text.Title"), FontSize = 20, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };

    private TextBlock Paragraph(string text, double top = 0, double bottom = 0, bool secondary = false)
    {
        var block = new TextBlock { Text = text, Style = (Style)FindResource("Orbit.Text.Body"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, top, 0, bottom) };
        if (secondary) block.SetResourceReference(TextBlock.ForegroundProperty, "Orbit.TextSecondary");
        return block;
    }

    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };

    private Button Button(string text, bool accent, Action click)
    {
        var button = new Button { Content = text, Style = (Style)FindResource(accent ? "Orbit.Button.Accent" : "Orbit.Button.Subtle"),
            Padding = new Thickness(18, 9, 18, 9), Margin = new Thickness(0, 0, 10, 0), MinWidth = accent ? 140 : 0 };
        button.Click += (_, _) => click();
        return button;
    }
}
