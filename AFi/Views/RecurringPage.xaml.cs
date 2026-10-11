using AFi.ViewModels;

namespace AFi.Views;

public partial class RecurringPage : ContentPage
{
    private readonly RecurringViewModel _viewModel;

    public RecurringPage(RecurringViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}