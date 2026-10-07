using AFi.Services;
using AFi.ViewModels;
using AFi.Views;
using Microsoft.Extensions.Logging;

namespace AFi;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ==================== DI-регистрация ====================

        // Сервисы
        builder.Services.AddSingleton<DatabaseService>();

        // ViewModels
        builder.Services.AddSingleton<MainViewModel>();

        // Страницы
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}