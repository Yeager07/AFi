using AFi.Services;
using AFi.ViewModels;

namespace AFi.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _viewModel;
    private readonly IThemeService _themeService;

    private static readonly Dictionary<string, string> ThemeIcons = new()
    {
        ["Light"]     = "☀️",
        ["Dark"]      = "🌙",
        ["Space"]     = "🌌",
        ["Halloween"] = "🎃",
        ["Pixel"]     = "🕹️",
    };

    public CategoriesPage(CategoriesViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _themeService = themeService;

        _themeService.ThemeChanged += OnThemeChanged;
        UpdateThemeIcon();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        UpdateThemeIcon();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _themeService.ThemeChanged -= OnThemeChanged;
    }

    private void UpdateThemeIcon()
    {
        var current = _themeService.CurrentThemeName;
        ThemeIconItem.Text = ThemeIcons.TryGetValue(current, out var icon)
            ? icon
            : "🎨";
    }

    private void OnThemeChanged(object? sender, EventArgs e) => UpdateThemeIcon();

    /// <summary>
    /// ВРЕМЕННЫЙ обработчик для проверки темизации. Перебирает все 5 тем по кругу.
    /// Уберём на 3.4.3 (часть 2), когда появится настоящий экран «Настройки».
    /// </summary>
    private void OnToggleThemeClicked(object? sender, EventArgs e)
    {
        var keys = _themeService.AvailableThemeKeys;
        var currentIndex = -1;

        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i] == _themeService.CurrentThemeName)
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = (currentIndex + 1) % keys.Count;
        _themeService.ApplyTheme(keys[nextIndex]);
    }
}