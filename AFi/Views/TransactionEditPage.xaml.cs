using AFi.ViewModels;

namespace AFi.Views;

public partial class TransactionEditPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly TransactionEditViewModel _viewModel;

    public TransactionEditPage(TransactionEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.EditCompleted += OnEditCompleted;
    }

    public Task<bool> WaitForResultAsync() => _tcs.Task;

    // ==================== Завершение ====================

    private async void OnEditCompleted(object? sender, bool saved)
    {
        if (!_tcs.TrySetResult(saved)) return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await Navigation.PopModalAsync(); }
            catch { }
        });
    }

    // ==================== Жизненный цикл ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsBusy) return true;
        _viewModel.CancelCommand.Execute(null);
        return true;
    }
}