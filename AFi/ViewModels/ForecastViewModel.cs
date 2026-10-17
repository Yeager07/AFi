using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AFi.ViewModels;

public partial class ForecastViewModel : ObservableObject, IRecipient<DataChangedMessage>
{
    private readonly ForecastService _forecastService;

    // ==================== Управление свежестью данных ====================
    private bool _initialLoaded;
    private bool _isDirty = true;

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

        WeakReferenceMessenger.Default.Register(this);
    }

    public void Receive(DataChangedMessage message)
    {
        if (ReferenceEquals(message.Sender, this)) return;
        _isDirty = true;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        if (_initialLoaded && !_isDirty) return;

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

            _isDirty = false;
            _initialLoaded = true;
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