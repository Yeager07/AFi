using AFi.Services;
using AFi.Views;

namespace AFi;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly IThemeService _themeService;
    private readonly IReminderService _reminderService;
    private readonly DatabaseService _db;

    public App(IServiceProvider services,
               IThemeService themeService,
               IReminderService reminderService,
               DatabaseService db)
    {
        InitializeComponent();

        _services = services;
        _themeService = themeService;
        _reminderService = reminderService;
        _db = db;

        // Тема — до первого кадра, чтобы splash уже был в нужных цветах
        _themeService.ApplySavedTheme();

        // Инициализация напоминаний — фоново, не блокирует splash
        _ = InitializeRemindersAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // При запуске сначала показываем SplashPage
        return new Window(_services.GetRequiredService<SplashPage>());
    }

    private async Task InitializeRemindersAsync()
    {
        try
        {
            await Task.Delay(1000);

            await _db.InitializeAsync();
            await _reminderService.RequestPermissionAsync();

            var all = await _db.GetRecurringPaymentsAsync();
            await _reminderService.RescheduleAllAsync(all);
        }
        catch { }
    }
}