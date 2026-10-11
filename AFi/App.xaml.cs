using AFi.Services;

namespace AFi;

public partial class App : Application
{
    private readonly IThemeService _themeService;
    private readonly IReminderService _reminderService;
    private readonly DatabaseService _db;

    public App(IThemeService themeService,
               IReminderService reminderService,
               DatabaseService db)
    {
        InitializeComponent();

        _themeService = themeService;
        _reminderService = reminderService;
        _db = db;

        // Применяем сохранённую тему до первого кадра
        _themeService.ApplySavedTheme();

        // Инициализация напоминаний в фоне — не блокирует старт
        _ = InitializeRemindersAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    /// <summary>
    /// Запрашивает разрешение на уведомления и перепланирует все
    /// сохранённые напоминания. Вызывается один раз при старте приложения.
    /// </summary>
    private async Task InitializeRemindersAsync()
    {
        try
        {
            // Небольшая задержка, чтобы UI успел инициализироваться
            // и запрос разрешения отобразился корректно
            await Task.Delay(1000);

            await _db.InitializeAsync();

            await _reminderService.RequestPermissionAsync();

            var all = await _db.GetRecurringPaymentsAsync();
            await _reminderService.RescheduleAllAsync(all);
        }
        catch
        {
            // Тихо игнорируем — напоминания не критичны
        }
    }
}