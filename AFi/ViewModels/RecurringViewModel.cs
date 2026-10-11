using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class RecurringViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;
    private readonly IReminderService _reminders;

    public ObservableCollection<RecurringPayment> Payments { get; } = new();

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public RecurringViewModel(DatabaseService db,
                              IDialogService dialogs,
                              IReminderService reminders)
    {
        _db = db;
        _dialogs = dialogs;
        _reminders = reminders;
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

    [RelayCommand]
    private async Task AddPaymentAsync()
    {
        await Shell.Current.GoToAsync("recurring-edit");
    }

    [RelayCommand]
    private async Task EditPaymentAsync(RecurringPayment? payment)
    {
        if (payment is null) return;
        await Shell.Current.GoToAsync($"recurring-edit?id={payment.Id}");
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(RecurringPayment? payment)
    {
        if (payment is null || IsBusy) return;

        var message = $"Удалить напоминание?\n\n" +
                      $"{payment.DisplayName}\n" +
                      $"{payment.Amount:N0} ₽ · {payment.DayOfMonth}-го числа";

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление", message, "Удалить", "Отмена");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            await _reminders.CancelAsync(payment);
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

    [RelayCommand]
    private async Task ToggleEnabledAsync(RecurringPayment? payment)
    {
        if (payment is null || IsBusy) return;

        IsBusy = true;
        try
        {
            await _db.UpdateRecurringPaymentAsync(payment);

            if (payment.IsEnabled)
                await _reminders.ScheduleAsync(payment);
            else
                await _reminders.CancelAsync(payment);

            StatusMessage = payment.IsEnabled
                ? $"Напоминание «{payment.Name}» включено"
                : $"Напоминание «{payment.Name}» выключено";
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
            ? "Пока нет напоминаний"
            : $"Всего напоминаний: {Payments.Count}";
    }
}