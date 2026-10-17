using AFi.Helpers;
using AFi.ViewModels;

namespace AFi.Views;

public partial class TransactionEditPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private readonly TransactionEditViewModel _viewModel;

    public TransactionEditPage(TransactionEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.EditCompleted += OnEditCompleted;

        SetupTypePicker();
        SetupCategoryPicker();
    }

    public Task<bool> WaitForResultAsync() => _tcs.Task;

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
            if (e.PropertyName == nameof(TransactionEditViewModel.SelectedTypeIndex))
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
            if (e.PropertyName == nameof(TransactionEditViewModel.SelectedCategory))
            {
                var next = _viewModel.SelectedCategory;
                if (!ReferenceEquals(CategoryPicker.SelectedItem, next))
                    CategoryPicker.SelectedItem = next;
            }
        };
    }

    // ==================== Вспомогательные ====================

    private void OnItemEntryChanged(object? sender, TextChangedEventArgs e)
    {
        _viewModel.RefreshItemsTotal();
    }

    private void OnEntryCompleted(object? sender, EventArgs e)
    {
        KeyboardHelper.Hide();
    }

    // ==================== Завершение ====================

    private async void OnEditCompleted(object? sender, bool saved)
    {
        if (!_tcs.TrySetResult(saved)) return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await Navigation.PopModalAsync(); }
            catch { }
        });
    }

    // ==================== Жизненный цикл ====================

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();

        var typeIdx = _viewModel.SelectedTypeIndex;
        if (typeIdx >= 0 && typeIdx < _viewModel.TypeOptions.Count)
            TypePicker.SelectedItem = _viewModel.TypeOptions[typeIdx];

        CategoryPicker.SelectedItem = _viewModel.SelectedCategory;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsBusy) return true;
        _viewModel.CancelCommand.Execute(null);
        return true;
    }
}