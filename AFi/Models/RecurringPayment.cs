using SQLite;

namespace AFi.Models;

[Table("recurring_payments")]
public class RecurringPayment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Expense;

    public int DayOfMonth { get; set; } = 1;

    public int ReminderDaysBefore { get; set; } = 2;

    public TimeSpan ReminderTime { get; set; } = TimeSpan.FromHours(10);

    public int? CategoryId { get; set; }

    [MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string CategoryIcon { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    [MaxLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Ignore]
    public string DisplayName =>
        string.IsNullOrEmpty(CategoryIcon) ? Name : $"{CategoryIcon} {Name}";

    /// <summary>
    /// Готовая строка для отображения под названием в карточке:
    /// "15-го числа · Оплата за офис".
    /// Формируется в модели, чтобы UI просто биндился на одно свойство.
    /// </summary>
    [Ignore]
    public string Subtitle
    {
        get
        {
            var dayText = $"{DayOfMonth}-го числа";
            var reminderText = ReminderDaysBefore switch
            {
                0 => "в день платежа",
                1 => "за 1 день",
                2 => "за 2 дня",
                3 => "за 3 дня",
                _ => $"за {ReminderDaysBefore} дней",
            };
            var timeText = $"{(int)ReminderTime.TotalHours:00}:{ReminderTime.Minutes:00}";

            var parts = new List<string> { dayText, $"{reminderText}, {timeText}" };
            if (!string.IsNullOrWhiteSpace(Note))
                parts.Add(Note);

            return string.Join(" · ", parts);
        }
    }

    public override string ToString() => DisplayName;
}