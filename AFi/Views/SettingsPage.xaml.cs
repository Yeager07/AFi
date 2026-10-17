using AFi.Helpers;
using AFi.ViewModels;

namespace AFi.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private void OnInitialBalanceCompleted(object? sender, EventArgs e)
    {
        KeyboardHelper.Hide();
        _viewModel.SaveInitialBalanceCommand.Execute(null);
    }
}