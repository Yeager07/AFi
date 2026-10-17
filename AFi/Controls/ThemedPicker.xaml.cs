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

    // ==================== Title (заголовок модалки) ====================

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
            // SetDynamicResource вместо прямого присваивания — тогда Label
            // остаётся подписан на ключ темы и перекрашивается при смене темы.
            DisplayLabel.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
        }
        else
        {
            DisplayLabel.Text = SelectedItem.ToString() ?? string.Empty;
            DisplayLabel.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        }
    }

    // ==================== Открытие модальной страницы ====================

    private bool _isOpeningModal;

    private async void OnTapped(object? sender, EventArgs e)
    {
        if (!IsEnabled) return;
        if (ItemsSource is null || ItemsSource.Count == 0) return;

        // Защита от двойного тапа — пока открыта модалка, повторный тап игнорируем
        if (_isOpeningModal) return;
        _isOpeningModal = true;

        try
        {
            var page = FindParentPage();
            if (page is null) return;

            var modalPage = new ThemedPickerModalPage(ItemsSource, SelectedItem, Title);

            await page.Navigation.PushModalAsync(modalPage);

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
        finally
        {
            _isOpeningModal = false;
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