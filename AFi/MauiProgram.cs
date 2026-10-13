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

        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<StatsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<RecurringViewModel>();
        builder.Services.AddTransient<RecurringEditViewModel>();
        builder.Services.AddTransient<ForecastViewModel>();

        // Страницы
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<StatsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<RecurringPage>();
        builder.Services.AddTransient<RecurringEditPage>();
        builder.Services.AddTransient<ForecastPage>();

        return builder.Build();
    }
}