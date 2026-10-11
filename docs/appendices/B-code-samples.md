# Приложение B. Ключевые листинги кода

В этом приложении собраны фрагменты кода, иллюстрирующие ключевые 
архитектурные решения проекта. Полный код доступен в GitHub-репозитории.

---

## B.1. Модели данных (денормализация)

**Файл:** `Models/Transaction.cs` (фрагмент)

Модель операции. Обратите внимание на **денормализацию**: 
`CategoryName` и `CategoryIcon` хранятся прямо в операции, 
чтобы не терять информацию при удалении категории.

```csharp
[Table("transactions")]
public partial class Transaction : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public decimal Amount { get; set; }                    // decimal, не double — точность
    public TransactionType Type { get; set; }
    public int? CategoryId { get; set; }                   // nullable на случай удаления
    public string CategoryName { get; set; } = string.Empty; // денормализация
    public string CategoryIcon { get; set; } = string.Empty; // денормализация
    public DateTime Date { get; set; }
    public string? Note { get; set; }
    public string? ReceiptImagePath { get; set; }          // имя файла фото чека
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // UI-флаги — не сохраняются в БД
    [Ignore] public bool IsSelected { get; set; }
    [Ignore] public bool IsExpanded { get; set; }

    // Позиции чека — ленивая загрузка
    [Ignore]
    public ObservableCollection<TransactionItem> Items { get; set; } = new();
}
```

**Что демонстрирует:** понимание компромисса между нормализацией 
и производительностью. Денормализация оправдана, потому что 
пользователь должен видеть имя категории даже после её удаления.

---

## B.2. Сервис БД — фасад над SQLite

**Файл:** `Services/DatabaseService.cs` (сигнатуры методов)

Все операции с SQLite инкапсулированы в одном сервисе. ViewModel 
работает только через этот фасад — она не знает про `SQLiteAsyncConnection`.

```csharp
public class DatabaseService
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    // Инициализация с защитой от параллельных вызовов
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            _connection = new SQLiteAsyncConnection(DatabasePath, ...);
            await _connection.CreateTableAsync<Category>();
            await _connection.CreateTableAsync<Transaction>();
            await _connection.CreateTableAsync<TransactionItem>();
            await _connection.CreateTableAsync<RecurringPayment>();

            await SeedDefaultCategoriesAsync();
            await MigrateIconsAsync();      // миграция для старых БД
            _initialized = true;
        }
        finally { _initLock.Release(); }
    }

    // ==================== Транзакции ====================
    public async Task<int> AddTransactionAsync(Transaction transaction);
    public async Task<int> UpdateTransactionAsync(Transaction transaction);
    public async Task<int> DeleteTransactionAsync(int id);   // + удаление позиций и фото
    public async Task<List<Transaction>> GetTransactionsAsync(...);
    public async Task<(decimal Income, decimal Expense)> GetSummaryAsync(...);
    public async Task<decimal> GetCurrentBalanceAsync();

    // ==================== Категории ====================
    public async Task<List<Category>> GetCategoriesAsync(TransactionType type);
    public async Task<int> AddCategoryAsync(Category category);
    public async Task DeleteCategoryAsync(int categoryId);   // + обнуление CategoryId

    // ==================== Позиции чека ====================
    public async Task<Dictionary<int, List<TransactionItem>>> GetItemsForTransactionsAsync(
        List<int> transactionIds);                            // N+1 → один запрос
    public async Task ReplaceItemsForTransactionAsync(int id, List<TransactionItem> items);

    // ==================== Регулярные платежи ====================
    public async Task<List<RecurringPayment>> GetRecurringPaymentsAsync();
    public async Task<int> AddRecurringPaymentAsync(RecurringPayment payment);
}
```

**Что демонстрирует:** паттерн **Facade** + **Repository**. Сервис 
скрывает детали SQLite. `SemaphoreSlim` в `InitializeAsync` защищает 
от гонки при параллельных вызовах.

**Оптимизация:** `GetItemsForTransactionsAsync` использует один SQL-запрос 
с `IN (...)` вместо N+1 запросов для списка операций.

---

## B.3. Тематизация — сердце оформления

**Файл:** `Services/ThemeService.cs` (полностью, сокращённо)

