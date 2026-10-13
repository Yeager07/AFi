namespace AFi.Controls;

public partial class ThemedPickerModalPage : ContentPage
{
    private readonly System.Collections.IList _items;
    private readonly object? _selected;
    private readonly TaskCompletionSource<object?> _tcs = new();

    public ThemedPickerModalPage(System.Collections.IList items, object? selected, string title)
    {
        InitializeComponent();

        _items = items;
        _selected = selected;

        if (!string.IsNullOrWhiteSpace(title))
            HeaderLabel.Text = title;

        BuildItems();
    }

    public Task<object?> WaitForResultAsync() => _tcs.Task;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        CardBorder.TranslationY = 500;
        CardBorder.Opacity = 0;

        await Task.WhenAll(
            CardBorder.TranslateToAsync(0, 0, 250, Easing.CubicOut),
            CardBorder.FadeToAsync(1, 200));
    }

    private void BuildItems()
    {
        foreach (var item in _items)
        {
            var isSelected = ReferenceEquals(item, _selected)
                             || (item is not null && item.Equals(_selected));

            var textLabel = new Label
            {
                Text = item?.ToString() ?? string.Empty,
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center,
            };
            textLabel.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");

            var checkLabel = new Label
            {
                Text = isSelected ? "✓" : string.Empty,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End,
            };
            checkLabel.SetDynamicResource(Label.TextColorProperty, "PrimaryColor");

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new(GridLength.Star),
                    new(GridLength.Auto),
                },
                ColumnSpacing = 10,
            };
            grid.Add(textLabel, 0);
            grid.Add(checkLabel, 1);

            var row = new Border
            {
                Padding = new Thickness(20, 14),
                StrokeThickness = 0,
                BackgroundColor = Colors.Transparent,
                Content = grid,
            };

            if (isSelected)
                row.SetDynamicResource(BackgroundColorProperty, "DividerColor");

            var captured = item;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                await Navigation.PopModalAsync();
                _tcs.TrySetResult(captured);
            };
            row.GestureRecognizers.Add(tap);

            ItemsContainer.Children.Add(row);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult(null);
    }

    private async void OnOutsideTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PopModalAsync();
        _tcs.TrySetResult(null);
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}