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

    public TransactionType Type { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>
    /// Иконка-эмодзи для визуального отображения категории.
    /// Хранится как одна кодовая точка (например, "🏠").
    /// У пользовательских категорий по умолчанию "👤".
    /// </summary>
    [MaxLength(10)]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Отображаемое имя с иконкой: "🏠 Аренда".
    /// Не сохраняется в БД. Используется в Picker и других местах,
    /// где нужен компактный текст.
    /// </summary>
    [Ignore]
    public string DisplayName =>
        string.IsNullOrEmpty(Icon) ? Name : $"{Icon} {Name}";
}