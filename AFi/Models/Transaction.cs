using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace AFi.Models;

/// <summary>
/// Одна финансовая операция: доход или расход.
/// Наследуется от ObservableObject, чтобы UI мгновенно реагировал
/// на изменение IsSelected и IsExpanded.
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

    [MaxLength(10)]
    public string CategoryIcon { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Имя файла с фото чека (только имя, без пути).
    /// Полный путь собирается через ReceiptService.GetFullPath().
    /// null — фото не прикреплено.
    /// </summary>
    [MaxLength(100)]
    public string? ReceiptImagePath { get; set; }

    // ==================== Временные флаги UI ====================

    private bool _isSelected;

    [Ignore]
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isExpanded;

    /// <summary>
    /// Развёрнута ли карточка в списке для показа позиций чека.
    /// </summary>
    [Ignore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    // ==================== Позиции чека ====================

    private ObservableCollection<TransactionItem>? _items;

    /// <summary>
    /// Позиции чека для этой операции. Загружаются лениво вместе
    /// со списком операций (одним запросом на все видимые).
    /// </summary>
    [Ignore]
    public ObservableCollection<TransactionItem> Items
    {
        get => _items ??= new ObservableCollection<TransactionItem>();
        set
        {
            if (SetProperty(ref _items, value))
            {
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(ItemsCounterText));
            }
        }
    }

    /// <summary>
    /// true, если у операции есть хотя бы одна позиция.
    /// </summary>
    [Ignore]
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Текст счётчика позиций: "3 позиции".
    /// </summary>
    [Ignore]
    public string ItemsCounterText
    {
        get
        {
            var count = Items.Count;
            if (count == 0) return string.Empty;

            var word = count switch
            {
                1 => "позиция",
                2 or 3 or 4 => "позиции",
                _ when count % 100 >= 11 && count % 100 <= 14 => "позиций",
                _ when count % 10 == 1 => "позиция",
                _ when count % 10 >= 2 && count % 10 <= 4 => "позиции",
                _ => "позиций",
            };

            return $"{count} {word}";
        }
    }

    // ==================== Отображение ====================

    [Ignore]
    public string DisplayCategoryName =>
        string.IsNullOrEmpty(CategoryIcon)
            ? CategoryName
            : $"{CategoryIcon} {CategoryName}";
}