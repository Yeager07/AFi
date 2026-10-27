using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Collections;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AFi.ViewModels;

public partial class MainViewModel : ObservableObject, ITransactionFormViewModel, IRecipient<DataChangedMessage>
{
    private const int PageSize = 10;
    private const int PeriodDays = 30;

    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;
    private readonly IReceiptService _receipts;
    private readonly IFormSnapshotService _snapshots;
    private readonly ISettingsService _settings;
    private readonly IQRCodeService _qrCodeService;
    private readonly IReceiptApiService _receiptApiService;

    private int? _editingTransactionId;
    private Transaction? _editingTransaction;
    private bool _suppressTypeChangeReload;

    private int _loadedCount = PageSize;
    private int _totalInPeriod;

    private readonly HashSet<int> _selectedIds = new();

    private string? _currentReceiptFileName;
    private string? _originalReceiptFileName;

    // ==================== Управление свежестью данных ====================
    private bool _initialLoaded;
    private bool _isDirty = true;

    // ==================== Состояние формы ====================

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private int _selectedTypeIndex = 0;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private decimal _currentBalance;
    [ObservableProperty] private DateTime _maxDate = DateTime.Today;

    [ObservableProperty] private bool _isFormExpanded;
    [ObservableProperty] private bool _isInitialLoading = true;

    public bool IsEditing => _editingTransactionId.HasValue;
    public string FormTitle => IsEditing ? "Редактирование" : "Новая операция";
    public string SubmitButtonText => IsEditing ? "Обновить" : "Сохранить";
    public bool IsBalanceNegative => CurrentBalance < 0;

    public bool ShowCollapsedForm => !IsFormExpanded && !IsEditing;
    public bool ShowExpandedForm => IsFormExpanded || IsEditing;
    public bool CanCollapseForm => !IsEditing;

    // ==================== Фото чека ====================

    [ObservableProperty] private bool _hasReceipt;
    [ObservableProperty] private ImageSource? _currentReceiptImageSource;

    // ==================== Позиции чека ====================

    public ObservableCollection<TransactionItem> CurrentItems { get; } = new();

    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private string _itemsTotalText = string.Empty;
    [ObservableProperty] private bool _hasAmountMismatch;
    [ObservableProperty] private string _amountMismatchText = string.Empty;

    // ==================== Списки ====================

    public ObservableCollection<string> TypeOptions { get; } = new() { "Расход", "Доход" };
    public ObservableRangeCollection<Category> AvailableCategories { get; } = new();
    public ObservableRangeCollection<TransactionGroup> TransactionGroups { get; } = new();

    // ==================== Пагинация ====================

    [ObservableProperty] private bool _hasMoreInPeriod;
    [ObservableProperty] private string _showMoreButtonText = "Показать ещё";
    public bool HasAnyTransactions => _totalInPeriod > 0;

    // ==================== Мультивыбор ====================

    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _selectedCount;
    public bool HasSelection => SelectedCount > 0;
    public bool CanEnterSelectionMode => !IsSelectionMode && HasAnyTransactions;

    public TransactionType CurrentType => SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    // ==================== Конструктор ====================

    public MainViewModel(DatabaseService db, IDialogService dialogs,
                         IReceiptService receipts, IFormSnapshotService snapshots,
                         ISettingsService settings, IQRCodeService qrCodeService,
                         IReceiptApiService receiptApiService)
    {
        _db = db;
        _dialogs = dialogs;
        _receipts = receipts;
        _snapshots = snapshots;
        _settings = settings;
        _qrCodeService = qrCodeService;
        _receiptApiService = receiptApiService;

        CurrentItems.CollectionChanged += (_, _) => RefreshItemsTotal();
        try { CurrentBalance = _settings.GetLastBalance(); } catch { }
        WeakReferenceMessenger.Default.Register(this);
    }

    // ==================== Реакция на внешние изменения ====================

    public void Receive(DataChangedMessage message)
    {
        if (ReferenceEquals(message.Sender, this)) return;
        _isDirty = true;
    }

    // ==================== Реакции ====================

