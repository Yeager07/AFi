using System.Collections.ObjectModel;
using AFi.Collections;
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

    private readonly HashSet<int> _selectedIds = new();

    public ObservableRangeCollection<RecurringPayment> Payments { get; } = new();

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    // ==================== Мультивыбор ====================

    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _selectedCount;

    public bool HasSelection => SelectedCount > 0;
    public bool CanEnterSelectionMode => !IsSelectionMode && Payments.Count > 0;

    public RecurringViewModel(DatabaseService db,
                              IDialogService dialogs,
                              IReminderService reminders)
    {
        _db = db;
        _dialogs = dialogs;
        _reminders = reminders;
    }

    // ==================== Реакции ====================

    partial void OnSelectedCountChanged(int value) =>
        OnPropertyChanged(nameof(HasSelection));

    partial void OnIsSelectionModeChanged(bool value) =>
        OnPropertyChanged(nameof(CanEnterSelectionMode));

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
    private async Task HandleCardTapAsync(RecurringPayment? payment)
    {
        if (payment is null) return;

        if (IsSelectionMode)
        {
            ToggleSelection(payment);
            return;
        }

        var sign = payment.Type == TransactionType.Income ? "+" : "−";
        var title = $"{sign}{payment.Amount:N0} ₽ · {payment.Name}";

        var choice = await _dialogs.ShowActionSheetAsync(
            title,
            "Отмена",
            "Редактировать",
            "Удалить");

        if (choice == "Редактировать")
        {
            await Shell.Current.GoToAsync($"recurring-edit?id={payment.Id}");
        }
        else if (choice == "Удалить")
        {
            await DeletePaymentCoreAsync(payment, skipConfirmation: true);
        }
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(RecurringPayment? payment)
    {
        await DeletePaymentCoreAsync(payment, skipConfirmation: false);
    }

    private async Task DeletePaymentCoreAsync(RecurringPayment? payment, bool skipConfirmation)
    {
        if (payment is null || IsBusy) return;

        if (!skipConfirmation)
        {
            var message = $"Удалить напоминание?\n\n" +
                          $"{payment.DisplayName}\n" +
                          $"{payment.Amount:N0} ₽ · {payment.DayOfMonth}-го числа";

            var confirmed = await _dialogs.ConfirmAsync(
                "Удаление", message, "Удалить", "Отмена");

            if (!confirmed) return;
        }

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

    // ==================== Мультивыбор ====================

    [RelayCommand]
    private void EnterSelectionMode()
    {
        if (!CanEnterSelectionMode) return;
        IsSelectionMode = true;
    }

    [RelayCommand]
    private void ToggleSelection(RecurringPayment? payment)
    {
        if (payment is null || !IsSelectionMode) return;

        if (_selectedIds.Contains(payment.Id))
        {
            _selectedIds.Remove(payment.Id);
            payment.IsSelected = false;
        }
        else
        {
            _selectedIds.Add(payment.Id);
            payment.IsSelected = true;
        }

        SelectedCount = _selectedIds.Count;
    }

    [RelayCommand]
    private void CancelSelection()
    {
        foreach (var p in Payments)
            p.IsSelected = false;

        _selectedIds.Clear();
        SelectedCount = 0;
        IsSelectionMode = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0 || IsBusy) return;

        var count = _selectedIds.Count;
        var word = Plural(count, "напоминание", "напоминания", "напоминаний");

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление",
            $"Удалить {count} {word}?\n\nЭто действие нельзя отменить.",
            "Удалить",
            "Отмена");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            foreach (var id in _selectedIds.ToList())
            {
                var payment = Payments.FirstOrDefault(p => p.Id == id);
                if (payment is not null)
                    await _reminders.CancelAsync(payment);

                await _db.DeleteRecurringPaymentAsync(id);
            }

            _selectedIds.Clear();
            SelectedCount = 0;
            IsSelectionMode = false;

            await LoadCoreAsync();
            StatusMessage = $"Удалено {count} {word}";
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

    // ==================== Вспомогательные ====================

    private async Task LoadCoreAsync()
    {
        var list = await _db.GetRecurringPaymentsAsync();

        Payments.ReplaceRange(list);

        _selectedIds.Clear();
        SelectedCount = 0;
        IsSelectionMode = false;

        OnPropertyChanged(nameof(CanEnterSelectionMode));

        StatusMessage = Payments.Count == 0
            ? "Пока нет напоминаний"
            : $"Всего напоминаний: {Payments.Count}";
    }

    private static string Plural(int count, string one, string few, string many)
    {
        int mod100 = count % 100;
        int mod10 = count % 10;
        if (mod100 >= 11 && mod100 <= 14) return many;
        if (mod10 == 1) return one;
        if (mod10 >= 2 && mod10 <= 4) return few;
        return many;
    }
}