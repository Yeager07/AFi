using AFi.Services;

namespace AFi;

public partial class App : Application
{
    private readonly IThemeService _themeService;

    public App(IThemeService themeService)
    {
        InitializeComponent();

        _themeService = themeService;

        // Применяем сохранённую тему (по умолчанию — Light)
        _themeService.ApplySavedTheme();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}