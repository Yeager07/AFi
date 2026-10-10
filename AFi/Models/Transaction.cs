using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace AFi.Models;

/// <summary>
/// Одна финансовая операция: доход или расход.
/// Наследуется от ObservableObject, чтобы UI мгновенно реагировал
/// на изменение IsSelected (галочка мультивыбора).
/// </summary>
[Table("transactions")]
public partial class Transaction : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }

    public int? CategoryId { get; set; }

    [MaxLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Иконка категории на момент создания операции. Денормализация —
    /// чтобы не терять иконку при удалении или переименовании категории.
    /// </summary>
    [MaxLength(10)]
    public string CategoryIcon { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    private bool _isSelected;

    /// <summary>
    /// Флаг выбора для режима мультивыбора. В БД не сохраняется
    /// (помечен [Ignore]). Уведомляет UI через SetProperty —
    /// благодаря этому галочка появляется мгновенно.
    /// </summary>
    [Ignore]
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Отображаемое имя категории с иконкой: "🏠 Аренда".
    /// Не сохраняется в БД.
    /// </summary>
    [Ignore]
    public string DisplayCategoryName =>
        string.IsNullOrEmpty(CategoryIcon)
            ? CategoryName
            : $"{CategoryIcon} {CategoryName}";
}