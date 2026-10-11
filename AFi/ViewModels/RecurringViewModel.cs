using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

/// <summary>
/// ViewModel экрана «Регулярные платежи».
/// Список напоминаний, удаление, включение/выключение.
/// </summary>
public partial class RecurringViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;

    public ObservableCollection<RecurringPayment> Payments { get; } = new();

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public RecurringViewModel(DatabaseService db, IDialogService dialogs)
    {
        _db = db;
        _dialogs = dialogs;
    }

    // ==================== Команды ====================

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await _db.InitializeAsync();
            await LoadCoreAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// ВРЕМЕННАЯ заглушка. Настоящая форма добавления появится в 4.3.
    /// </summary>
    [RelayCommand]
    private async Task AddPaymentAsync()
    {
        await Shell.Current.GoToAsync("recurring-edit");
    }


    /// <summary>
    /// ВРЕМЕННАЯ заглушка. Редактирование — в 4.3.
    /// </summary>

    [RelayCommand]
    private async Task EditPaymentAsync(RecurringPayment? payment)
    {
        if (payment is null) return;

        await Shell.Current.GoToAsync($"recurring-edit?id={payment.Id}");
    }

    /// <summary>
    /// Удалить платёж с подтверждением.
    /// </summary>
    [RelayCommand]
    private async Task DeletePaymentAsync(RecurringPayment? payment)
    {
        if (payment is null || IsBusy) return;

        var message = $"Удалить напоминание?\n\n" +
                      $"{payment.DisplayName}\n" +
                      $"{payment.Amount:N0} ₽ · {FormatDayOfMonth(payment.DayOfMonth)}";

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление", message, "Удалить", "Отмена");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            await _db.DeleteRecurringPaymentAsync(payment.Id);
            await LoadCoreAsync();
            StatusMessage = "Напоминание удалено";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Переключить напоминание для платежа. Вызывается событием Switch.Toggled.
    /// Сам IsEnabled уже переключён через двустороннюю привязку.
    /// </summary>
    [RelayCommand]
    private async Task ToggleEnabledAsync(RecurringPayment? payment)
    {
        if (payment is null || IsBusy) return;

        IsBusy = true;
        try
        {
            await _db.UpdateRecurringPaymentAsync(payment);
            StatusMessage = payment.IsEnabled
                ? $"Напоминание для «{payment.Name}» включено"
                : $"Напоминание для «{payment.Name}» выключено";

            // Здесь на 4.4 будет отмена/планирование уведомления
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Вспомогательные ====================

    private async Task LoadCoreAsync()
    {
        var list = await _db.GetRecurringPaymentsAsync();

        Payments.Clear();
        foreach (var p in list)
            Payments.Add(p);

        StatusMessage = Payments.Count == 0
            ? "Пока нет регулярных платежей"
            : $"Всего напоминаний: {Payments.Count}";
    }

    /// <summary>
    /// Форматирует день месяца: "1-го числа", "5-го числа" и т.п.
    /// Используется в карточках и диалоге удаления.
    /// </summary>
    public static string FormatDayOfMonth(int day) => $"{day}-го числа";
}