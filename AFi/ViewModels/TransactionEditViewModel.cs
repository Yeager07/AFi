using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Collections;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AFi.ViewModels;

/// <summary>
/// ViewModel модальной формы редактирования существующей операции.
/// Используется со страницы Истории. Сумма и тип операции immutable;
/// если у операции есть позиции — сумма пересчитывается из позиций.
/// </summary>
public partial class TransactionEditViewModel : ObservableObject, ITransactionFormViewModel
{
    private readonly DatabaseService _db;
    private readonly IReceiptService _receipts;
    private readonly IQRCodeService _qrCodeService;
    private readonly IReceiptApiService _receiptApiService;
    private readonly IOcrService _ocrService; // ← Новый сервис

    private int? _transactionId;
    private Transaction? _transaction;
    private bool _suppressTypeChangeReload;
    private bool _loaded;

    private string? _currentReceiptFileName;
    private string? _originalReceiptFileName;

    /// <summary>
    /// true — сохранено, false — отменено.
    /// </summary>
    public event EventHandler<bool>? EditCompleted;

    // ==================== Состояние формы ====================

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private int _selectedTypeIndex;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private DateTime _maxDate = DateTime.Today;

    public bool IsEditing => true;

    // ==================== Фото ====================

    [ObservableProperty] private bool _hasReceipt;
    [ObservableProperty] private ImageSource? _currentReceiptImageSource;

    // ==================== Позиции ====================

    public ObservableCollection<TransactionItem> CurrentItems { get; } = new();

    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private string _itemsTotalText = string.Empty;
    [ObservableProperty] private bool _hasAmountMismatch;
    [ObservableProperty] private string _amountMismatchText = string.Empty;

    // ==================== Списки ====================

    public ObservableCollection<string> TypeOptions { get; } = new() { "Расход", "Доход" };
    public ObservableRangeCollection<Category> AvailableCategories { get; } = new();

    public TransactionType CurrentType => SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    // ==================== Конструктор ====================

    public TransactionEditViewModel(DatabaseService db, IReceiptService receipts,
                                    IQRCodeService qrCodeService, IReceiptApiService receiptApiService,
                                    IOcrService ocrService)
    {
        _db = db;
        _receipts = receipts;
        _qrCodeService = qrCodeService;
        _receiptApiService = receiptApiService;
        _ocrService = ocrService;
        CurrentItems.CollectionChanged += (_, _) => RefreshItemsTotal();
    }

    public void InitializeForEdit(int transactionId) => _transactionId = transactionId;

    // ==================== Реакции ====================

    partial void OnSelectedTypeIndexChanged(int value) { if (!_suppressTypeChangeReload) _ = ReloadCategoriesAsync(); }
    partial void OnAmountTextChanged(string value) => RefreshAmountMismatch();

    // ==================== Загрузка ====================

    public async Task LoadAsync()
    {
        if (_loaded || !_transactionId.HasValue || IsBusy) return;
        IsBusy = true;

        try
        {
            MaxDate = DateTime.Today;
            await _db.InitializeAsync();

            var tx = await _db.GetTransactionByIdAsync(_transactionId.Value);
            if (tx is null) { StatusMessage = "Не найдена"; return; }
            _transaction = tx;

            _suppressTypeChangeReload = true; SelectedTypeIndex = tx.Type == TransactionType.Income ? 1 : 0; _suppressTypeChangeReload = false;
            await ReloadCategoriesAsync();
            SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == tx.CategoryId) ?? AvailableCategories.FirstOrDefault(c => c.Name == tx.CategoryName) ?? AvailableCategories.FirstOrDefault();

            AmountText = tx.Amount.ToString("0.##", CultureInfo.InvariantCulture);
            Date = tx.Date; Note = tx.Note ?? string.Empty;
            _originalReceiptFileName = tx.ReceiptImagePath; _currentReceiptFileName = tx.ReceiptImagePath;

            if (!string.IsNullOrEmpty(tx.ReceiptImagePath))
            {
                var path = _receipts.GetFullPath(tx.ReceiptImagePath);
                if (path is not null) { HasReceipt = true; CurrentReceiptImageSource = ImageSource.FromFile(path); }
                else _currentReceiptFileName = null;
            }

            CurrentItems.Clear();
            var items = await _db.GetItemsForTransactionAsync(tx.Id);
            foreach (var item in items) { item.InitializeTexts(); CurrentItems.Add(item); }
            RefreshItemsTotal();
            _loaded = true;
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var previousId = SelectedCategory?.Id;
            var categories = await _db.GetCategoriesAsync(CurrentType);
            AvailableCategories.ReplaceRange(categories);
            SelectedCategory = previousId.HasValue ? AvailableCategories.FirstOrDefault(c => c.Id == previousId.Value) ?? AvailableCategories.FirstOrDefault() : AvailableCategories.FirstOrDefault();
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
    }