Сервис хранит 5 палитр цветов прямо в C# и применяет их через 
`Application.Current.Resources`. Это решение появилось после того, 
как XAML-словари не заработали из-за `MauiXamlInflator = SourceGen`.

```csharp
public class ThemeService : IThemeService
{
    public string CurrentThemeName { get; private set; } = "Light";
    public event EventHandler? ThemeChanged;

    // 5 палитр — по 11 цветовых ролей в каждой
    private static readonly Dictionary<string, string> LightTheme = new()
    {
        ["PrimaryColor"]         = "#512BD4",
        ["PrimaryTextColor"]     = "#FFFFFF",
        ["PageBackgroundColor"]  = "#F3F3F3",
        ["CardBackgroundColor"]  = "#FFFFFF",
        ["CardBorderColor"]      = "#D0D0D0",
        ["TextPrimaryColor"]     = "#000000",
        ["TextSecondaryColor"]   = "#808080",
        ["SuccessColor"]         = "#2E7D32",
        ["DangerColor"]          = "#D32F2F",
        ["WarningColor"]         = "#FFB300",
        ["DividerColor"]         = "#E0E0E0",
    };

    private static readonly Dictionary<string, string> SpaceTheme = new()
    {
        // Палитра в стиле Interstellar / NASA
        ["PrimaryColor"]         = "#64B5F6",   // холодный голубой
        ["PageBackgroundColor"]  = "#0B1426",   // глубокий тёмно-синий
        ["CardBackgroundColor"]  = "#16233D",
        ["TextPrimaryColor"]     = "#E8EFF7",   // почти белый
        // ...
    };

    // Аналогично: DarkTheme, HalloweenTheme, ArcadeTheme

    private static readonly Dictionary<string, Dictionary<string, string>> AllThemes = new()
    {
        ["Light"] = LightTheme, ["Dark"] = DarkTheme,
        ["Space"] = SpaceTheme, ["Halloween"] = HalloweenTheme,
        ["Arcade"] = ArcadeTheme,
    };

    public string ApplyTheme(string themeName)
    {
        var theme = GetTheme(themeName);
        var res = Application.Current!.Resources;

        foreach (var kvp in theme)
        {
            var color = Color.FromArgb(kvp.Value);

            if (res.ContainsKey(kvp.Key))
                res[kvp.Key] = color;         // обновляем — UI перекрасится
            else
                res.Add(kvp.Key, color);
        }

        CurrentThemeName = themeName;
        Preferences.Set("app_theme", themeName);
        ThemeChanged?.Invoke(this, EventArgs.Empty);   // уведомляем подписчиков

        return "OK";
    }
}
```

**Что демонстрирует:** 
- Паттерн **Strategy** — 5 реализаций одной абстракции.
- **Динамическая смена темы** — через `Application.Current.Resources` 
  и `DynamicResource` в XAML.
- **Сохранение в Preferences** — тема остаётся между запусками.
- **Событие `ThemeChanged`** — для тех, кому нужно перестроиться 
  (например, перерисовать диаграммы).

---

## B.4. Прогноз бюджета — гибридный алгоритм

**Файл:** `Services/ForecastService.cs` (метод `CalculateAsync`)

Реализация алгоритма «частотный фильтр + медиана». Отсекает разовые 
траты, оставляет только регулярные.

