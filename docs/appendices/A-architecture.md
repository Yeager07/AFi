# Приложение A. Архитектура: подробности

## A.1. Общая схема слоёв

Приложение использует классическую многослойную архитектуру. 
Каждый слой имеет строго определённую ответственность.

### Слой Models (предметная область)

**Назначение:** описание сущностей предметной области.

**Особенности:**
- Классы — POCO с атрибутами SQLite (`[Table]`, `[PrimaryKey]`, 
  `[Ignore]`).
- Некоторые модели наследуют `ObservableObject` для поддержки 
  UI-флагов (`IsSelected`, `IsExpanded`) — например, `Transaction`.
- Денормализация используется осознанно: `Transaction` хранит 
  `CategoryName` + `CategoryIcon`, чтобы не терять информацию 
  при удалении категории.

### Слой Services (бизнес-логика)

**Назначение:** инкапсуляция логики работы с внешними ресурсами 
и инфраструктурой.

**Правило:** каждый сервис имеет интерфейс. Потребитель работает 
только с интерфейсом. Реализация подставляется через DI.

**Сервисы:**

- `DatabaseService` — единственный, кто знает про `SQLiteAsyncConnection`. 
  Все остальные слои работают через его методы.
- `ThemeService` — управляет словарём цветов в `Application.Current.Resources`.
- `ReminderService` — планирует/отменяет уведомления через 
  `LocalNotificationCenter`.
- `ReceiptService` — работает с файловой системой (`FileSystem.AppDataDirectory`).
- `DialogService` — единая точка для всех модальных окон.
- `ForecastService` — реализация алгоритма прогноза.

### Слой ViewModels (состояние UI)

**Назначение:** держать состояние экрана и обрабатывать пользовательские 
команды.

**Принципы:**
- Наследование от `ObservableObject`.
- Свойства через `[ObservableProperty]`.
- Команды через `[RelayCommand]`.
- Никаких прямых обращений к UI-контролам — только через `IDialogService` 
  или события.

### Слой Views (XAML)

**Назначение:** отрисовка и обработка пользовательского ввода.

**Правила code-behind:**
- Установка `BindingContext` из DI.
- Связка кастомных `ThemedPicker` с ViewModel.
- Реакции на `OnAppearing` / `OnDisappearing` / `OnBackButtonPressed`.
- Специфичные для страницы жесты (скролл, анимации).

**Логика — в ViewModel, не в code-behind.** Это аксиома MVVM.

## A.2. Как проходит запрос данных

Пример: пользователь нажал «Сохранить» на форме операции.

1. **View (MainPage.xaml)** — `Button.Clicked` привязан к 
   `SaveCommand` через `Command="{Binding SaveCommand}"`.
2. **ViewModel (MainViewModel)** — `SaveAsync()` вызывается. Внутри:
   - Валидация (сумма, категория, дата).
   - Формирование объекта `Transaction`.
   - Вызов `_db.AddTransactionAsync(transaction)`.
   - Вызов `_db.ReplaceItemsForTransactionAsync(id, items)`.
   - Обновление `TransactionGroups` (перезагрузка списка).
3. **Service (DatabaseService)** — `InsertAsync(transaction)` через 
   `SQLiteAsyncConnection`.
4. **SQLite** — INSERT-запрос, возврат нового `Id`.
5. **ViewModel** — обновляет `ObservableCollection<TransactionGroup>`, 
   UI мгновенно перерисовывается.

## A.3. Обработка ошибок

Все операции, работающие с внешними ресурсами (БД, файлы, 
уведомления), обёрнуты в `try/catch`:

```csharp
try
{
    await _db.AddTransactionAsync(transaction);
    StatusMessage = "Операция сохранена";
}
catch (Exception ex)
{
    StatusMessage = $"Ошибка: {ex.Message}";
}
```

Пользователь не видит стек-трейс — только понятное сообщение 
в статусной строке. Ошибки логируются в `Debug.WriteLine` для 
отладки.

## A.4. Управление жизненным циклом

**`App.xaml.cs`:**
- В конструкторе применяется сохранённая тема (`ApplySavedTheme`).
- В фоне запускается `InitializeRemindersAsync` — запрос разрешений 
  и перепланирование уведомлений.

**Страницы:**
- `OnAppearing` — вызывается при появлении страницы. Обычно 
  дёргает `LoadCommand`.
- `OnDisappearing` — очистка ресурсов (например, отписка от событий).
- `OnBackButtonPressed` — перехват системной кнопки «назад» на Android. 
  В режиме мультивыбора отменяет выбор вместо выхода.

## A.5. Файловая структура проекта (детально)

```
AFi/
├── Models/
│   ├── Category.cs
│   ├── ForecastItem.cs
│   ├── ForecastResult.cs
│   ├── LegendItem.cs
│   ├── RecurringPayment.cs
│   ├── TopItem.cs
│   ├── Transaction.cs
│   ├── TransactionGroup.cs
│   ├── TransactionItem.cs
│   └── TransactionType.cs
├── Services/
│   ├── DatabaseService.cs
│   ├── DialogService.cs
│   ├── ForecastService.cs
│   ├── IDialogService.cs
│   ├── IReceiptService.cs
│   ├── IReminderService.cs
│   ├── IThemeService.cs
│   ├── ReceiptService.cs
│   ├── ReminderService.cs
│   └── ThemeService.cs
├── ViewModels/
│   ├── CategoriesViewModel.cs
│   ├── ForecastViewModel.cs
│   ├── HistoryViewModel.cs
│   ├── MainViewModel.cs
│   ├── RecurringEditViewModel.cs
│   ├── RecurringViewModel.cs
│   ├── SettingsViewModel.cs
│   └── StatsViewModel.cs
├── Views/
│   ├── CategoriesPage.xaml
│   ├── ForecastPage.xaml
│   ├── HistoryPage.xaml
│   ├── MainPage.xaml
│   ├── RecurringEditPage.xaml
│   ├── RecurringPage.xaml
│   ├── SettingsPage.xaml
│   └── StatsPage.xaml
├── Controls/
│   ├── ReceiptViewerPage.xaml
│   ├── ThemedActionSheetPage.xaml
│   ├── ThemedConfirmPage.xaml
│   ├── ThemedPicker.xaml
│   └── ThemedPickerModalPage.xaml
├── Converters/
│   ├── DayOfMonthConverter.cs
│   ├── InverseBoolConverter.cs
│   ├── IsNotEmptyConverter.cs
│   ├── OpacityConverter.cs
│   └── TitleToEmojiConverter.cs
├── Themes/
│   ├── LightTheme.xaml
│   └── DarkTheme.xaml
├── Resources/
│   ├── AppIcon/
│   ├── Fonts/
│   ├── Images/
│   ├── Raw/
│   ├── Splash/
│   └── Styles/
├── Platforms/
│   ├── Android/
│   ├── iOS/
│   ├── MacCatalyst/
│   └── Windows/
├── App.xaml
├── AppShell.xaml
├── MauiProgram.cs
└── AFi.csproj
```