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
    /// Заглушка для команды добавления — наполним логикой на шаге 2.5.3.
    /// </summary>
    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        await _dialogs.ConfirmAsync(
            "В разработке",
            "Добавление категорий появится в следующем шаге.",
            "OK",
            "OK");
    }

    /// <summary>
    /// Заглушка для команды удаления — наполним логикой на шаге 2.5.4.
    /// </summary>
    [RelayCommand]
    private async Task DeleteCategoryAsync(Category? category)
    {
        if (category is null) return;

        await _dialogs.ConfirmAsync(
            "В разработке",
            $"Удаление категории «{category.Name}» появится в следующем шаге.",
            "OK",
            "OK");
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

    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await _db.InitializeAsync();

            var list = await _db.GetCategoriesAsync(CurrentType);

            Categories.Clear();
            foreach (var c in list)
                Categories.Add(c);

            StatusMessage = $"Категорий: {Categories.Count}";
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
}