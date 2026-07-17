namespace OverwatchRandomizer.Modern;

public partial class App : Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "Overwatch Randomizer",
            Width = 1180,
            Height = 760,
            MinimumWidth = 360,
            MinimumHeight = 600,
        };
        return window;
    }
}
