using AFi.Services;
using AFi.ViewModels;
using AFi.Views;
using CommunityToolkit.Maui;
using Microcharts.Maui;
using Microsoft.Extensions.Logging;

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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ==================== DI-регистрация ====================

        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<StatsViewModel>();

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<StatsPage>();

        return builder.Build();
    }
}