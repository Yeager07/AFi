using AFi.ViewModels;

namespace AFi.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage(HistoryViewModel viewModel)
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
    /// мультивыбора — отменяем выбор и не выходим со страницы.
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