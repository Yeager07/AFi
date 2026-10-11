using AFi.Views;

namespace AFi;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Пуш-маршрут для истории (используется из кнопки «Вся история»)
        Routing.RegisterRoute("history-push", typeof(HistoryPage));

        // Пуш-маршрут для формы регулярного платежа
        Routing.RegisterRoute("recurring-edit", typeof(RecurringEditPage));
    }
}