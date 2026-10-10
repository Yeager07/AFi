using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

/// <summary>
/// ViewModel экрана управления категориями.
/// Показывает список категорий выбранного типа (расход/доход),
/// позволяет добавлять и удалять их.
/// </summary>
public partial class CategoriesViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;

    // ==================== Состояние ====================

    /// <summary>
    /// 0 — Расходы, 1 — Доходы. По умолчанию открываем расходы,
    /// потому что 90% времени пользователь работает с ними.
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// Текст статуса: «Категория добавлена», «Ошибка...» и т.д.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Флаг «идёт операция» — блокирует кнопки на время записи в БД.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Категории выбранного типа для отображения.
    /// </summary>
    public ObservableCollection<Category> Categories { get; } = new();

    /// <summary>
    /// Тип, соответствующий выбранной вкладке.
    /// </summary>
    public TransactionType CurrentType =>
        SelectedTabIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    public CategoriesViewModel(DatabaseService db, IDialogService dialogs)
    {
        _db = db;
        _dialogs = dialogs;
    }

    // ==================== Реакции ====================

    partial void OnSelectedTabIndexChanged(int value)
    {
        _ = ReloadAsync();
    }

    // ==================== Команды ====================

    /// <summary>
    /// Загружает категории при открытии экрана.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        await ReloadAsync();
    }

    /// <summary>
    /// Добавляет новую категорию через prompt-диалог.
    /// Проверяет пустоту и дубликаты по имени внутри текущего типа.
    /// </summary>
    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (IsBusy) return;

        var typeRu = CurrentType == TransactionType.Income ? "доходов" : "расходов";

        var rawName = await _dialogs.PromptAsync(
            title: "Новая категория",
            message: $"Введите название для раздела «{typeRu}»",
            placeholder: "Например, Хозтовары",
            maxLength: 50);

        if (rawName is null) return; // пользователь отменил

        var name = rawName.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Название не может быть пустым";
            return;
        }

        if (name.Length < 2)
        {
            StatusMessage = "Название должно содержать минимум 2 символа";
            return;
        }

        // Проверка на дубликат среди категорий того же типа
        if (Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Категория «{name}» уже есть в этом разделе";
            return;
        }

        IsBusy = true;
        try
        {
            var category = new Category
            {
                Name = name,
                Type = CurrentType,
                IsDefault = false,
                Icon = "👤",   // стандартная иконка для пользовательских категорий
            };

            await _db.AddCategoryAsync(category);
            await LoadCategoriesCoreAsync();   // ← обход проверки IsBusy

            StatusMessage = $"Категория «{name}» добавлена — всего {Categories.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка добавления: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Удаляет категорию после подтверждения. Стандартные категории
    /// (IsDefault = true) защищены от удаления на уровне ViewModel.
    /// Операции, привязанные к категории, остаются — у них CategoryId
    /// становится null, а денормализованное CategoryName сохраняется.
    /// </summary>
    [RelayCommand]
    private async Task DeleteCategoryAsync(Category? category)
    {
        if (category is null) return;
        if (IsBusy) return;

        // Защита на уровне VM: стандартные категории удалять нельзя.
        // В XAML кнопка для них тоже скрыта, но подстраховка лишней не бывает.
        if (category.IsDefault)
        {
            StatusMessage = "Стандартную категорию удалить нельзя";
            return;
        }

        // Считаем привязанные операции — от этого зависит текст диалога
        var transactionCount = await _db.CountTransactionsByCategoryAsync(category.Id);

        string message;
        if (transactionCount == 0)
        {
            message = $"Удалить категорию «{category.Name}»?";
        }
        else
        {
            var word = Plural(transactionCount, "операция", "операции", "операций");
            message = $"У этой категории {transactionCount} {word}.\n\n" +
                    "Они останутся в истории, но потеряют привязку к категории. " +
                    "Продолжить?";
        }

        var confirmed = await _dialogs.ConfirmAsync(
            title: "Удаление категории",
            message: message,
            accept: "Удалить",
            cancel: "Отмена");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            await _db.DeleteCategoryAsync(category.Id);
            await LoadCategoriesCoreAsync();

            StatusMessage = $"Категория «{category.Name}» удалена — всего {Categories.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Склонение существительных по числу для русского языка.
    /// Пример: 1 операция, 2 операции, 5 операций.
    /// </summary>
    private static string Plural(int count, string one, string few, string many)
    {
        int mod100 = count % 100;
        int mod10 = count % 10;

        if (mod100 >= 11 && mod100 <= 14) return many;
        if (mod10 == 1) return one;
        if (mod10 >= 2 && mod10 <= 4) return few;
        return many;
    }
    
    /// <summary>
    /// Переключает вкладку на «Расходы».
    /// </summary>
    [RelayCommand]
    private void SelectExpensesTab()
    {
        SelectedTabIndex = 0;
    }

    /// <summary>
    /// Переключает вкладку на «Доходы».
    /// </summary>
    [RelayCommand]
    private void SelectIncomeTab()
    {
        SelectedTabIndex = 1;
    }
    // ==================== Вспомогательные ====================

    /// <summary>
    /// Публичная перезагрузка списка. Защищена от параллельных вызовов
    /// через флаг IsBusy. Используется в хуках и в LoadAsync.
    /// </summary>
    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await LoadCategoriesCoreAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Реальная загрузка категорий — без проверки IsBusy.
    /// Вызывается из методов, которые уже держат IsBusy=true
    /// (например, AddCategoryAsync), чтобы избежать ложной блокировки.
    /// </summary>
    private async Task LoadCategoriesCoreAsync()
    {
        await _db.InitializeAsync();

        var list = await _db.GetCategoriesAsync(CurrentType);

        Categories.Clear();
        foreach (var c in list)
            Categories.Add(c);
    }
}