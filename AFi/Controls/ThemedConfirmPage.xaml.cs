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

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult(true);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult(false);
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(false);
        return base.OnBackButtonPressed();
    }
}