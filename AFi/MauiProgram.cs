using AFi.Services;
using AFi.ViewModels;
using AFi.Views;
using CommunityToolkit.Maui;
using Microcharts.Maui;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;

namespace AFi;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMicrocharts()
            .UseLocalNotification()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Сервисы
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<IReminderService, ReminderService>();
        builder.Services.AddSingleton<IReceiptService, ReceiptService>();
        builder.Services.AddSingleton<IFormSnapshotService, FormSnapshotService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<ForecastService>();
        builder.Services.AddSingleton<IDataBackupService, DataBackupService>();

        // ViewModels — Singleton
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddSingleton<StatsViewModel>();
        builder.Services.AddSingleton<ForecastViewModel>();
        builder.Services.AddSingleton<CategoriesViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<RecurringViewModel>();

        // Форма редактирования напоминания — Transient
        builder.Services.AddTransient<RecurringEditViewModel>();

        // Страницы — Singleton
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<StatsPage>();
        builder.Services.AddSingleton<CategoriesPage>();
        builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddSingleton<RecurringPage>();
        builder.Services.AddSingleton<ForecastPage>();

        // Форма редактирования напоминания — Transient
        builder.Services.AddTransient<RecurringEditPage>();

        return builder.Build();
    }
}