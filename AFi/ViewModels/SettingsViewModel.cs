using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly ISettingsService _settings;

    public ObservableCollection<ThemePreviewItem> Themes { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Начальный баланс в виде строки для ввода.
    /// Пустая строка, если баланс = 0 — чтобы пользователь не видел
    /// предзаполненный "0", к которому потом приписывается ввод.
    /// </summary>
    [ObservableProperty] private string _initialBalanceText = string.Empty;

    /// <summary>
    /// Статус сохранения начального баланса.
    /// </summary>
    [ObservableProperty] private string _initialBalanceStatus = string.Empty;

    public SettingsViewModel(IThemeService themeService, ISettingsService settings)
    {
        _themeService = themeService;
        _settings = settings;

        _themeService.ThemeChanged += OnThemeChanged;
        BuildThemes();

        // Загружаем текущий начальный баланс.
        // Если он 0 — оставляем поле пустым, иначе показываем значение.
        var current = _settings.GetInitialBalance();
        _initialBalanceText = current == 0m
            ? string.Empty
            : current.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Строит список превью по всем темам из ThemeService.
    /// </summary>
    private void BuildThemes()
    {
        Themes.Clear();

        foreach (var key in _themeService.AvailableThemeKeys)
        {
            var colors = _themeService.GetThemeColors(key);
            var item = new ThemePreviewItem
            {
                Key = key,
                Name = _themeService.GetDisplayName(key),
                IsSelected = key == _themeService.CurrentThemeName,
            };

            if (colors.TryGetValue("PageBackgroundColor", out var bg))
                item.PreviewBackground = Color.FromArgb(bg);
            if (colors.TryGetValue("CardBackgroundColor", out var card))
                item.PreviewCard = Color.FromArgb(card);
            if (colors.TryGetValue("PrimaryColor", out var primary))
                item.PreviewPrimary = Color.FromArgb(primary);
            if (colors.TryGetValue("TextPrimaryColor", out var text))
                item.PreviewText = Color.FromArgb(text);
            if (colors.TryGetValue("TextSecondaryColor", out var secondary))
                item.PreviewSecondary = Color.FromArgb(secondary);

            Themes.Add(item);
        }
    }

    /// <summary>
    /// Применить выбранную тему.
    /// </summary>
    [RelayCommand]
    private void SelectTheme(ThemePreviewItem? item)
    {
        if (item is null) return;

        _themeService.ApplyTheme(item.Key);
        StatusMessage = $"Тема «{item.Name}» применена";
    }

    /// <summary>
    /// Сохранить начальный баланс.
    /// Пустое поле трактуется как 0.
    /// </summary>
    [RelayCommand]
    private void SaveInitialBalance()
    {
        var raw = InitialBalanceText?.Trim() ?? string.Empty;

        // Пустое поле = сброс баланса в 0
        if (string.IsNullOrEmpty(raw))
        {
            _settings.SetInitialBalance(0m);
            InitialBalanceStatus = "Сохранено";
            return;
        }

        var normalized = raw.Replace(',', '.');

        if (!decimal.TryParse(normalized, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out var value))
        {
            InitialBalanceStatus = "Введите число";
            return;
        }

        _settings.SetInitialBalance(value);
        InitialBalanceStatus = "Сохранено";
    }

    /// <summary>
    /// Обновляет галочку при смене темы (в том числе с других страниц).
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (var t in Themes)
            t.IsSelected = t.Key == _themeService.CurrentThemeName;
    }
}