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

    private int? _editingId;
    private int? _pendingCategoryId;
    private bool _suppressTypeChangeReload;

    // ==================== Состояние формы ====================

    [ObservableProperty] private string _formTitle = "Новое напоминание";

    [ObservableProperty] private string _name = string.Empty;

    [ObservableProperty] private string _amountText = string.Empty;

    [ObservableProperty] private int _selectedTypeIndex;

    [ObservableProperty] private Category? _selectedCategory;

    [ObservableProperty] private int _selectedDayIndex;

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

    public ObservableCollection<Category> AvailableCategories { get; } = new();

    public TransactionType CurrentType =>
        SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    public bool IsEditing => _editingId.HasValue;

    public RecurringEditViewModel(DatabaseService db)
    {
        _db = db;

        for (int i = 1; i <= 31; i++)
            DayOptions.Add(i);

        _selectedDayIndex = 0;
    }

    // ==================== Инициализация от навигации ====================

    public void InitializeForEdit(int? paymentId)
    {
        _editingId = paymentId;
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

            if (_editingId.HasValue)
                await LoadExistingAsync();

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

        // ==================== Сохранение ====================

        IsBusy = true;
        try
        {
            var day = DayOptions[SelectedDayIndex];

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
            }
            else
            {
                var payment = new RecurringPayment
                {
                    Name = Name.Trim(),
                    Amount = amount,
                    Type = CurrentType,
                    DayOfMonth = day,
                    ReminderDaysBefore = 2,
                    ReminderTime = TimeSpan.FromHours(10),
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
        Note = payment.Note ?? string.Empty;

        var dayIdx = DayOptions.IndexOf(payment.DayOfMonth);
        SelectedDayIndex = dayIdx >= 0 ? dayIdx : 0;

        // ← вот ключевое: сохраняем id категории, чтобы после
        // пересборки списка категорий восстановить выбранную
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

            // Приоритет: восстановить категорию по сохранённому id
            // (при загрузке платежа) или по уже выбранному объекту
            // (при смене типа в UI).
            int? idToRestore = _pendingCategoryId ?? SelectedCategory?.Id;

            if (idToRestore.HasValue)
            {
                var matched = AvailableCategories
                    .FirstOrDefault(c => c.Id == idToRestore.Value);

                SelectedCategory = matched;

                // Сбрасываем только если категория найдена.
                // Если не найдена — оставляем pending на следующий
                // тип (маловероятно, но пусть будет)
                if (matched is not null)
                    _pendingCategoryId = null;
            }
            else
            {
                // Если ранее ничего не было выбрано и сейчас ничего
                // — ставим null (не первая категория из списка)
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