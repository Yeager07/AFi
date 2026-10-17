using AFi.Helpers;

namespace AFi.Controls;

public partial class ThemedPromptPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private bool _isClosing;

    public ThemedPromptPage(string title, string message,
                            string placeholder = "",
                            int maxLength = -1,
                            string initialValue = "")
    {
        InitializeComponent();

        TitleLabel.Text = title;

        if (!string.IsNullOrWhiteSpace(message))
        {
            MessageLabel.Text = message;
            MessageLabel.IsVisible = true;
        }

        InputEntry.Placeholder = placeholder;
        InputEntry.Text = initialValue;

        if (maxLength > 0)
            InputEntry.MaxLength = maxLength;
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.WhenAll(
            CardBorder.TranslateToAsync(0, 0, 250, Easing.CubicOut),
            CardBorder.FadeToAsync(1, 200));

        InputEntry.Focus();
    }

    private async Task CloseAsync(string? result)
    {
        if (_isClosing) return;
        _isClosing = true;

        KeyboardHelper.Hide();

        try { await Navigation.PopModalAsync(); }
        catch { }

        _tcs.TrySetResult(result);
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        await CloseAsync(InputEntry.Text);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private async void OnOutsideTapped(object? sender, TappedEventArgs e)
    {
        await CloseAsync(null);
    }

    private void OnEntryCompleted(object? sender, EventArgs e)
    {
        _ = CloseAsync(InputEntry.Text);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }
}