```csharp
public async Task<ForecastResult> CalculateAsync()
{
    var today = DateTime.Today;
    var firstOfCurrentMonth = new DateTime(today.Year, today.Month, 1);

    // 3 последних ПОЛНЫХ месяца (текущий неполный не учитываем)
    var periodFrom = firstOfCurrentMonth.AddMonths(-3);
    var periodTo = firstOfCurrentMonth.AddDays(-1);

    var transactions = await _db.GetTransactionsAsync(periodFrom, periodTo);
    var expenses = transactions.Where(t => t.Type == TransactionType.Expense).ToList();

    var result = new ForecastResult { PeriodFrom = periodFrom, PeriodTo = periodTo };
    if (expenses.Count == 0) return result;

    // Группируем по категориям
    foreach (var group in expenses.GroupBy(t => t.CategoryName))
    {
        var monthlySums = new List<decimal>();
        int monthsPresent = 0;

        // Считаем сумму по каждому из 3 месяцев
        for (int i = 0; i < 3; i++)
        {
            var monthStart = periodFrom.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var monthSum = group
                .Where(t => t.Date.Date >= monthStart.Date
                         && t.Date.Date <= monthEnd.Date)
                .Sum(t => t.Amount);

            if (monthSum > 0) { monthlySums.Add(monthSum); monthsPresent++; }
        }

        // Применяем правило
        decimal predicted = 0;
        bool included = false;

        if (monthsPresent == 3)
        {
            // 3 месяца — медиана (устойчиво к выбросам)
            var sorted = monthlySums.OrderBy(x => x).ToList();
            predicted = sorted[1];                 // середина из трёх
            included = true;
        }
        else if (monthsPresent == 2)
        {
            // 2 месяца — среднее (медиана из двух = среднее)
            predicted = monthlySums.Average();
            included = true;
        }
        // 1 месяц — не включаем (разовая трата)

        result.Items.Add(new ForecastItem
        {
            CategoryName = group.Key,
            Icon = group.First().CategoryIcon,
            MonthsPresent = monthsPresent,
            PredictedSum = predicted,
            IsIncluded = included,
        });
    }

    result.TotalPredicted = result.Items
        .Where(i => i.IsIncluded)
        .Sum(i => i.PredictedSum);

    return result;
}
```

**Что демонстрирует:**
- **Практическое применение статистики** — медиана вместо среднего 
  для устойчивости к выбросам.
- **Работа с временными периодами** — корректный расчёт границ месяцев.
- **Простая эвристика вместо ML** — для 3 точек данных ML избыточен.

**Пример работы:** если категория «Закупки» имела суммы 10 000, 
12 000 и 80 000 ₽ (в одном месяце купили дорогое оборудование), 
среднее = 34 000 ₽ (завышено), медиана = 12 000 ₽ (реалистично).

---

## B.5. Команда Save с валидацией и позициями

**Файл:** `ViewModels/MainViewModel.cs` (фрагмент `SaveAsync`)

Метод показывает многослойную валидацию и логику «сумма = сумма позиций».

```csharp
[RelayCommand]
private async Task SaveAsync()
{
    if (IsBusy) return;

    // ==================== Валидация ====================
    if (string.IsNullOrWhiteSpace(AmountText))
    {
        StatusMessage = "Введите сумму или добавьте позиции";
        return;
    }

    // Нормализация десятичного разделителя (запятая / точка)
    var normalized = AmountText.Replace(',', '.');
    if (!decimal.TryParse(normalized, NumberStyles.Number,
                          CultureInfo.InvariantCulture, out var amount) || amount <= 0)
    {
        StatusMessage = "Некорректная сумма";
        return;
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

    // Валидация позиций
    foreach (var item in CurrentItems)
    {
        if (string.IsNullOrWhiteSpace(item.Name))
        {
            StatusMessage = "У всех позиций должно быть название";
            return;
        }
        if (item.Quantity <= 0)
        {
            StatusMessage = "Количество должно быть больше нуля";
            return;
        }
    }

    // ==================== Сохранение ====================
    IsBusy = true;
    try
    {
        // Если есть позиции — сумма = сумме позиций
        decimal? itemsTotal = null;
        if (HasItems)
        {
            itemsTotal = CurrentItems.Sum(i => i.Total);
            if (itemsTotal > 0)
                AmountText = itemsTotal.Value.ToString("0.##");
        }

        int transactionId;

        if (_editingTransaction is not null)
        {
            // Обновление существующей — сумма immutable,
            // кроме случая, когда есть позиции
            _editingTransaction.CategoryId = SelectedCategory.Id;
            _editingTransaction.CategoryName = SelectedCategory.Name;
            _editingTransaction.Date = Date;
            _editingTransaction.Note = Note?.Trim();

            if (itemsTotal.HasValue)
                _editingTransaction.Amount = itemsTotal.Value;

            await _db.UpdateTransactionAsync(_editingTransaction);
            transactionId = _editingTransaction.Id;
        }
        else
        {
            // Создание новой
            var transaction = new Transaction
            {
                Amount = amount,
                Type = CurrentType,
                CategoryId = SelectedCategory.Id,
                CategoryName = SelectedCategory.Name,
                Date = Date,
                Note = Note?.Trim(),
                CreatedAt = DateTime.Now,
                ReceiptImagePath = _currentReceiptFileName,
            };

            await _db.AddTransactionAsync(transaction);
            transactionId = transaction.Id;
        }

        // Сохраняем позиции — заменяем все сразу
        await _db.ReplaceItemsForTransactionAsync(transactionId, CurrentItems.ToList());

        ResetForm();
        await LoadTransactionsPageAsync();
        await ReloadBalanceAsync();

        StatusMessage = "Операция сохранена";
    }
    catch (Exception ex)
    {
        StatusMessage = $"Ошибка: {ex.Message}";
    }
    finally
    {
        IsBusy = false;
    }
}
```

