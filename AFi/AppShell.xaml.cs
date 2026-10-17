using AFi.Views;

namespace AFi;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        // Резолвим Singleton-страницы из DI — они создаются один раз.
        // Shell будет переключать уже готовые визуальные деревья,
        // без повторного парсинга XAML при каждом переходе.
        MainShellContent.Content       = services.GetRequiredService<MainPage>();
        HistoryShellContent.Content    = services.GetRequiredService<HistoryPage>();
        StatsShellContent.Content      = services.GetRequiredService<StatsPage>();
        RecurringShellContent.Content  = services.GetRequiredService<RecurringPage>();
        ForecastShellContent.Content   = services.GetRequiredService<ForecastPage>();
        CategoriesShellContent.Content = services.GetRequiredService<CategoriesPage>();
        SettingsShellContent.Content   = services.GetRequiredService<SettingsPage>();

        // Пуш-маршруты (модалки и формы)
        Routing.RegisterRoute("history-push", typeof(HistoryPage));
        Routing.RegisterRoute("recurring-edit", typeof(RecurringEditPage));
    }
}