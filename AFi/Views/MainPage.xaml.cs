using AFi.Helpers;
using AFi.Models;
using AFi.ViewModels;

namespace AFi.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        SetupTypePicker();
        SetupCategoryPicker();

        // При входе в режим редактирования — прокрутить страницу наверх,
        // чтобы пользователь увидел форму, а не остался на карточке в списке.
        _viewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsEditing)
                && _viewModel.IsEditing)
            {
                await MainScrollView.ScrollToAsync(0, 0, animated: true);
            }
        };
    }

    // ==================== Picker «Тип» ====================

    private void SetupTypePicker()
    {
        TypePicker.ItemsSource = _viewModel.TypeOptions;

        var initialIdx = _viewModel.SelectedTypeIndex;
        if (initialIdx >= 0 && initialIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[initialIdx];

        TypePicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && TypePicker.SelectedItem is string selected)
            {
                var idx = _viewModel.TypeOptions.IndexOf(selected);
                if (idx >= 0 && idx != _viewModel.SelectedTypeIndex)
                    _viewModel.SelectedTypeIndex = idx;
            }
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTypeIndex))
            {
                var idx = _viewModel.SelectedTypeIndex;
                if (idx >= 0 && idx < _viewModel.TypeOptions.Count)
                {
                    var next = _viewModel.TypeOptions[idx];
                    if (!Equals(TypePicker.SelectedItem, next))
                        TypePicker.SelectedItem = next;
                }
            }
        };
    }

    // ==================== Picker «Категория» ====================

    private void SetupCategoryPicker()
    {
        CategoryPicker.ItemsSource = _viewModel.AvailableCategories;

        CategoryPicker.SelectedItem = _viewModel.SelectedCategory;

        CategoryPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && CategoryPicker.SelectedItem is Category selected)
            {
                if (!ReferenceEquals(_viewModel.SelectedCategory, selected))
                    _viewModel.SelectedCategory = selected;
            }
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedCategory))
            {
                var next = _viewModel.SelectedCategory;
                if (!ReferenceEquals(CategoryPicker.SelectedItem, next))
                    CategoryPicker.SelectedItem = next;
            }
        };
    }

    // ==================== Позиции чека ====================

    private void OnItemEntryChanged(object? sender, TextChangedEventArgs e)
    {
        _viewModel.RefreshItemsTotal();
    }

    // ==================== Клавиатура ====================

    /// <summary>
    /// Срабатывает при нажатии «ОК» на клавиатуре в любом Entry.
    /// Принудительно скрываем клавиатуру.
    /// </summary>
    private void OnEntryCompleted(object? sender, EventArgs e)
    {
        KeyboardHelper.Hide();
    }

    // ==================== Жизненный цикл ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        var typeIdx = _viewModel.SelectedTypeIndex;
        if (typeIdx >= 0 && typeIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[typeIdx];

        CategoryPicker.SelectedItem = _viewModel.SelectedCategory;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsSelectionMode)
        {
            _viewModel.CancelSelectionCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}