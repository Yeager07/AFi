using AFi.Services;
using AFi.ViewModels;
using AFi.Views;
using CommunityToolkit.Maui;
using Microcharts.Maui;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using ZXing.Net.Maui.Controls;

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
            .UseBarcodeReader() // Инициализация ZXing для QR
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ==================== Сервисы ====================
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<IReminderService, ReminderService>();
        builder.Services.AddSingleton<IReceiptService, ReceiptService>();
        builder.Services.AddSingleton<IFormSnapshotService, FormSnapshotService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<ForecastService>();
        builder.Services.AddSingleton<IDataBackupService, DataBackupService>();
        builder.Services.AddSingleton<IOcrService, OcrService>();
        
        // Оставляем только QR-сервисы
        builder.Services.AddSingleton<IQRCodeService, QRCodeService>();
        builder.Services.AddSingleton<IReceiptApiService, ReceiptApiService>();

        // ViewModels — Singleton
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();   // ← было Singleton
        builder.Services.AddSingleton<StatsViewModel>();
        builder.Services.AddSingleton<ForecastViewModel>();
        builder.Services.AddSingleton<CategoriesViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<RecurringViewModel>();

        // Форма редактирования напоминания — Transient
        builder.Services.AddTransient<RecurringEditViewModel>();

        // Страницы — Singleton
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<SplashPage>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<HistoryPage>();        // ← было Singleton
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