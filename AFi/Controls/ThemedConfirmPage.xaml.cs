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
        // Сначала отдаём результат — вызывающий код сразу продолжит работу.
        // Модалка закроется следом, уже без блокировки.
        _tcs.TrySetResult(true);

        try
        {
            await Navigation.PopModalAsync();
        }
        catch
        {
            // Тихо — если PopModal по какой-то причине не сработал,
            // не валим UI. Результат уже отдан.
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(false);

        try
        {
            await Navigation.PopModalAsync();
        }
        catch
        {
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(false);
        return base.OnBackButtonPressed();
    }
}