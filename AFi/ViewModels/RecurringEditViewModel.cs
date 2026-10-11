using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class RecurringEditViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly IReminderService _reminders;

    private int? _editingId;
    private int? _pendingCategoryId;
    private bool _suppressTypeChangeReload;
    private bool _loadedFromDb;

    // ==================== Состояние формы ====================

    [ObservableProperty] private string _formTitle = "Новое напоминание";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private int _selectedTypeIndex;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private int _selectedDayIndex;
    [ObservableProperty] private int _selectedDaysBeforeIndex = 1;
    [ObservableProperty] private TimeSpan _reminderTime = TimeSpan.FromHours(10);
    [ObservableProperty] private bool _reminderEnabled = true;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    // ==================== Списки ====================

    public ObservableCollection<string> TypeOptions { get; } = new()
    {
        "Расход",
        "Доход",
    };

    public ObservableCollection<int> DayOptions { get; } = new();

    public ObservableCollection<string> DaysBeforeOptions { get; } = new()
    {
        "В день платежа",
        "За 1 день",
        "За 2 дня",
        "За 3 дня",
        "За 5 дней",
        "За 7 дней",
        "За 14 дней",
        "За 30 дней",
    };

    public static readonly int[] DaysBeforeValues = { 0, 1, 2, 3, 5, 7, 14, 30 };

    public ObservableCollection<Category> AvailableCategories { get; } = new();

    public TransactionType CurrentType =>
        SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    public bool IsEditing => _editingId.HasValue;

    public RecurringEditViewModel(DatabaseService db, IReminderService reminders)
    {
        _db = db;
        _reminders = reminders;

        for (int i = 1; i <= 31; i++)
            DayOptions.Add(i);

        _selectedDayIndex = 0;
    }

    // ==================== Инициализация от навигации ====================

    public void InitializeForEdit(int? paymentId)
    {
        // Если уже инициализированы тем же id — не сбрасываем флаг _loadedFromDb,
        // иначе повторный вызов ApplyQueryAttributes снова начнёт читать из БД
        // и затрёт правки пользователя.
        if (_editingId == paymentId)
            return;

        _editingId = paymentId;
        _loadedFromDb = false;
        FormTitle = paymentId.HasValue ? "Редактирование" : "Новое напоминание";
    }

    // ==================== Реакции ====================

    partial void OnSelectedTypeIndexChanged(int value)
    {
        if (_suppressTypeChangeReload) return;
        _ = ReloadCategoriesAsync();
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

            // Читаем из БД только при первой загрузке формы.
            // При возврате из модалок Picker-ов LoadAsync вызывается снова,
            // но данные в форме уже актуальные — перечитывать их нельзя.
            if (_editingId.HasValue && !_loadedFromDb)
            {
                await LoadExistingAsync();
                _loadedFromDb = true;
            }

            await ReloadCategoriesAsync();
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
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        // ==================== Валидация ====================

        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Введите название";
            return;
        }

        if (string.IsNullOrWhiteSpace(AmountText))
        {
            StatusMessage = "Введите сумму";
            return;
        }

        var normalized = AmountText.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out var amount))
        {
            StatusMessage = "Сумма должна быть числом";
            return;
        }

        if (amount <= 0)
        {
            StatusMessage = "Сумма должна быть больше нуля";
            return;
        }

        if (SelectedDayIndex < 0 || SelectedDayIndex >= DayOptions.Count)
        {
            StatusMessage = "Выберите день месяца";
            return;
        }

        if (SelectedDaysBeforeIndex < 0 || SelectedDaysBeforeIndex >= DaysBeforeValues.Length)
        {
            StatusMessage = "Выберите, за сколько дней напоминать";
            return;
        }

        // ==================== Сохранение ====================

        IsBusy = true;
        try
        {
            var day = DayOptions[SelectedDayIndex];
            var daysBefore = DaysBeforeValues[SelectedDaysBeforeIndex];

            if (_editingId.HasValue)
            {
                var existing = (await _db.GetRecurringPaymentsAsync())
                    .FirstOrDefault(p => p.Id == _editingId.Value);

                if (existing is null)
                {
                    StatusMessage = "Платёж не найден";
                    return;
                }

                existing.Name = Name.Trim();
                existing.Amount = amount;
                existing.Type = CurrentType;
                existing.DayOfMonth = day;
                existing.ReminderDaysBefore = daysBefore;
                existing.ReminderTime = ReminderTime;
                existing.IsEnabled = ReminderEnabled;
                existing.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

                if (SelectedCategory is not null)
                {
                    existing.CategoryId = SelectedCategory.Id;
                    existing.CategoryName = SelectedCategory.Name;
                    existing.CategoryIcon = SelectedCategory.Icon;
                }
                else
                {
                    existing.CategoryId = null;
                    existing.CategoryName = string.Empty;
                    existing.CategoryIcon = string.Empty;
                }

                await _db.UpdateRecurringPaymentAsync(existing);

                if (existing.IsEnabled)
                    await _reminders.ScheduleAsync(existing);
                else
                    await _reminders.CancelAsync(existing);
            }
            else
            {
                var payment = new RecurringPayment
                {
                    Name = Name.Trim(),
                    Amount = amount,
                    Type = CurrentType,
                    DayOfMonth = day,
                    ReminderDaysBefore = daysBefore,
                    ReminderTime = ReminderTime,
                    IsEnabled = ReminderEnabled,
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                    CreatedAt = DateTime.Now,
                };

                if (SelectedCategory is not null)
                {
                    payment.CategoryId = SelectedCategory.Id;
                    payment.CategoryName = SelectedCategory.Name;
                    payment.CategoryIcon = SelectedCategory.Icon;
                }

                await _db.AddRecurringPaymentAsync(payment);

                if (payment.IsEnabled)
                    await _reminders.ScheduleAsync(payment);
            }

            await Shell.Current.GoToAsync("..");
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

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    // ==================== Вспомогательные ====================

    private async Task LoadExistingAsync()
    {
        if (!_editingId.HasValue) return;

        var payment = (await _db.GetRecurringPaymentsAsync())
            .FirstOrDefault(p => p.Id == _editingId.Value);

        if (payment is null)
        {
            StatusMessage = "Платёж не найден";
            return;
        }

        _suppressTypeChangeReload = true;

        Name = payment.Name;
        AmountText = payment.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        SelectedTypeIndex = payment.Type == TransactionType.Income ? 1 : 0;
        ReminderEnabled = payment.IsEnabled;
        ReminderTime = payment.ReminderTime;
        Note = payment.Note ?? string.Empty;

        var dayIdx = DayOptions.IndexOf(payment.DayOfMonth);
        SelectedDayIndex = dayIdx >= 0 ? dayIdx : 0;

        var daysIdx = Array.IndexOf(DaysBeforeValues, payment.ReminderDaysBefore);
        SelectedDaysBeforeIndex = daysIdx >= 0 ? daysIdx : 2;

        _pendingCategoryId = payment.CategoryId;

        _suppressTypeChangeReload = false;
    }

    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var categories = await _db.GetCategoriesAsync(CurrentType);

            AvailableCategories.Clear();
            foreach (var c in categories)
                AvailableCategories.Add(c);

            int? idToRestore = _pendingCategoryId ?? SelectedCategory?.Id;

            if (idToRestore.HasValue)
            {
                var matched = AvailableCategories
                    .FirstOrDefault(c => c.Id == idToRestore.Value);

                SelectedCategory = matched;

                if (matched is not null)
                    _pendingCategoryId = null;
            }
            else
            {
                if (SelectedCategory is not null
                    && !AvailableCategories.Contains(SelectedCategory))
                {
                    SelectedCategory = null;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки категорий: {ex.Message}";
        }
    }
}