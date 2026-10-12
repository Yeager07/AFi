using AFi.Helpers;
using AFi.ViewModels;

namespace AFi.Views;

public partial class RecurringEditPage : ContentPage, IQueryAttributable
{
    private readonly RecurringEditViewModel _viewModel;

    public RecurringEditPage(RecurringEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        SetupTypePicker();
        SetupCategoryPicker();
        SetupDayPicker();
        SetupDaysBeforePicker();
    }

    // ==================== Параметры навигации ====================

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        int? paymentId = null;

        if (query.TryGetValue("id", out var idObj)
            && int.TryParse(idObj?.ToString(), out var id))
        {
            paymentId = id;
        }

        _viewModel.InitializeForEdit(paymentId);
    }

    // ==================== Picker «Тип» ====================

    private void SetupTypePicker()
    {
        TypePicker.ItemsSource = _viewModel.TypeOptions;

        TypePicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && TypePicker.SelectedItem is string selected)
            {
                var idx = _viewModel.TypeOptions.IndexOf(selected);
                if (idx >= 0 && idx != _viewModel.SelectedTypeIndex)
                    _viewModel.SelectedTypeIndex = idx;
            }
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecurringEditViewModel.SelectedTypeIndex))
            {
                var idx = _viewModel.SelectedTypeIndex;
                if (idx >= 0 && idx < _viewModel.TypeOptions.Count)
                {
                    var next = _viewModel.TypeOptions[idx];
                    if (!Equals(TypePicker.SelectedItem, next))
                        TypePicker.SelectedItem = next;
                }
            }
        };
    }

    // ==================== Picker «Категория» ====================

    private void SetupCategoryPicker()
    {
        CategoryPicker.ItemsSource = _viewModel.AvailableCategories;

        CategoryPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && CategoryPicker.SelectedItem is Models.Category selected)
            {
                if (!ReferenceEquals(_viewModel.SelectedCategory, selected))
                    _viewModel.SelectedCategory = selected;
            }
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecurringEditViewModel.SelectedCategory))
            {
                var next = _viewModel.SelectedCategory;
                if (!ReferenceEquals(CategoryPicker.SelectedItem, next))
                    CategoryPicker.SelectedItem = next;
            }
        };
    }

    // ==================== Picker «День месяца» ====================

    private void SetupDayPicker()
    {
        DayPicker.ItemsSource = _viewModel.DayOptions;

        DayPicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && DayPicker.SelectedItem is int selected)
            {
                var idx = _viewModel.DayOptions.IndexOf(selected);
                if (idx >= 0 && idx != _viewModel.SelectedDayIndex)
                    _viewModel.SelectedDayIndex = idx;
            }
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecurringEditViewModel.SelectedDayIndex))
            {
                var idx = _viewModel.SelectedDayIndex;
                if (idx >= 0 && idx < _viewModel.DayOptions.Count)
                {
                    var next = _viewModel.DayOptions[idx];
                    if (!Equals(DayPicker.SelectedItem, next))
                        DayPicker.SelectedItem = next;
                }
            }
        };
    }

    // ==================== Picker «За сколько дней» ====================

    private void SetupDaysBeforePicker()
    {
        DaysBeforePicker.ItemsSource = _viewModel.DaysBeforeOptions;

        DaysBeforePicker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Controls.ThemedPicker.SelectedItem)
                && DaysBeforePicker.SelectedItem is string selected)
            {
                var idx = _viewModel.DaysBeforeOptions.IndexOf(selected);
                if (idx >= 0 && idx != _viewModel.SelectedDaysBeforeIndex)
                    _viewModel.SelectedDaysBeforeIndex = idx;
            }
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecurringEditViewModel.SelectedDaysBeforeIndex))
            {
                var idx = _viewModel.SelectedDaysBeforeIndex;
                if (idx >= 0 && idx < _viewModel.DaysBeforeOptions.Count)
                {
                    var next = _viewModel.DaysBeforeOptions[idx];
                    if (!Equals(DaysBeforePicker.SelectedItem, next))
                        DaysBeforePicker.SelectedItem = next;
                }
            }
        };
    }

    // ==================== Клавиатура ====================

    private void OnEntryCompleted(object? sender, EventArgs e)
    {
        KeyboardHelper.Hide();
    }

    // ==================== Жизненный цикл ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        CategoryPicker.ItemsSource = _viewModel.AvailableCategories;

        var typeIdx = _viewModel.SelectedTypeIndex;
        if (typeIdx >= 0 && typeIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[typeIdx];

        CategoryPicker.SelectedItem = _viewModel.SelectedCategory;

        var dayIdx = _viewModel.SelectedDayIndex;
        if (dayIdx >= 0 && dayIdx < _viewModel.DayOptions.Count)
            DayPicker.SelectedItem = _viewModel.DayOptions[dayIdx];

        var daysBeforeIdx = _viewModel.SelectedDaysBeforeIndex;
        if (daysBeforeIdx >= 0 && daysBeforeIdx < _viewModel.DaysBeforeOptions.Count)
            DaysBeforePicker.SelectedItem = _viewModel.DaysBeforeOptions[daysBeforeIdx];
    }
}