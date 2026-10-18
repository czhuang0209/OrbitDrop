using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace OrbitDrop.UI.Onboarding;

/// <summary>Three-step first-run guide explaining the drag + tap gesture.</summary>
public partial class OnboardingWindow : Window
{
    private readonly (string Heading, string Body)[] _steps;
    private readonly UIElement[] _art;
    private int _step;

    public OnboardingWindow(string triggerKey)
    {
        InitializeComponent();
        KeyName.Text = triggerKey;
        _steps =
        [
            ("Pick up any file", "Start dragging one or more files in File Explorer or on your desktop, just like you normally would. Keep holding the mouse button."),
            ($"Tap {triggerKey}", $"While you're still dragging, tap {triggerKey}. The OrbitDrop wheel opens right around your pointer. Tap {triggerKey} again to switch between Convert, Tools and Actions."),
            ("Release on an option", "Let go over PNG, Rotate or Copy path, and you're done. The new file appears next to the original, which is never changed. Release on the centre or press Esc to cancel."),
        ];
        _art = [Art1, Art2, Art3];
        foreach (var _ in _steps)
            Dots.Children.Add(new Ellipse { Width = 6, Height = 6, Margin = new Thickness(3, 0, 3, 0) });
        ShowStep(0);
    }

    /// <summary>Raised when the user finishes or skips the guide.</summary>
    public event Action? Completed;

    public void ShowStep(int step)
    {
        _step = step;
        StepLabel.Text = $"STEP {step + 1} OF {_steps.Length}";
        Heading.Text = _steps[step].Heading;
        Body.Text = _steps[step].Body;
        for (int i = 0; i < _art.Length; i++) _art[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < Dots.Children.Count; i++)
        {
            var dot = (Ellipse)Dots.Children[i];
            dot.SetResourceReference(Shape.FillProperty, i == step ? "Orbit.Accent" : "Orbit.StrokeStrong");
            dot.Width = i == step ? 18 : 6;
        }
        BackButton.Visibility = step == 0 ? Visibility.Hidden : Visibility.Visible;
        SkipButton.Visibility = step == _steps.Length - 1 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = step == _steps.Length - 1 ? "Get started" : "Next";

        if (!ThemeManager.Current.ReduceMotion && IsLoaded)
            _art[step].BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    /// <summary>The content root, for offscreen design-review renders.</summary>
    public FrameworkElement ContentRoot => Root;

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_step < _steps.Length - 1) ShowStep(_step + 1);
        else Finish();
    }

    private void OnBack(object sender, RoutedEventArgs e) { if (_step > 0) ShowStep(_step - 1); }

    private void OnSkip(object sender, RoutedEventArgs e) => Finish();

    private void Finish()
    {
        Completed?.Invoke();
        Close();
    }
}
