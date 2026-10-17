using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Collections;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AFi.ViewModels;

public partial class TransactionEditViewModel : ObservableObject, ITransactionFormViewModel
{
    private readonly DatabaseService _db;
    private readonly IReceiptService _receipts;

    private int? _transactionId;
    private Transaction? _transaction;
    private bool _suppressTypeChangeReload;
    private bool _loaded;

    private string? _currentReceiptFileName;
    private string? _originalReceiptFileName;

    public event EventHandler<bool>? EditCompleted;

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private int _selectedTypeIndex;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DateTime _maxDate = DateTime.Today;

    public bool IsEditing => true;

    [ObservableProperty] private bool _hasReceipt;
    [ObservableProperty] private ImageSource? _currentReceiptImageSource;

    public ObservableCollection<TransactionItem> CurrentItems { get; } = new();

    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private string _itemsTotalText = string.Empty;
    [ObservableProperty] private bool _hasAmountMismatch;
    [ObservableProperty] private string _amountMismatchText = string.Empty;

    public ObservableCollection<string> TypeOptions { get; } = new() { "Расход", "Доход" };
    public ObservableRangeCollection<Category> AvailableCategories { get; } = new();

    public TransactionType CurrentType =>
        SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    public TransactionEditViewModel(DatabaseService db, IReceiptService receipts)
    {
        _db = db;
        _receipts = receipts;
        CurrentItems.CollectionChanged += (_, _) => RefreshItemsTotal();
    }

    public void InitializeForEdit(int transactionId)
    {
        _transactionId = transactionId;
    }

    partial void OnSelectedTypeIndexChanged(int value)
    {
        if (_suppressTypeChangeReload) return;
        _ = ReloadCategoriesAsync();
    }

    partial void OnAmountTextChanged(string value) => RefreshAmountMismatch();

    // ==================== Загрузка ====================

    public async Task LoadAsync()
    {
        if (_loaded) return;
        if (!_transactionId.HasValue || IsBusy) return;
        IsBusy = true;

        try
        {
            MaxDate = DateTime.Today;
            await _db.InitializeAsync();

            var tx = await _db.GetTransactionByIdAsync(_transactionId.Value);
            if (tx is null)
            {
                StatusMessage = "Операция не найдена";
                return;
            }
            _transaction = tx;

            _suppressTypeChangeReload = true;
            SelectedTypeIndex = tx.Type == TransactionType.Income ? 1 : 0;
            _suppressTypeChangeReload = false;

            await ReloadCategoriesAsync();

            SelectedCategory =
                AvailableCategories.FirstOrDefault(c => c.Id == tx.CategoryId)
                ?? AvailableCategories.FirstOrDefault(c => c.Name == tx.CategoryName)
                ?? AvailableCategories.FirstOrDefault();

            AmountText = tx.Amount.ToString("0.##", CultureInfo.InvariantCulture);
            Date = tx.Date;
            Note = tx.Note ?? string.Empty;

            _originalReceiptFileName = tx.ReceiptImagePath;
            _currentReceiptFileName = tx.ReceiptImagePath;

            if (!string.IsNullOrEmpty(tx.ReceiptImagePath))
            {
                var path = _receipts.GetFullPath(tx.ReceiptImagePath);
                if (path is not null)
                {
                    HasReceipt = true;
                    CurrentReceiptImageSource = ImageSource.FromFile(path);
                }
                else
                {
                    _currentReceiptFileName = null;
                }
            }

            CurrentItems.Clear();
            var items = await _db.GetItemsForTransactionAsync(tx.Id);
            foreach (var item in items)
            {
                item.InitializeTexts();
                CurrentItems.Add(item);
            }
            RefreshItemsTotal();

            _loaded = true;
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

    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var previousId = SelectedCategory?.Id;
            var categories = await _db.GetCategoriesAsync(CurrentType);
            AvailableCategories.ReplaceRange(categories);

            SelectedCategory = previousId.HasValue
                ? AvailableCategories.FirstOrDefault(c => c.Id == previousId.Value)
                  ?? AvailableCategories.FirstOrDefault()
                : AvailableCategories.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки категорий: {ex.Message}";
        }
    }

    // ==================== Позиции ====================

    [RelayCommand]
    private void AddItem()
    {
        CurrentItems.Add(new TransactionItem
        {
            Name = string.Empty,
            Quantity = 1m,
            UnitPrice = 0m,
        });
    }

    [RelayCommand]
    private void RemoveItem(TransactionItem? item)
    {
        if (item is null) return;
        CurrentItems.Remove(item);
    }