    partial void OnSelectedTypeIndexChanged(int value) { if (!_suppressTypeChangeReload) _ = ReloadCategoriesAsync(); }
    partial void OnCurrentBalanceChanged(decimal value) => OnPropertyChanged(nameof(IsBalanceNegative));
    partial void OnSelectedCountChanged(int value) => OnPropertyChanged(nameof(HasSelection));
    partial void OnIsSelectionModeChanged(bool value) => OnPropertyChanged(nameof(CanEnterSelectionMode));
    partial void OnAmountTextChanged(string value) => RefreshAmountMismatch();

    partial void OnIsFormExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCollapsedForm));
        OnPropertyChanged(nameof(ShowExpandedForm));
    }

    private void NotifyEditStateChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SubmitButtonText));
        OnPropertyChanged(nameof(ShowCollapsedForm));
        OnPropertyChanged(nameof(ShowExpandedForm));
        OnPropertyChanged(nameof(CanCollapseForm));
    }

    // ==================== Загрузка и навигация ====================

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            MaxDate = DateTime.Today;
            if (_initialLoaded && !_isDirty)
            {
                await RestoreSnapshotIfNeededAsync();
                return;
            }
            await _db.InitializeAsync();
            _loadedCount = PageSize;
            await Task.WhenAll(ReloadCategoriesAsync(), LoadTransactionsPageAsync(), ReloadBalanceAsync());
            await RestoreSnapshotIfNeededAsync();
            _isDirty = false;
            _initialLoaded = true;
        }
        catch (Exception ex) { StatusMessage = $"Ошибка загрузки: {ex.Message}"; }
        finally { IsBusy = false; IsInitialLoading = false; }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMoreInPeriod) return;
        _loadedCount += PageSize;
        await LoadTransactionsPageAsync();
    }

    [RelayCommand]
    private async Task GoToHistoryAsync() => await Shell.Current.GoToAsync("history-push");

    [RelayCommand]
    private void ExpandForm() => IsFormExpanded = true;

    [RelayCommand]
    private void CollapseForm() { if (!IsEditing) IsFormExpanded = false; }

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
        AmountMismatchText = diff > 0
            ? $"Позиции: {itemsTotal:N0} ₽. Сумма операции больше на {diff:N0} ₽."
            : $"Позиции: {itemsTotal:N0} ₽. Сумма операции меньше на {Math.Abs(diff):N0} ₽.";
    }

    // ==================== Фото чека ====================

    [RelayCommand]
    private async Task PickReceiptFromGalleryAsync()
    {
        if (IsBusy) return;
        SaveSnapshot();
        var fileName = await _receipts.PickFromGalleryAsync();
        if (fileName is not null) await SetReceiptAsync(fileName);
    }

    [RelayCommand]
    private async Task CaptureReceiptFromCameraAsync()
    {
        if (IsBusy) return;
        SaveSnapshot();
        var fileName = await _receipts.CaptureFromCameraAsync();
        if (fileName is not null) await SetReceiptAsync(fileName);
    }

    [RelayCommand]
    private async Task RemoveReceiptAsync()
    {
        if (string.IsNullOrEmpty(_currentReceiptFileName)) return;
        await _receipts.DeleteAsync(_currentReceiptFileName);
        _currentReceiptFileName = null;
        HasReceipt = false;
        CurrentReceiptImageSource = null;
    }

    // ==================== Сканирование QR и загрузка позиций ====================

    /// <summary>
    /// Спрашивает подтверждение, если в форме уже есть позиции (они будут заменены).
    /// </summary>
    private async Task<bool> ConfirmReplaceIfHasItemsAsync()
    {
        if (CurrentItems.Count == 0) return true;
        return await _dialogs.ConfirmAsync(
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
                await _dialogs.ConfirmAsync(
                    "Ошибка", "Сервер вернул пустой список позиций", "OK", "Закрыть");
                StatusMessage = "Список позиций пуст";
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ConfirmAsync("Ошибка API", ex.Message, "OK", "Закрыть");
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
        if (string.IsNullOrEmpty(_currentReceiptFileName)) { StatusMessage = "Сначала добавьте фото чека"; return; }

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
        if (string.IsNullOrEmpty(_currentReceiptFileName)) { StatusMessage = "Сначала добавьте фото чека"; return; }

        var fullPath = _receipts.GetFullPath(_currentReceiptFileName);
        if (fullPath is null) return;

        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;

        // Открываем модалку ручного выделения области QR
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

        // Открываем модалку ручного ввода реквизитов
        var manualPage = new Controls.ManualReceiptPage();
        await page.Navigation.PushModalAsync(manualPage, animated: false);
        var input = await manualPage.WaitForResultAsync();

        if (input is null) return;

        if (!await ConfirmReplaceIfHasItemsAsync()) return;

        await ApplyReceiptFromQrRawAsync(input.ToQrRaw());
    }

    private async Task SetReceiptAsync(string fileName)
    {
        if (!string.IsNullOrEmpty(_currentReceiptFileName) && _currentReceiptFileName != _originalReceiptFileName)
            await _receipts.DeleteAsync(_currentReceiptFileName);
        _currentReceiptFileName = fileName;
        HasReceipt = true;
        var path = _receipts.GetFullPath(fileName);
        CurrentReceiptImageSource = path is not null ? ImageSource.FromFile(path) : null;
    }

    [RelayCommand]
    private async Task ViewReceiptAsync(Transaction? transaction)
    {
        if (transaction?.ReceiptImageSource is not null) await _dialogs.ShowReceiptAsync(transaction.ReceiptImageSource);
    }

    private void SaveSnapshot()
    {
        var snapshot = new FormSnapshot
        {
            AmountText = AmountText, SelectedTypeIndex = SelectedTypeIndex, CategoryId = SelectedCategory?.Id,
            Date = Date, Note = Note, ReceiptFileName = _currentReceiptFileName,
            EditingTransactionId = _editingTransactionId, OriginalReceiptFileName = _originalReceiptFileName,
            Items = CurrentItems.Select(i => new ItemSnapshot { Name = i.Name, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList(),
        };
        _snapshots.Save(snapshot);
    }

    private async Task RestoreSnapshotIfNeededAsync()
    {
        if (!IsFormEmpty()) return;
        var snapshot = _snapshots.Load();
        if (snapshot is null) return;

        IsFormExpanded = true;
        _suppressTypeChangeReload = true; SelectedTypeIndex = snapshot.SelectedTypeIndex; _suppressTypeChangeReload = false;

        if (snapshot.CategoryId.HasValue)
            SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == snapshot.CategoryId.Value) ?? AvailableCategories.FirstOrDefault();

        AmountText = snapshot.AmountText; Date = snapshot.Date; Note = snapshot.Note;
        _currentReceiptFileName = snapshot.ReceiptFileName; _originalReceiptFileName = snapshot.OriginalReceiptFileName;

        if (!string.IsNullOrEmpty(snapshot.ReceiptFileName))
        {
            var path = _receipts.GetFullPath(snapshot.ReceiptFileName);
            if (path is not null) { HasReceipt = true; CurrentReceiptImageSource = ImageSource.FromFile(path); }
        }

        CurrentItems.Clear();
        foreach (var item in snapshot.Items)
        {
            var newItem = new TransactionItem { Name = item.Name, Quantity = item.Quantity, UnitPrice = item.UnitPrice };
            newItem.InitializeTexts(); CurrentItems.Add(newItem);
        }
        RefreshItemsTotal();

        if (snapshot.EditingTransactionId.HasValue)
        {
            var tx = await _db.GetTransactionByIdAsync(snapshot.EditingTransactionId.Value);
            if (tx is not null) { _editingTransaction = tx; _editingTransactionId = tx.Id; NotifyEditStateChanged(); }
        }
    }

    private bool IsFormEmpty() => string.IsNullOrWhiteSpace(AmountText) && SelectedCategory is null && CurrentItems.Count == 0 && string.IsNullOrEmpty(_currentReceiptFileName) && !_editingTransactionId.HasValue;

    // ==================== Сохранение ====================

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        bool wasEditing = IsEditing;
        if (!wasEditing && HasItems)
        {
            var itemsTotal = CurrentItems.Sum(i => i.Total);
            if (itemsTotal > 0) AmountText = itemsTotal.ToString("0.##", CultureInfo.InvariantCulture);
        }

        decimal amount;
        if (_editingTransaction is not null) { amount = _editingTransaction.Amount; }
        else
        {
            if (string.IsNullOrWhiteSpace(AmountText)) { StatusMessage = "Введите сумму"; return; }
            var normalized = AmountText.Replace(',', '.');
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) { StatusMessage = "Сумма должна быть числом"; return; }
            if (amount <= 0) { StatusMessage = "Сумма должна быть > 0"; return; }
        }

        if (SelectedCategory is null) { StatusMessage = "Выберите категорию"; return; }
        if (Date.Date > DateTime.Today) { StatusMessage = "Дата не может быть в будущем"; return; }

        foreach (var item in CurrentItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) { StatusMessage = "У позиций должно быть название"; return; }
            if (item.Quantity <= 0) { StatusMessage = "Кол-во должно быть > 0"; return; }
        }

        IsBusy = true;
        try
        {
            int transactionId;
            decimal? itemsTotal = null;
            if (HasItems)
            {
                itemsTotal = CurrentItems.Sum(i => i.Total);
                if (itemsTotal > 0) AmountText = itemsTotal.Value.ToString("0.##", CultureInfo.InvariantCulture);
            }

            if (_editingTransaction is not null)
            {
                _editingTransaction.CategoryId = SelectedCategory.Id; _editingTransaction.CategoryName = SelectedCategory.Name;
                _editingTransaction.CategoryIcon = SelectedCategory.Icon; _editingTransaction.Date = Date;
                _editingTransaction.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
                if (itemsTotal.HasValue && itemsTotal.Value > 0) _editingTransaction.Amount = itemsTotal.Value;
                if (_currentReceiptFileName != _originalReceiptFileName)
                {
                    if (!string.IsNullOrEmpty(_originalReceiptFileName)) await _receipts.DeleteAsync(_originalReceiptFileName);
                    _editingTransaction.ReceiptImagePath = _currentReceiptFileName;
                }
                await _db.UpdateTransactionAsync(_editingTransaction);
                transactionId = _editingTransaction.Id;
                _editingTransactionId = null; _editingTransaction = null; _originalReceiptFileName = null; _currentReceiptFileName = null;
                NotifyEditStateChanged(); StatusMessage = "Операция обновлена";
            }
            else
            {
                var transaction = new Transaction
                {
                    Amount = amount, Type = CurrentType, CategoryId = SelectedCategory.Id,
                    CategoryName = SelectedCategory.Name, CategoryIcon = SelectedCategory.Icon,
                    Date = Date, Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                    CreatedAt = DateTime.Now, ReceiptImagePath = _currentReceiptFileName,
                };
                await _db.AddTransactionAsync(transaction);
                transactionId = transaction.Id;
                _currentReceiptFileName = null; _originalReceiptFileName = null;
                StatusMessage = "Операция сохранена";
            }

            await _db.ReplaceItemsForTransactionAsync(transactionId, CurrentItems.ToList());
            ResetForm();
            if (wasEditing) IsFormExpanded = false;
            _loadedCount = PageSize;
            await LoadTransactionsPageAsync();
            await ReloadBalanceAsync();
            WeakReferenceMessenger.Default.Send(new DataChangedMessage(this));
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ==================== Редактирование ====================

    [RelayCommand]
    private async Task StartEditAsync(Transaction? transaction)
    {
        if (transaction is null) return;
        _editingTransaction = transaction;
        _editingTransactionId = transaction.Id;
        IsFormExpanded = true;

        _suppressTypeChangeReload = true; SelectedTypeIndex = transaction.Type == TransactionType.Income ? 1 : 0; _suppressTypeChangeReload = false;
        await ReloadCategoriesAsync();
        SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Id == transaction.CategoryId) ?? AvailableCategories.FirstOrDefault(c => c.Name == transaction.CategoryName) ?? AvailableCategories.FirstOrDefault();
        AmountText = transaction.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        Date = transaction.Date; Note = transaction.Note ?? string.Empty;
        _originalReceiptFileName = transaction.ReceiptImagePath; _currentReceiptFileName = transaction.ReceiptImagePath;

        if (!string.IsNullOrEmpty(transaction.ReceiptImagePath))
        {
            var path = _receipts.GetFullPath(transaction.ReceiptImagePath);
            if (path is not null) { HasReceipt = true; CurrentReceiptImageSource = ImageSource.FromFile(path); }
            else { HasReceipt = false; CurrentReceiptImageSource = null; _currentReceiptFileName = null; }
        }
        else { HasReceipt = false; CurrentReceiptImageSource = null; }

        CurrentItems.Clear();
        var items = await _db.GetItemsForTransactionAsync(transaction.Id);
        foreach (var item in items) { item.InitializeTexts(); CurrentItems.Add(item); }
        RefreshItemsTotal(); NotifyEditStateChanged();
        StatusMessage = $"Редактирование от {transaction.Date:dd.MM.yyyy}";
    }

    [RelayCommand]
    private async Task CancelEditAsync()
    {
        _snapshots.Clear();
        if (!string.IsNullOrEmpty(_currentReceiptFileName) && _currentReceiptFileName != _originalReceiptFileName)
            await _receipts.DeleteAsync(_currentReceiptFileName);

        _editingTransactionId = null; _editingTransaction = null; _currentReceiptFileName = null; _originalReceiptFileName = null;
        AmountText = string.Empty; Note = string.Empty; StatusMessage = string.Empty; HasReceipt = false; CurrentReceiptImageSource = null;
        CurrentItems.Clear(); IsFormExpanded = false; NotifyEditStateChanged();
    }

    // ==================== Удаление ====================

    [RelayCommand]
    private async Task DeleteTransactionAsync(Transaction? transaction) => await DeleteTransactionCoreAsync(transaction, skipConfirmation: false);

    private async Task DeleteTransactionCoreAsync(Transaction? transaction, bool skipConfirmation)
    {
        if (transaction is null || IsBusy) return;
        if (!skipConfirmation)
        {
            var sign = transaction.Type == TransactionType.Income ? "+" : "−";
            var message = $"Удалить операцию?\n\n{sign}{transaction.Amount:N0} ₽ · {transaction.CategoryName}\n{transaction.Date:dd.MM.yyyy}";
            if (!await _dialogs.ConfirmAsync("Удаление", message, "Удалить", "Отмена")) return;
        }

        IsBusy = true;
        try
        {
            if (!string.IsNullOrEmpty(transaction.ReceiptImagePath)) await _receipts.DeleteAsync(transaction.ReceiptImagePath);
            await _db.DeleteTransactionAsync(transaction.Id);
            if (_editingTransactionId == transaction.Id) await CancelEditAsync();
            _loadedCount = PageSize; await LoadTransactionsPageAsync(); await ReloadBalanceAsync();
            StatusMessage = "Операция удалена";
            WeakReferenceMessenger.Default.Send(new DataChangedMessage(this));
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ==================== Тап по карточке ====================

    [RelayCommand]
    private async Task HandleCardTapAsync(Transaction? transaction)
    {
        if (transaction is null) return;

        if (IsSelectionMode)
        {
            ToggleSelection(transaction);
            return;
        }

        var sign = transaction.Type == TransactionType.Income ? "+" : "−";
        var title = $"{sign}{transaction.Amount:N0} ₽ · {transaction.CategoryName}";

        var choice = await _dialogs.ShowActionSheetAsync(
            title,
            "Отмена",
            "Подробнее",
            "Редактировать",
            "Удалить");

        if (choice == "Подробнее")
        {
            await ShowDetailsAsync(transaction);
        }
        else if (choice == "Редактировать")
        {
            await StartEditAsync(transaction);
        }
        else if (choice == "Удалить")
        {
            await DeleteTransactionCoreAsync(transaction, skipConfirmation: false);
        }
    }

    private async Task ShowDetailsAsync(Transaction transaction)
    {
        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return;

        var detailsPage = new Controls.TransactionDetailsPage(transaction);
        await page.Navigation.PushModalAsync(detailsPage);
        await detailsPage.WaitForCloseAsync();
    }

    // ==================== Мультивыбор ====================

    [RelayCommand]
    private void EnterSelectionMode() { if (HasAnyTransactions) IsSelectionMode = true; }

    [RelayCommand]
    private void ToggleSelection(Transaction? transaction)
    {
        if (transaction is null || !IsSelectionMode) return;
        if (_selectedIds.Contains(transaction.Id)) { _selectedIds.Remove(transaction.Id); transaction.IsSelected = false; }
        else { _selectedIds.Add(transaction.Id); transaction.IsSelected = true; }
        SelectedCount = _selectedIds.Count;
    }

    [RelayCommand]
    private void CancelSelection()
    {
        foreach (var group in TransactionGroups) foreach (var t in group) t.IsSelected = false;
        _selectedIds.Clear(); SelectedCount = 0; IsSelectionMode = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0 || IsBusy) return;
        var count = _selectedIds.Count;
        var word = Plural(count, "операцию", "операции", "операций");
        if (!await _dialogs.ConfirmAsync("Удаление", $"Удалить {count} {word}?\n\nЭто действие нельзя отменить.", "Удалить", "Отмена")) return;

        IsBusy = true;
        try
        {
            foreach (var id in _selectedIds.ToList())
            {
                var transaction = TransactionGroups.SelectMany(g => g).FirstOrDefault(t => t.Id == id);
                if (transaction is not null && !string.IsNullOrEmpty(transaction.ReceiptImagePath))
                    await _receipts.DeleteAsync(transaction.ReceiptImagePath);
                await _db.DeleteTransactionAsync(id);
            }
            _selectedIds.Clear(); SelectedCount = 0; IsSelectionMode = false;
            _loadedCount = PageSize; await LoadTransactionsPageAsync(); await ReloadBalanceAsync();
            StatusMessage = $"Удалено {count} {word}";
            WeakReferenceMessenger.Default.Send(new DataChangedMessage(this));
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ==================== Вспомогательные ====================

    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var previousName = SelectedCategory?.Name;
            var categories = await _db.GetCategoriesAsync(CurrentType);
            AvailableCategories.ReplaceRange(categories);
            SelectedCategory = AvailableCategories.FirstOrDefault(c => c.Name == previousName) ?? AvailableCategories.FirstOrDefault();
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
    }

    private async Task LoadTransactionsPageAsync()
    {
        try
        {
            var to = DateTime.Today; var from = to.AddDays(-(PeriodDays - 1));
            var toShow = await _db.GetTransactionsAsync(from, to, null, limit: _loadedCount, offset: 0);
            _totalInPeriod = await _db.GetTransactionsCountAsync(from, to);

            var ids = toShow.Select(t => t.Id).ToList();
            var itemsByTransaction = await _db.GetItemsForTransactionsAsync(ids);

            foreach (var t in toShow)
            {
                if (itemsByTransaction.TryGetValue(t.Id, out var items))
                {
                    var collection = new ObservableCollection<TransactionItem>();
                    foreach (var item in items) collection.Add(item);
                    t.Items = collection;
                }
                if (!string.IsNullOrEmpty(t.ReceiptImagePath))
                {
                    var path = _receipts.GetFullPath(t.ReceiptImagePath);
                    if (path is not null) t.ReceiptImageSource = ImageSource.FromFile(path);
                }
            }

            var groups = toShow.GroupBy(t => t.Date.Date).OrderByDescending(g => g.Key).Select(g => new TransactionGroup(g.Key, g.ToList())).ToList();
            TransactionGroups.ReplaceRange(groups);

            HasMoreInPeriod = _loadedCount < _totalInPeriod;
            ShowMoreButtonText = HasMoreInPeriod ? "Показать ещё" : "Вся история";
            OnPropertyChanged(nameof(HasAnyTransactions)); OnPropertyChanged(nameof(CanEnterSelectionMode));
            _selectedIds.Clear(); SelectedCount = 0; IsSelectionMode = false;
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
    }

    private async Task ReloadBalanceAsync()
    {
        try
        {
            var operationsBalance = await _db.GetCurrentBalanceAsync();
            var initialBalance = _settings.GetInitialBalance();
            CurrentBalance = initialBalance + operationsBalance;
            _settings.SetLastBalance(CurrentBalance);
        }
        catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
    }

    private void ResetForm()
    {
        AmountText = string.Empty; Note = string.Empty; CurrentItems.Clear();
        HasReceipt = false; CurrentReceiptImageSource = null;
        _currentReceiptFileName = null; _originalReceiptFileName = null;
        _snapshots.Clear();
    }

    private static string Plural(int count, string one, string few, string many)
    {
        int mod100 = count % 100; int mod10 = count % 10;
        if (mod100 >= 11 && mod100 <= 14) return many;
        if (mod10 == 1) return one;
        if (mod10 >= 2 && mod10 <= 4) return few;
        return many;
    }
}