namespace AFi.Controls;

public partial class ThemedConfirmPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private bool _isClosing;

    public ThemedConfirmPage(string title, string message,
                             string acceptText, string cancelText)
    {
        InitializeComponent();

        TitleLabel.Text = title;
        MessageLabel.Text = message;
        AcceptButton.Text = acceptText;
        CancelButton.Text = cancelText;
    }

    public Task<bool> WaitForResultAsync() => _tcs.Task;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.WhenAll(
            CardBorder.TranslateToAsync(0, 0, 250, Easing.CubicOut),
            CardBorder.FadeToAsync(1, 200));
    }

    private async Task CloseAsync(bool result)
    {
        if (_isClosing) return;
        _isClosing = true;

        try { await Navigation.PopModalAsync(); }
        catch { }

        _tcs.TrySetResult(result);
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        await CloseAsync(true);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await CloseAsync(false);
    }

    private async void OnOutsideTapped(object? sender, TappedEventArgs e)
    {
        await CloseAsync(false);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(false);
        return true;
    }
}