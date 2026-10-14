using AFi.Collections;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableRangeCollection<Category> Categories { get; } = new();

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

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ReloadAsync();
    }

    [RelayCommand]
    private void SelectExpensesTab()
    {
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    private void SelectIncomeTab()
    {
        SelectedTabIndex = 1;
    }

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

        if (rawName is null) return;

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
                Icon = "👤",
            };

            await _db.AddCategoryAsync(category);
            await LoadCategoriesCoreAsync();

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

    [RelayCommand]
    private async Task DeleteCategoryAsync(Category? category)
    {
        if (category is null) return;
        if (IsBusy) return;

        if (category.IsDefault)
        {
            StatusMessage = "Стандартную категорию удалить нельзя";
            return;
        }

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

    // ==================== Вспомогательные ====================

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

    private async Task LoadCategoriesCoreAsync()
    {
        await _db.InitializeAsync();

        var list = await _db.GetCategoriesAsync(CurrentType);

        Categories.ReplaceRange(list);
    }

    private static string Plural(int count, string one, string few, string many)
    {
        int mod100 = count % 100;
        int mod10 = count % 10;
        if (mod100 >= 11 && mod100 <= 14) return many;
        if (mod10 == 1) return one;
        if (mod10 >= 2 && mod10 <= 4) return few;
        return many;
    }
}