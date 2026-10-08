using AFi.ViewModels;

namespace AFi.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
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
            return true;   // событие «съедено», приложение не закрывается
        }

        return base.OnBackButtonPressed();
    }
}