using AFi.ViewModels;

namespace AFi.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        SetupPeriodPicker();
        SetupCategoryPicker();
    }

    // ==================== Picker «Период» ====================

    private void SetupPeriodPicker()
    {
        PeriodPicker.ItemsSource = _viewModel.FilterOptions;

        var initialIdx = _viewModel.SelectedFilterIndex;
        if (initialIdx >= 0 && initialIdx < _viewModel.FilterOptions.Count)
            PeriodPicker.SelectedItem = _viewModel.FilterOptions[initialIdx];

        // Picker → VM
        PeriodPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && PeriodPicker.SelectedItem is string selected)
            {
                var idx = _viewModel.FilterOptions.IndexOf(selected);
                if (idx >= 0 && idx != _viewModel.SelectedFilterIndex)
                    _viewModel.SelectedFilterIndex = idx;
            }
        };

        // VM → Picker
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HistoryViewModel.SelectedFilterIndex))
            {
                var idx = _viewModel.SelectedFilterIndex;
                if (idx >= 0 && idx < _viewModel.FilterOptions.Count)
                {
                    var next = _viewModel.FilterOptions[idx];
                    if (!Equals(PeriodPicker.SelectedItem, next))
                        PeriodPicker.SelectedItem = next;
                }
            }
        };
    }

    // ==================== Picker «Категория» ====================

    private void SetupCategoryPicker()
    {
        CategoryPicker.ItemsSource = _viewModel.CategoryFilterItems;

        CategoryPicker.SelectedItem = _viewModel.SelectedCategoryIndex >= 0
            && _viewModel.SelectedCategoryIndex < _viewModel.CategoryFilterItems.Count
            ? _viewModel.CategoryFilterItems[_viewModel.SelectedCategoryIndex]
            : null;

        // Picker → VM
        CategoryPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && CategoryPicker.SelectedItem is CategoryFilterItem selected)
            {
                var idx = _viewModel.CategoryFilterItems.IndexOf(selected);
                if (idx >= 0 && idx != _viewModel.SelectedCategoryIndex)
                    _viewModel.SelectedCategoryIndex = idx;
            }
        };

        // VM → Picker
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HistoryViewModel.SelectedCategoryIndex))
            {
                var idx = _viewModel.SelectedCategoryIndex;
                if (idx >= 0 && idx < _viewModel.CategoryFilterItems.Count)
                {
                    var next = _viewModel.CategoryFilterItems[idx];
                    if (!ReferenceEquals(CategoryPicker.SelectedItem, next))
                        CategoryPicker.SelectedItem = next;
                }
            }
        };
    }

    // ==================== Жизненный цикл ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        CategoryPicker.ItemsSource = _viewModel.CategoryFilterItems;

        var periodIdx = _viewModel.SelectedFilterIndex;
        if (periodIdx >= 0 && periodIdx < _viewModel.FilterOptions.Count)
            PeriodPicker.SelectedItem = _viewModel.FilterOptions[periodIdx];

        var catIdx = _viewModel.SelectedCategoryIndex;
        if (catIdx >= 0 && catIdx < _viewModel.CategoryFilterItems.Count)
            CategoryPicker.SelectedItem = _viewModel.CategoryFilterItems[catIdx];
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