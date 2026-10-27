namespace AFi.Controls;

public partial class ThemedPicker : ContentView
{
    public ThemedPicker()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    // ==================== ItemsSource ====================

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(
            nameof(ItemsSource),
            typeof(System.Collections.IList),
            typeof(ThemedPicker),
            default(System.Collections.IList),
            propertyChanged: OnAnyChanged);

    public System.Collections.IList? ItemsSource
    {
        get => (System.Collections.IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    // ==================== SelectedItem ====================

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(
            nameof(SelectedItem),
            typeof(object),
            typeof(ThemedPicker),
            default(object),
            defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnAnyChanged);

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    // ==================== Placeholder ====================

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(
            nameof(Placeholder),
            typeof(string),
            typeof(ThemedPicker),
            string.Empty,
            propertyChanged: OnAnyChanged);

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    // ==================== Title ====================

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(
            nameof(Title),
            typeof(string),
            typeof(ThemedPicker),
            string.Empty);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // ==================== Отображение ====================

    private static void OnAnyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ThemedPicker picker)
            picker.UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (SelectedItem is null)
        {
            DisplayLabel.Text = string.IsNullOrEmpty(Placeholder) ? "—" : Placeholder;
            DisplayLabel.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        }
        else
        {
            DisplayLabel.Text = SelectedItem.ToString() ?? string.Empty;
            DisplayLabel.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        }
    }

    // ==================== Открытие модальной страницы ====================

    private DateTime _lastOpenAttempt = DateTime.MinValue;

    private async void OnTapped(object? sender, EventArgs e)
    {
        if (!IsEnabled) return;
        if (ItemsSource is null || ItemsSource.Count == 0) return;

        // Защита от двойного тапа: игнорируем повторный тап в течение 400 мс.
        // Это заменяет старый флаг _isOpeningModal, который мог «застрять»
        // в true, если модалка не закрылась корректно (например, при навигации).
        var now = DateTime.UtcNow;
        if ((now - _lastOpenAttempt).TotalMilliseconds < 400) return;
        _lastOpenAttempt = now;

        try
        {
            var page = FindParentPage();
            if (page is null) return;

            var modalPage = new ThemedPickerModalPage(ItemsSource, SelectedItem, Title);

            await page.Navigation.PushModalAsync(modalPage, animated: false);

            var result = await modalPage.WaitForResultAsync();

            if (result is not null)
            {
                SelectedItem = result;
            }
        }
        catch
        {
            // Молча выходим — если что-то упало в UI, чтобы не блокировать приложение
        }
    }

    private Page? FindParentPage()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is Page page) return page;
            current = current.Parent;
        }
        return Application.Current?.Windows.FirstOrDefault()?.Page;
    }
}