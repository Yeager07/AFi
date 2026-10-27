using AFi.Models;

namespace AFi.Controls;

/// <summary>
/// Выбирает DataTemplate для элемента HistoryRow: либо шаблон заголовка
/// дня, либо шаблон карточки транзакции.
/// </summary>
public class HistoryTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? TransactionTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is HistoryRow row && row.IsHeader)
        {
            return HeaderTemplate
                ?? throw new InvalidOperationException("HeaderTemplate не задан");
        }

        return TransactionTemplate
            ?? throw new InvalidOperationException("TransactionTemplate не задан");
    }
}