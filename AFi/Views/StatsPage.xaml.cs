using AFi.ViewModels;

namespace AFi.Views;

public partial class StatsPage : ContentPage
{
    private readonly StatsViewModel _viewModel;

    public StatsPage(StatsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        SetupPeriodPicker();
    }

    private void SetupPeriodPicker()
    {
        PeriodPicker.ItemsSource = _viewModel.FilterOptions;

        var initialIdx = _viewModel.SelectedFilterIndex;
        if (initialIdx >= 0 && initialIdx < _viewModel.FilterOptions.Count)
            PeriodPicker.SelectedItem = _viewModel.FilterOptions[initialIdx];

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

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatsViewModel.SelectedFilterIndex))
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.Yield();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        var idx = _viewModel.SelectedFilterIndex;
        if (idx >= 0 && idx < _viewModel.FilterOptions.Count)
            PeriodPicker.SelectedItem = _viewModel.FilterOptions[idx];
    }
}