using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace AFi.Models;

/// <summary>
/// Одна финансовая операция: доход или расход.
/// Наследуется от ObservableObject, чтобы UI мгновенно реагировал
/// на изменение IsSelected, IsExpanded и ReceiptImageSource.
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

    private string? _receiptImagePath;

    [MaxLength(100)]
    public string? ReceiptImagePath
    {
        get => _receiptImagePath;
        set
        {
            if (SetProperty(ref _receiptImagePath, value))
            {
                OnPropertyChanged(nameof(HasReceipt));
                OnPropertyChanged(nameof(HasExtraDetails));
                OnPropertyChanged(nameof(ExtraDetailsLabel));
            }
        }
    }

    [Ignore]
    public bool HasReceipt => !string.IsNullOrEmpty(ReceiptImagePath);

    // ==================== Временные флаги UI ====================

    private bool _isSelected;

    [Ignore]
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isExpanded;

    [Ignore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    private ImageSource? _receiptImageSource;

    [Ignore]
    public ImageSource? ReceiptImageSource
    {
        get => _receiptImageSource;
        set => SetProperty(ref _receiptImageSource, value);
    }

    // ==================== Позиции чека ====================

    private ObservableCollection<TransactionItem>? _items;

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
                OnPropertyChanged(nameof(HasExtraDetails));
                OnPropertyChanged(nameof(ExtraDetailsLabel));
            }
        }
    }

    [Ignore]
    public bool HasItems => Items.Count > 0;

    /// <summary>
    /// Есть ли что показать в развёрнутом виде: позиции или фото.
    /// </summary>
    [Ignore]
    public bool HasExtraDetails => HasItems || HasReceipt;

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

    /// <summary>
    /// Строка-индикатор деталей: «3 позиции · фото чека».
    /// </summary>
    [Ignore]
    public string ExtraDetailsLabel
    {
        get
        {
            var parts = new List<string>();
            if (HasItems) parts.Add(ItemsCounterText);
            if (HasReceipt) parts.Add("фото чека");
            return string.Join(" · ", parts);
        }
    }

    [Ignore]
    public string DisplayCategoryName =>
        string.IsNullOrEmpty(CategoryIcon)
            ? CategoryName
            : $"{CategoryIcon} {CategoryName}";

    /// <summary>
    /// Склеенная строка «дата · комментарий» для карточки списка.
    /// Заменяет MultiBinding в XAML — быстрее и проще.
    /// </summary>
    [Ignore]
    public string DateAndNote =>
        string.IsNullOrWhiteSpace(Note)
            ? Date.ToString("dd.MM.yyyy")
            : $"{Date:dd.MM.yyyy} · {Note}";
}