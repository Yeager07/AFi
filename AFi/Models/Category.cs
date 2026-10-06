using SQLite;

namespace AFi.Models;

/// <summary>
/// Категория операций. Может быть предустановленной (IsDefault = true)
/// или созданной пользователем.
/// </summary>
[Table("categories")]
public class Category
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// К какой группе относится: расходная или доходная.
    /// Категории разных типов не смешиваются в Picker'е.
    /// </summary>
    public TransactionType Type { get; set; }

    /// <summary>
    /// true — предустановленная категория, false — созданная пользователем.
    /// Пока не используется (управление категориями будет на Этапе 2),
    /// но закладываем сразу, чтобы не переделывать схему БД.
    /// </summary>
    public bool IsDefault { get; set; }
}