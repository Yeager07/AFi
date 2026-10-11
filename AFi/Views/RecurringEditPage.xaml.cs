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

        var initialIdx = _viewModel.SelectedTypeIndex;
        if (initialIdx >= 0 && initialIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[initialIdx];

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

        var initialIdx = _viewModel.SelectedDayIndex;
        if (initialIdx >= 0 && initialIdx < _viewModel.DayOptions.Count)
            DayPicker.SelectedItem = _viewModel.DayOptions[initialIdx];

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

    // ==================== Жизненный цикл ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        // После загрузки — синхронизируем все Picker'ы
        CategoryPicker.ItemsSource = _viewModel.AvailableCategories;

        var typeIdx = _viewModel.SelectedTypeIndex;
        if (typeIdx >= 0 && typeIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[typeIdx];

        CategoryPicker.SelectedItem = _viewModel.SelectedCategory;

        var dayIdx = _viewModel.SelectedDayIndex;
        if (dayIdx >= 0 && dayIdx < _viewModel.DayOptions.Count)
            DayPicker.SelectedItem = _viewModel.DayOptions[dayIdx];
    }
}