**Что демонстрирует:**
- **Многоуровневая валидация** — до записи в БД.
- **Принцип immutable amount** — при редактировании сумма не меняется, 
  кроме случая с позициями.
- **`IsBusy` guard** — защита от двойных тапов.
- **Обработка исключений** — понятное сообщение пользователю вместо 
  стек-трейса.

---

## B.6. Кастомный контрол — ThemedPicker

**Файл:** `Controls/ThemedPicker.xaml.cs` (фрагмент)

Замена системного Picker, который не поддерживает цвета темы.

```csharp
public partial class ThemedPicker : ContentView
{
    public ThemedPicker()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    // ==================== Bindable Properties ====================
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList),
            typeof(ThemedPicker), default(IList), propertyChanged: OnAnyChanged);

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object),
            typeof(ThemedPicker), default(object),
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnAnyChanged);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    // ==================== Отображение ====================
    private void UpdateDisplay()
    {
        if (SelectedItem is null)
        {
            DisplayLabel.Text = Placeholder ?? "—";
            // SetDynamicResource — ключ к тому, что Label остаётся
            // подписанным на смену темы. Прямое присваивание TextColor
            // перекрыло бы подписку, и Label не перекрашивался бы.
            DisplayLabel.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        }
        else
        {
            DisplayLabel.Text = SelectedItem.ToString() ?? "";
            DisplayLabel.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        }
    }

    // ==================== Открытие модалки ====================
    private bool _isOpeningModal;

    private async void OnTapped(object? sender, EventArgs e)
    {
        if (ItemsSource is null || ItemsSource.Count == 0) return;
        if (_isOpeningModal) return;   // защита от двойного тапа
        _isOpeningModal = true;

        try
        {
            var page = FindParentPage();
            if (page is null) return;

            var modalPage = new ThemedPickerModalPage(ItemsSource, SelectedItem, Title);
            await page.Navigation.PushModalAsync(modalPage);

            var result = await modalPage.WaitForResultAsync();
            if (result is not null)
                SelectedItem = result;
        }
        finally { _isOpeningModal = false; }
    }
}
```

**Что демонстрирует:**
- **`BindableProperty`** — правильный способ создавать контролы в MAUI.
- **`SetDynamicResource`** вместо прямого `TextColor` — критичный приём 
  для поддержки смены темы в рантайме.
- **Защита от двойного тапа** через флаг.
- **Модальная страница вместо Popup** — упрощает контроль над стеком.

---

## B.7. Полноэкранный просмотр фото с зумом

**Файл:** `Controls/ReceiptViewerPage.xaml.cs` (полностью)

Модалка для просмотра фото чека с пинч-зумом.

```csharp
public partial class ReceiptViewerPage : ContentPage
{
    private const double MinScale = 1.0;
    private const double MaxScale = 5.0;

    private double _currentScale = 1.0;
    private double _startScale = 1.0;
    private double _xOffset = 0, _yOffset = 0;
    private double _startXOffset = 0, _startYOffset = 0;

    public ReceiptViewerPage(ImageSource source)
    {
        InitializeComponent();
        ImageViewer.Source = source;

        // Зум от центра, не от левого верхнего угла
        ImageViewer.AnchorX = 0.5;
        ImageViewer.AnchorY = 0.5;
    }

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _startScale = ImageViewer.Scale;
        }
        else if (e.Status == GestureStatus.Running)
        {
            _currentScale = _startScale * e.Scale;   // не += , а *= — точнее
            _currentScale = Math.Max(MinScale, Math.Min(MaxScale, _currentScale));
            ImageViewer.Scale = _currentScale;
        }
        else if (e.Status == GestureStatus.Completed)
        {
            // При возврате к масштабу 1 — сброс смещения (фото в центр)
            if (Math.Abs(_currentScale - MinScale) < 0.05)
            {
                _currentScale = MinScale;
                ImageViewer.Scale = MinScale;
                _xOffset = _yOffset = 0;
                ImageViewer.TranslationX = 0;
                ImageViewer.TranslationY = 0;
            }
        }
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        // Панорамирование работает только при зуме > 1
        if (_currentScale <= MinScale + 0.01) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _startXOffset = _xOffset;
                _startYOffset = _yOffset;
                break;

            case GestureStatus.Running:
                _xOffset = _startXOffset + e.TotalX;
                _yOffset = _startYOffset + e.TotalY;
                ImageViewer.TranslationX = _xOffset;
                ImageViewer.TranslationY = _yOffset;
                break;
        }
    }
}
```

