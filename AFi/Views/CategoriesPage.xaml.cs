using AFi.Services;
using AFi.ViewModels;

namespace AFi.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _viewModel;
    private readonly IThemeService _themeService;

    public CategoriesPage(CategoriesViewModel viewModel, IThemeService themeService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _themeService = themeService;

        // Подписываемся на смену темы, чтобы обновить иконку Toolbar
        _themeService.ThemeChanged += OnThemeChanged;

        UpdateThemeIcon();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // На случай, если тема сменилась на другой странице
        UpdateThemeIcon();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _themeService.ThemeChanged -= OnThemeChanged;
    }

    /// <summary>
    /// Обновляет иконку в Toolbar в зависимости от текущей темы:
    /// ☀️ для светлой, 🌙 для тёмной.
    /// </summary>
    private void UpdateThemeIcon()
    {
        ThemeIconItem.Text = _themeService.CurrentThemeName == "Dark" ? "🌙" : "☀️";
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UpdateThemeIcon();
    }

    /// <summary>
    /// ВРЕМЕННЫЙ обработчик для проверки темизации. Уберём на 3.4.3,
    /// когда появится настоящий экран «Настройки» с выбором темы.
    /// </summary>
    private void OnToggleThemeClicked(object? sender, EventArgs e)
    {
        var next = _themeService.CurrentThemeName == "Dark" ? "Light" : "Dark";
        _themeService.ApplyTheme(next);
    }
}