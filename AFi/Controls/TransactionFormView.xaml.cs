using System.ComponentModel;
using AFi.Helpers;
using AFi.Models;
using AFi.ViewModels;

namespace AFi.Controls;

public partial class TransactionFormView : ContentView
{
    public static readonly BindableProperty FormViewModelProperty =
        BindableProperty.Create(
            nameof(FormViewModel),
            typeof(ITransactionFormViewModel),
            typeof(TransactionFormView),
            default(ITransactionFormViewModel),
            propertyChanged: OnFormViewModelChanged);

    public ITransactionFormViewModel? FormViewModel
    {
        get => (ITransactionFormViewModel?)GetValue(FormViewModelProperty);
        set => SetValue(FormViewModelProperty, value);
    }

    public TransactionFormView()
    {
        InitializeComponent();
    }

    private static void OnFormViewModelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not TransactionFormView view) return;

        if (oldValue is ITransactionFormViewModel oldVm)
            oldVm.PropertyChanged -= view.OnVmPropertyChanged;

        view.BindingContext = newValue;

        if (newValue is ITransactionFormViewModel newVm)
            newVm.PropertyChanged += view.OnVmPropertyChanged;

        view.SetupPickers();
    }

    private void SetupPickers()
    {
        if (FormViewModel is null) return;

        // ==================== Type Picker ====================
        TypePicker.ItemsSource = FormViewModel.TypeOptions;

        var typeIdx = FormViewModel.SelectedTypeIndex;
        if (typeIdx >= 0 && typeIdx < FormViewModel.TypeOptions.Count)
            TypePicker.SelectedItem = FormViewModel.TypeOptions[typeIdx];

        TypePicker.PropertyChanged -= OnTypePickerChanged;
        TypePicker.PropertyChanged += OnTypePickerChanged;

        // ==================== Category Picker ====================
        CategoryPicker.ItemsSource = FormViewModel.AvailableCategories;
        CategoryPicker.SelectedItem = FormViewModel.SelectedCategory;

        CategoryPicker.PropertyChanged -= OnCategoryPickerChanged;
        CategoryPicker.PropertyChanged += OnCategoryPickerChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (FormViewModel is null) return;

        if (e.PropertyName == nameof(ITransactionFormViewModel.SelectedTypeIndex))
        {
            var idx = FormViewModel.SelectedTypeIndex;
            if (idx >= 0 && idx < FormViewModel.TypeOptions.Count)
            {
                var next = FormViewModel.TypeOptions[idx];
                if (!Equals(TypePicker.SelectedItem, next))
                    TypePicker.SelectedItem = next;
            }
        }
        else if (e.PropertyName == nameof(ITransactionFormViewModel.SelectedCategory))
        {
            var next = FormViewModel.SelectedCategory;
            if (!ReferenceEquals(CategoryPicker.SelectedItem, next))
                CategoryPicker.SelectedItem = next;
        }
    }

    private void OnTypePickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (FormViewModel is null) return;

        if (e.PropertyName == nameof(ThemedPicker.SelectedItem)
            && TypePicker.SelectedItem is string selected)
        {
            var idx = FormViewModel.TypeOptions.IndexOf(selected);
            if (idx >= 0 && idx != FormViewModel.SelectedTypeIndex)
                FormViewModel.SelectedTypeIndex = idx;
        }
    }

    private void OnCategoryPickerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (FormViewModel is null) return;

        if (e.PropertyName == nameof(ThemedPicker.SelectedItem)
            && CategoryPicker.SelectedItem is Category selected)
        {
            if (!ReferenceEquals(FormViewModel.SelectedCategory, selected))
                FormViewModel.SelectedCategory = selected;
        }
    }

    private void OnItemEntryChanged(object? sender, TextChangedEventArgs e)
    {
        FormViewModel?.RefreshItemsTotal();
    }

    private void OnEntryCompleted(object? sender, EventArgs e)
    {
        KeyboardHelper.Hide();
    }
}