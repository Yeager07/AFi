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
    private readonly IDataBackupService _backup;

    public ObservableCollection<ThemePreviewItem> Themes { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty] private string _initialBalanceText = string.Empty;
    [ObservableProperty] private string _initialBalanceStatus = string.Empty;

    // ==================== Резервное копирование ====================

    [ObservableProperty] private string _backupStatus = string.Empty;
    [ObservableProperty] private bool _isBackupBusy;

    public SettingsViewModel(IThemeService themeService,
                             ISettingsService settings,
                             IDataBackupService backup)
    {
        _themeService = themeService;
        _settings = settings;
        _backup = backup;

        _themeService.ThemeChanged += OnThemeChanged;
        BuildThemes();

        var current = _settings.GetInitialBalance();
        _initialBalanceText = current == 0m
            ? string.Empty
            : current.ToString("0.##", CultureInfo.InvariantCulture);
    }

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

    [RelayCommand]
    private void SelectTheme(ThemePreviewItem? item)
    {
        if (item is null) return;

        _themeService.ApplyTheme(item.Key);
        StatusMessage = $"Тема «{item.Name}» применена";
    }

    [RelayCommand]
    private void SaveInitialBalance()
    {
        var raw = InitialBalanceText?.Trim() ?? string.Empty;

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

    // ==================== Резервное копирование ====================

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        if (IsBackupBusy) return;

        IsBackupBusy = true;
        BackupStatus = string.Empty;

        try
        {
            var ok = await _backup.ExportAsync();
            BackupStatus = ok
                ? "Резервная копия создана"
                : "Не удалось создать копию";
        }
        catch (Exception ex)
        {
            BackupStatus = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportDataAsync()
    {
        if (IsBackupBusy) return;

        IsBackupBusy = true;
        BackupStatus = string.Empty;

        try
        {
            var ok = await _backup.ImportAsync();
            BackupStatus = ok
                ? "Данные восстановлены. Перейдите на Главную."
                : string.Empty;
        }
        catch (Exception ex)
        {
            BackupStatus = $"Ошибка: {ex.Message}";
        }
        finally
        {
            IsBackupBusy = false;
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (var t in Themes)
            t.IsSelected = t.Key == _themeService.CurrentThemeName;
    }
}