using SQLite;

namespace AFi.Models;

/// <summary>
/// Регулярный платёж — это напоминание, а не автоплатёж.
/// Деньги с карты не списываются. Приложение просто показывает
/// уведомление в заданное время до даты платежа.
/// </summary>
[Table("recurring_payments")]
public class RecurringPayment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>
    /// Тип платежа: регулярный расход (аренда) или регулярный доход
    /// (например, оплата от клиента по договору).
    /// </summary>
    public TransactionType Type { get; set; } = TransactionType.Expense;

    /// <summary>
    /// День месяца, когда происходит платёж (1–31).
    /// Если в месяце меньше дней — платёж «переезжает» на последний день.
    /// </summary>
    public int DayOfMonth { get; set; } = 1;

    /// <summary>
    /// За сколько дней до даты напоминать. Сейчас всегда 2,
    /// но поле уже в БД — позже дадим пользователю настраивать.
    /// </summary>
    public int ReminderDaysBefore { get; set; } = 2;

    /// <summary>
    /// Время напоминания (часы и минуты). Хранится как TimeSpan,
    /// потому что SQLite-net не умеет TimeOnly из коробки.
    /// Сейчас всегда 10:00, потом пользователь сможет менять.
    /// </summary>
    public TimeSpan ReminderTime { get; set; } = TimeSpan.FromHours(10);

    /// <summary>
    /// Привязка к категории. Необязательна — пользователь может
    /// сделать напоминание без категории.
    /// </summary>
    public int? CategoryId { get; set; }

    [MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string CategoryIcon { get; set; } = string.Empty;

    /// <summary>
    /// Включено ли напоминание. Если false — уведомление не планируется.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    [MaxLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Отображаемое название с иконкой: "🏠 Аренда".
    /// </summary>
    [Ignore]
    public string DisplayName =>
        string.IsNullOrEmpty(CategoryIcon) ? Name : $"{CategoryIcon} {Name}";

    /// <summary>
    /// Для отладки и потенциального отображения.
    /// </summary>
    public override string ToString() => DisplayName;
}