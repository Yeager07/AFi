namespace AFi.Controls;

public partial class ThemedActionSheetPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private bool _isClosing;

    public ThemedActionSheetPage(string title, string cancelText, string[] actions)
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(title))
        {
            TitleLabel.Text = title;
            TitleLabel.IsVisible = true;
            TitleDivider.IsVisible = true;
        }
        else
        {
            TitleLabel.IsVisible = false;
            TitleDivider.IsVisible = false;
        }

        if (CancelButton.Content is Label cancelLabel)
            cancelLabel.Text = cancelText;

        BuildActions(actions);
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    // ==================== Анимация открытия ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // CardBorder в XAML имеет TranslationY="400" и Opacity="0".
        // Анимируем появление карточки снизу.
        await Task.WhenAll(
            CardBorder.TranslateToAsync(0, 0, 250, Easing.CubicOut),
            CardBorder.FadeToAsync(1, 200));
    }

    // ==================== Закрытие ====================

    private async Task CloseAsync(string? result)
    {
        if (_isClosing) return;
        _isClosing = true;

        try { await Navigation.PopModalAsync(); }
        catch { }

        _tcs.TrySetResult(result);
    }

    // ==================== Построение кнопок ====================

    private void BuildActions(string[] actions)
    {
        foreach (var action in actions)
        {
            // ==================== Семантика кнопки ====================
            // Удалить — деструктивное действие (красный).
            // Подробнее / Просмотр / Открыть — нейтральное (только смотрит данные).
            // Всё остальное (Редактировать, Скачать и т.д.) — акцентное (PrimaryColor).

            var isDestructive = action.Equals("Удалить", StringComparison.OrdinalIgnoreCase);

            var isNeutral =
                action.Equals("Подробнее", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("Просмотр", StringComparison.OrdinalIgnoreCase) ||
                action.Equals("Открыть", StringComparison.OrdinalIgnoreCase);

            var isPrimary = !isDestructive && !isNeutral;

            var border = new Border
            {
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = new CornerRadius(8)
                },
                Padding = new Thickness(15, 12),
                BackgroundColor = Colors.Transparent,
            };

            var label = new Label
            {
                Text = action,
                FontSize = 15,
                FontAttributes = isNeutral ? FontAttributes.None : FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            };

            if (isDestructive)
            {
                border.SetDynamicResource(Border.StrokeProperty, "DangerColor");
                label.SetDynamicResource(Label.TextColorProperty, "DangerColor");
            }
            else if (isNeutral)
            {
                border.SetDynamicResource(Border.StrokeProperty, "CardBorderColor");
                label.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
            }
            else if (isPrimary)
            {
                border.SetDynamicResource(Border.StrokeProperty, "PrimaryColor");
                label.SetDynamicResource(Label.TextColorProperty, "PrimaryColor");
            }

            border.Content = label;

            var captured = action;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await CloseAsync(captured);
            border.GestureRecognizers.Add(tap);

            ActionsContainer.Children.Add(border);
        }
    }

    private async void OnCancelTapped(object? sender, TappedEventArgs e)
    {
        await CloseAsync(null);
    }

    private async void OnOutsideTapped(object? sender, TappedEventArgs e)
    {
        await CloseAsync(null);
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }
}