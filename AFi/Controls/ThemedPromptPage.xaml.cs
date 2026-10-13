using AFi.Helpers;

namespace AFi.Controls;

public partial class ThemedPromptPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();

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

    /// <summary>
    /// Возвращает Task, который завершится введённым текстом
    /// или null, если пользователь отменил.
    /// </summary>
    public Task<string?> WaitForResultAsync() => _tcs.Task;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Автофокус и открытие клавиатуры
        InputEntry.Focus();
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        KeyboardHelper.Hide();

        var result = InputEntry.Text;
        _tcs.TrySetResult(result);

        try
        {
            await Navigation.PopModalAsync();
        }
        catch { }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        KeyboardHelper.Hide();

        _tcs.TrySetResult(null);

        try
        {
            await Navigation.PopModalAsync();
        }
        catch { }
    }

    private void OnEntryCompleted(object? sender, EventArgs e)
    {
        // Пользователь нажал «ОК» на клавиатуре — считаем, что подтвердил
        OnAcceptClicked(sender, e);
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}