    public void RefreshItemsTotal()
    {
        HasItems = CurrentItems.Count > 0;

        if (!HasItems)
        {
            ItemsTotalText = string.Empty;
            RefreshAmountMismatch();
            return;
        }

        var total = CurrentItems.Sum(i => i.Total);
        ItemsTotalText = $"Сумма по позициям: {total:N0} ₽";
        RefreshAmountMismatch();
    }

    private void RefreshAmountMismatch()
    {
        if (!HasItems)
        {
            HasAmountMismatch = false;
            AmountMismatchText = string.Empty;
            return;
        }

        var normalized = (AmountText ?? string.Empty).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out var amount))
        {
            HasAmountMismatch = false;
            AmountMismatchText = string.Empty;
            return;
        }

        var itemsTotal = CurrentItems.Sum(i => i.Total);
        var diff = amount - itemsTotal;

        if (Math.Abs(diff) < 0.01m)
        {
            HasAmountMismatch = false;
            AmountMismatchText = string.Empty;
            return;
        }

        HasAmountMismatch = true;
        AmountMismatchText = diff > 0
            ? $"Позиции: {itemsTotal:N0} ₽. Сумма операции больше на {diff:N0} ₽."
            : $"Позиции: {itemsTotal:N0} ₽. Сумма операции меньше на {Math.Abs(diff):N0} ₽.";
    }

    // ==================== Фото ====================

    [RelayCommand]
    private async Task PickReceiptFromGalleryAsync()
    {
        if (IsBusy) return;
        var fileName = await _receipts.PickFromGalleryAsync();
        if (fileName is null) return;
        await SetReceiptAsync(fileName);
    }

    [RelayCommand]
    private async Task CaptureReceiptFromCameraAsync()
    {
        if (IsBusy) return;
        var fileName = await _receipts.CaptureFromCameraAsync();
        if (fileName is null) return;
        await SetReceiptAsync(fileName);
    }

    [RelayCommand]
    private async Task RemoveReceiptAsync()
    {
        if (string.IsNullOrEmpty(_currentReceiptFileName)) return;

        if (_currentReceiptFileName != _originalReceiptFileName)
            await _receipts.DeleteAsync(_currentReceiptFileName);

        _currentReceiptFileName = null;
        HasReceipt = false;
        CurrentReceiptImageSource = null;
    }

    private async Task SetReceiptAsync(string fileName)
    {
        if (!string.IsNullOrEmpty(_currentReceiptFileName)
            && _currentReceiptFileName != _originalReceiptFileName)
        {
            await _receipts.DeleteAsync(_currentReceiptFileName);
        }

        _currentReceiptFileName = fileName;
        HasReceipt = true;

        var path = _receipts.GetFullPath(fileName);
        CurrentReceiptImageSource = path is not null ? ImageSource.FromFile(path) : null;
    }

    // ==================== Сохранить / Отмена ====================

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy || _transaction is null) return;

        foreach (var item in CurrentItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                StatusMessage = "У всех позиций должно быть название";
                return;
            }
            if (item.Quantity <= 0)
            {
                StatusMessage = "У всех позиций должно быть количество больше нуля";
                return;
            }
        }

        if (SelectedCategory is null)
        {
            StatusMessage = "Выберите категорию";
            return;
        }

        if (Date.Date > DateTime.Today)
        {
            StatusMessage = "Дата операции не может быть в будущем";
            return;
        }

        IsBusy = true;
        try
        {
            _transaction.CategoryId = SelectedCategory.Id;
            _transaction.CategoryName = SelectedCategory.Name;
            _transaction.CategoryIcon = SelectedCategory.Icon;
            _transaction.Date = Date;
            _transaction.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

            if (HasItems)
            {
                var itemsTotal = CurrentItems.Sum(i => i.Total);
                if (itemsTotal > 0)
                    _transaction.Amount = itemsTotal;
            }

            if (_currentReceiptFileName != _originalReceiptFileName)
            {
                if (!string.IsNullOrEmpty(_originalReceiptFileName))
                    await _receipts.DeleteAsync(_originalReceiptFileName);

                _transaction.ReceiptImagePath = _currentReceiptFileName;
            }

            await _db.UpdateTransactionAsync(_transaction);
            await _db.ReplaceItemsForTransactionAsync(_transaction.Id, CurrentItems.ToList());

            // Оповестить остальные VM, что данные изменились
            WeakReferenceMessenger.Default.Send(new DataChangedMessage(this));

            EditCompleted?.Invoke(this, true);
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
        if (IsBusy) return;

        if (!string.IsNullOrEmpty(_currentReceiptFileName)
            && _currentReceiptFileName != _originalReceiptFileName)
        {
            await _receipts.DeleteAsync(_currentReceiptFileName);
        }

        EditCompleted?.Invoke(this, false);
    }
}