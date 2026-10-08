using System.Globalization;

namespace AFi.Models;

/// <summary>
/// Группа операций одного дня для отображения в группированном CollectionView.
/// Наследуется от List&lt;Transaction&gt; — это требование MAUI:
/// CollectionView с IsGrouped="True" ожидает, что источник — IEnumerable&lt;IEnumerable&lt;T&gt;&gt;
/// или коллекция элементов, наследующих IEnumerable.
/// </summary>
public class TransactionGroup : List<Transaction>
{
    public DateTime Date { get; }
    public string DateLabel { get; }
    public string SummaryText { get; }
    public bool IsPositive { get; }

    public TransactionGroup(DateTime date, List<Transaction> items) : base(items)
    {
        Date = date.Date;
        DateLabel = FormatDateLabel(Date);

        decimal income = 0, expense = 0;
        foreach (var t in items)
        {
            if (t.Type == TransactionType.Income) income += t.Amount;
            else expense += t.Amount;
        }

        var balance = income - expense;
        IsPositive = balance >= 0;
        var sign = balance >= 0 ? "+" : "−";
        SummaryText = $"{sign}{Math.Abs(balance):N0} ₽";
    }

    private static string FormatDateLabel(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today) return "Сегодня";
        if (date == today.AddDays(-1)) return "Вчера";

        var culture = new CultureInfo("ru-RU");
        var dayOfWeek = culture.DateTimeFormat.GetDayName(date.DayOfWeek);
        return $"{date:dd.MM.yyyy}, {dayOfWeek}";
    }
}