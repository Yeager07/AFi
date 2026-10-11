using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class ForecastViewModel : ObservableObject
{
    private readonly ForecastService _forecastService;

    public ObservableCollection<ForecastItem> Items { get; } = new();

    [ObservableProperty] private string _totalText = "0 ₽";
    [ObservableProperty] private string _periodText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasForecast;
    [ObservableProperty] private bool _hasAnyData;

    public ForecastViewModel(ForecastService forecastService)
    {
        _forecastService = forecastService;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var result = await _forecastService.CalculateAsync();

            Items.Clear();
            foreach (var item in result.Items)
                Items.Add(item);

            TotalText = result.TotalPredictedText;
            PeriodText = result.PeriodText;
            HasForecast = result.HasForecast;
            HasAnyData = result.Items.Count > 0;

            StatusMessage = result.Items.Count == 0
                ? "За последние 3 месяца нет данных для прогноза"
                : !result.HasForecast
                    ? "Все категории были разовыми — прогноз не составлен"
                    : $"Прогноз на следующий месяц · {result.Items.Count(i => i.IsIncluded)} категорий";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка расчёта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}