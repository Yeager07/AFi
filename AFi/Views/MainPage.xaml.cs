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
    }

    // ==================== Picker «Тип» ====================

    private void SetupTypePicker()
    {
        TypePicker.ItemsSource = _viewModel.TypeOptions;

        var initialIdx = _viewModel.SelectedTypeIndex;
        if (initialIdx >= 0 && initialIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[initialIdx];

        // Picker → VM
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

        // VM → Picker
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
        // ItemsSource — ссылка на ту же коллекцию, что у VM.
        // ObservableCollection сама уведомит Picker при Clear+Add.
        CategoryPicker.ItemsSource = _viewModel.AvailableCategories;

        CategoryPicker.SelectedItem = _viewModel.SelectedCategory;

        // Picker → VM
        CategoryPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && CategoryPicker.SelectedItem is Category selected)
            {
                if (!ReferenceEquals(_viewModel.SelectedCategory, selected))
                    _viewModel.SelectedCategory = selected;
            }
        };

        // VM → Picker
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

    // ==================== Жизненный цикл ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // Синхронизируем оба Picker после загрузки
        var typeIdx = _viewModel.SelectedTypeIndex;
        if (typeIdx >= 0 && typeIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[typeIdx];

        CategoryPicker.SelectedItem = _viewModel.SelectedCategory;
    }

    /// <summary>
    /// Системная кнопка «назад» на Android. Если активен режим
    /// мультивыбора — отменяем выбор и не выходим.
    /// </summary>
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