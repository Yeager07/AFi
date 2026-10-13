namespace AFi.Controls;

public partial class ThemedConfirmPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _tcs = new();

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

        CardBorder.TranslationY = 400;
        CardBorder.Opacity = 0;

        await Task.WhenAll(
            CardBorder.TranslateToAsync(0, 0, 250, Easing.CubicOut),
            CardBorder.FadeToAsync(1, 200));
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(true);

        try
        {
            await Navigation.PopModalAsync();
        }
        catch { }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(false);

        try
        {
            await Navigation.PopModalAsync();
        }
        catch { }
    }

    private async void OnOutsideTapped(object? sender, TappedEventArgs e)
    {
        _tcs.TrySetResult(false);

        try
        {
            await Navigation.PopModalAsync();
        }
        catch { }
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(false);
        return base.OnBackButtonPressed();
    }
}