    // ==================== Позиции ====================

    [RelayCommand]
    private void AddItem() => CurrentItems.Add(new TransactionItem { Name = string.Empty, Quantity = 1m, UnitPrice = 0m });

    [RelayCommand]
    private void RemoveItem(TransactionItem? item) { if (item is not null) CurrentItems.Remove(item); }

    public void RefreshItemsTotal()
    {
        HasItems = CurrentItems.Count > 0;
        if (!HasItems) { ItemsTotalText = string.Empty; RefreshAmountMismatch(); return; }
        var total = CurrentItems.Sum(i => i.Total);
        ItemsTotalText = $"Сумма по позициям: {total:N0} ₽";
        RefreshAmountMismatch();
    }

    private void RefreshAmountMismatch()
    {
        if (!HasItems) { HasAmountMismatch = false; AmountMismatchText = string.Empty; return; }
        var normalized = (AmountText ?? string.Empty).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            HasAmountMismatch = false; AmountMismatchText = string.Empty; return;
        }
        var itemsTotal = CurrentItems.Sum(i => i.Total);
        var diff = amount - itemsTotal;
        if (Math.Abs(diff) < 0.01m) { HasAmountMismatch = false; AmountMismatchText = string.Empty; return; }
        HasAmountMismatch = true;
        AmountMismatchText = diff > 0 ? $"Позиции: {itemsTotal:N0} ₽. Сумма операции больше на {diff:N0} ₽." : $"Позиции: {itemsTotal:N0} ₽. Сумма операции меньше на {Math.Abs(diff):N0} ₽.";
    }

    // ==================== Фото ====================

    [RelayCommand]
    private async Task PickReceiptFromGalleryAsync()
    {
        if (IsBusy) return;
        var fileName = await _receipts.PickFromGalleryAsync();
        if (fileName is not null) await SetReceiptAsync(fileName);
    }

    [RelayCommand]
    private async Task CaptureReceiptFromCameraAsync()
    {
        if (IsBusy) return;
        var fileName = await _receipts.CaptureFromCameraAsync();
        if (fileName is not null) await SetReceiptAsync(fileName);
    }

    [RelayCommand]
    private async Task RemoveReceiptAsync()
    {
        if (string.IsNullOrEmpty(_currentReceiptFileName)) return;
        if (_currentReceiptFileName != _originalReceiptFileName) await _receipts.DeleteAsync(_currentReceiptFileName);
        _currentReceiptFileName = null; HasReceipt = false; CurrentReceiptImageSource = null;
    }

    // ==================== Сканирование QR и загрузка позиций ====================

    /// <summary>
    /// Спрашивает подтверждение, если в форме уже есть позиции (они будут заменены).
    /// </summary>
    private async Task<bool> ConfirmReplaceIfHasItemsAsync()
    {
        if (CurrentItems.Count == 0) return true;
        return await Shell.Current.DisplayAlertAsync(
            "Внимание",
            "Текущие позиции будут заменены данными из нового чека. Продолжить?",
            "Да", "Отмена");
    }

    /// <summary>
    /// Загружает чек по qrraw-строке (полученной из QR-кода или собранной
    /// вручную из реквизитов) и заполняет список позиций.
    /// </summary>
    private async Task ApplyReceiptFromQrRawAsync(string qrRaw)
    {
        IsBusy = true;
        StatusMessage = "Загрузка данных из ФНС...";
        try
        {
            var receiptData = await _receiptApiService.GetReceiptDataAsync(qrRaw);

            // Pattern matching: проверяем, что Items не null и не пуст
            if (receiptData?.Items is { Count: > 0 } items)
            {
                CurrentItems.Clear();
                foreach (var item in items)
                {
                    var newItem = new TransactionItem
                    {
                        Name = item.Name,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price / 100m,
                    };
                    newItem.InitializeTexts();
                    CurrentItems.Add(newItem);
                }
                RefreshItemsTotal();
                StatusMessage = $"Загружено {items.Count} позиций из чека";
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Ошибка", "Сервер вернул пустой список позиций", "OK");
                StatusMessage = "Список позиций пуст";
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка API", ex.Message, "OK");
            StatusMessage = "Ошибка получения данных";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScanQRCodeAsync()
    {
        if (string.IsNullOrEmpty(_currentReceiptFileName)) { StatusMessage = "Сначала добавьте фото"; return; }

        if (!await ConfirmReplaceIfHasItemsAsync()) return;

        var fullPath = _receipts.GetFullPath(_currentReceiptFileName);
        if (fullPath is null) return;

        StatusMessage = "Сканирование QR-кода...";
        string? qrRaw = null;
        try
        {
            qrRaw = await _qrCodeService.DecodeFiscalQRCodeAsync(fullPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка QR: {ex.Message}";
            return;
        }

        if (string.IsNullOrEmpty(qrRaw))
        {
            StatusMessage = "QR-код не найден. Попробуйте «Выделить QR» или «Ввести реквизиты»";
            return;
        }

        await ApplyReceiptFromQrRawAsync(qrRaw);
    }

    [RelayCommand]
    private async Task ScanQRCodeManualAsync()
    {
        if (string.IsNullOrEmpty(_currentReceiptFileName)) { StatusMessage = "Сначала добавьте фото"; return; }

        var fullPath = _receipts.GetFullPath(_currentReceiptFileName);
        if (fullPath is null) return;

        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;

        var cropPage = new Controls.QrCropPage(fullPath);
        await page.Navigation.PushModalAsync(cropPage, animated: false);
        var qrRaw = await cropPage.WaitForResultAsync();

        if (string.IsNullOrEmpty(qrRaw))
        {
            StatusMessage = "QR-код не распознан в выделенной области";
            return;
        }

        if (!await ConfirmReplaceIfHasItemsAsync()) return;

        await ApplyReceiptFromQrRawAsync(qrRaw);
    }

    [RelayCommand]
    private async Task EnterReceiptDataManualAsync()
    {
        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;

        var manualPage = new Controls.ManualReceiptPage();
        await page.Navigation.PushModalAsync(manualPage, animated: false);
        var input = await manualPage.WaitForResultAsync();

        if (input is null) return;

        if (!await ConfirmReplaceIfHasItemsAsync()) return;

        await ApplyReceiptFromQrRawAsync(input.ToQrRaw());
    }

    // ==================== OCR ====================

    [RelayCommand]
    private async Task RecognizeReceiptWithOcrAsync()
    {
        if (string.IsNullOrEmpty(_currentReceiptFileName))
        {
            StatusMessage = "Сначала добавьте фото чека";
            return;
        }

        if (!await ConfirmReplaceIfHasItemsAsync()) return;

        var fullPath = _receipts.GetFullPath(_currentReceiptFileName);
        if (fullPath is null) return;

        IsBusy = true;
        StatusMessage = "Распознавание текста (OCR)...";

        try
        {
            var result = await _ocrService.RecognizeReceiptAsync(fullPath);

            if (result?.Items is { Count: > 0 } items)
            {
                CurrentItems.Clear();
                foreach (var item in items)
                {
                    var newItem = new TransactionItem
                    {
                        Name = item.Name,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price,
                    };
                    newItem.InitializeTexts();
                    CurrentItems.Add(newItem);
                }
                RefreshItemsTotal();
                StatusMessage = $"Распознано {items.Count} позиций (OCR)";
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("OCR", "Не удалось распознать позиции. Попробуйте сфотографировать чек ровнее.", "OK");
                StatusMessage = "OCR не нашел позиций на чеке";
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Ошибка OCR", ex.Message, "OK");
            StatusMessage = "Ошибка распознавания";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Прочее ====================

    private async Task SetReceiptAsync(string fileName)
    {
        if (!string.IsNullOrEmpty(_currentReceiptFileName) && _currentReceiptFileName != _originalReceiptFileName)
            await _receipts.DeleteAsync(_currentReceiptFileName);
        _currentReceiptFileName = fileName; HasReceipt = true;
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
            if (string.IsNullOrWhiteSpace(item.Name)) { StatusMessage = "У позиций должно быть название"; return; }
            if (item.Quantity <= 0) { StatusMessage = "Кол-во > 0"; return; }
        }
        if (SelectedCategory is null) { StatusMessage = "Выберите категорию"; return; }
        if (Date.Date > DateTime.Today) { StatusMessage = "Дата в будущем"; return; }

        IsBusy = true;
        try
        {
            _transaction.CategoryId = SelectedCategory.Id; _transaction.CategoryName = SelectedCategory.Name;
            _transaction.CategoryIcon = SelectedCategory.Icon; _transaction.Date = Date;
            _transaction.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

            if (HasItems)
            {
                var itemsTotal = CurrentItems.Sum(i => i.Total);
                if (itemsTotal > 0) _transaction.Amount = itemsTotal;
            }

            if (_currentReceiptFileName != _originalReceiptFileName)
            {
                if (!string.IsNullOrEmpty(_originalReceiptFileName)) await _receipts.DeleteAsync(_originalReceiptFileName);
                _transaction.ReceiptImagePath = _currentReceiptFileName;
            }

            await _db.UpdateTransactionAsync(_transaction);
            await _db.ReplaceItemsForTransactionAsync(_transaction.Id, CurrentItems.ToList());
            WeakReferenceMessenger.Default.Send(new DataChangedMessage(this));
            EditCompleted?.Invoke(this, true);
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsBusy) return;
        if (!string.IsNullOrEmpty(_currentReceiptFileName) && _currentReceiptFileName != _originalReceiptFileName)
            await _receipts.DeleteAsync(_currentReceiptFileName);
        EditCompleted?.Invoke(this, false);
    }
}