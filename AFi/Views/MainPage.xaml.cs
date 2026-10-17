using AFi.ViewModels;

namespace AFi.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsEditing)
                && _viewModel.IsEditing)
            {
                await MainScrollView.ScrollToAsync(0, 0, animated: true);
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Дать Shell стартовать анимацию перехода, потом уже грузить данные
        await Task.Yield();
        await _viewModel.LoadCommand.ExecuteAsync(null);
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