**Что демонстрирует:**
- **Комбинация жестов** — `PinchGestureRecognizer` + `PanGestureRecognizer`.
- **Правильная математика зума** — `_startScale * e.Scale` вместо `+=`.
- **Ограничение MinScale/MaxScale** — фото не «улетает» за границы.
- **Сброс смещения** при возврате к масштабу 1.

---

## B.8. Уведомления — расчёт времени

**Файл:** `Services/ReminderService.cs` (фрагмент)

Расчёт ближайшего момента для напоминания.

```csharp
private static DateTimeOffset? CalculateNextNotificationTime(RecurringPayment payment)
{
    var today = DateTime.Today;

    // Проверяем текущий и два следующих месяца — первая дата в будущем
    for (int monthOffset = 0; monthOffset < 3; monthOffset++)
    {
        var baseMonth = new DateTime(today.Year, today.Month, 1).AddMonths(monthOffset);

        // Учитываем, что в месяце может быть меньше дней, чем DayOfMonth
        // (например, 31 → 28/29 для февраля)
        var daysInMonth = DateTime.DaysInMonth(baseMonth.Year, baseMonth.Month);
        var safeDay = Math.Min(Math.Max(payment.DayOfMonth, 1), daysInMonth);

        var paymentDate = new DateTime(baseMonth.Year, baseMonth.Month, safeDay);
        var notifyDate = paymentDate.AddDays(-payment.ReminderDaysBefore);
        var notifyDateTime = notifyDate.Date + payment.ReminderTime;

        if (notifyDateTime > DateTime.Now)
            return new DateTimeOffset(notifyDateTime);
    }

    return null;   // за 3 месяца не нашли — не планируем
}
```

**Что демонстрирует:**
- **Работа с календарём** — учёт разного количества дней в месяцах.
- **Итеративный поиск** — первая подходящая дата в будущем.
- **`DateTimeOffset`** — правильный тип для планирования alarm-ов 
  (учитывает часовой пояс).

**Планирование с точным режимом:**

```csharp
var request = new NotificationRequest
{
    NotificationId = payment.Id,
    Title = $"🔔 {payment.Name}",
    Description = BuildDescription(payment),
    Schedule = new NotificationRequestSchedule
    {
        NotifyTime = notifyTime.Value,
        Android = new AndroidScheduleOptions
        {
            // ExactAllowWhileIdle — точный alarm даже в режиме Doze
            ScheduleMode = AndroidScheduleMode.ExactAllowWhileIdle
        }
    },
};

await LocalNotificationCenter.Current.Show(request);
```

**Ключевое решение:** `ExactAllowWhileIdle` вместо `InexactAllowWhileIdle`. 
Первый использует `AlarmManager` напрямую, второй — `WorkManager`, 
который на Samsung может задерживать задачи на 15–30 минут.

---

## Заключение

Все семь фрагментов показывают ключевые технические решения проекта:

| № | Фрагмент | Что показывает |
|---|---|---|
| B.1 | Модель Transaction | Денормализация, ObservableObject |
| B.2 | DatabaseService | Facade, Repository, защита от гонки |
| B.3 | ThemeService | Strategy, динамическая смена темы |
| B.4 | ForecastService | Применение статистики, эвристика |
| B.5 | MainViewModel.SaveAsync | Валидация, immutable amount, guard |
| B.6 | ThemedPicker | Кастомный контрол, SetDynamicResource |
| B.7 | ReceiptViewerPage | Жесты, математика зума |
| B.8 | ReminderService | AlarmManager, ExactAllowWhileIdle |

Полный код доступен в репозитории: `<ссылка на GitHub>`.