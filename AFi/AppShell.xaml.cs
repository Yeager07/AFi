using AFi.Views;

namespace AFi;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Регистрируем push-маршрут для Истории.
        // Это позволит открыть её «поверх» Главной — со стрелкой возврата.
        // Пункт в боковом меню при этом работает независимо.
        Routing.RegisterRoute("history-push", typeof(HistoryPage));
    }
}