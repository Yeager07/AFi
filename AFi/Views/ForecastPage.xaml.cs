using AFi.ViewModels;

namespace AFi.Views;

public partial class ForecastPage : ContentPage
{
    private readonly ForecastViewModel _viewModel;

    public ForecastPage(ForecastViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Yield();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}