using SQLite;

namespace AFi.Models;

/// <summary>
/// Одна финансовая операция: доход или расход.
/// </summary>
[Table("transactions")]
public class Transaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Сумма операции в рублях. decimal — чтобы избежать ошибок округления
    /// (double 0.1 + 0.2 = 0.30000000000000004, а decimal — точно).
    /// </summary>
    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>
    /// Ссылка на категорию. Nullable на случай удаления категории пользователем
    /// на Этапе 2 — операция останется, но потеряет связь.
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Имя категории на момент создания операции. Денормализация —
    /// чтобы не делать JOIN для отображения списка и сохранить
    /// историческую запись, даже если категорию потом переименуют или удалят.
    /// </summary>
    [MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Дата операции (не создания записи). Пользователь может внести вчерашнюю покупку.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Комментарий, необязательный. Может быть null.
    /// </summary>
    [MaxLength(200)]
    public string? Note { get; set; }

    /// <summary>
    /// Когда запись создана в системе. Нужно для сортировки,
    /// если у нескольких операций одинаковая дата.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}