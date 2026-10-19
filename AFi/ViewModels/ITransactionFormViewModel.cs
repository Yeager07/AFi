using System.Collections.ObjectModel;
using AFi.Collections;
using AFi.Models;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

/// <summary>
/// Общий минимум для форм операций. Реализуется MainViewModel (форма на Главной)
/// и TransactionEditViewModel (модалка редактирования).
/// Позволяет вынести XAML-форму в TransactionFormView.
/// </summary>
public interface ITransactionFormViewModel : System.ComponentModel.INotifyPropertyChanged
{
    // ==================== Поля ====================
    string AmountText { get; set; }
    int SelectedTypeIndex { get; set; }
    Category? SelectedCategory { get; set; }
    DateTime Date { get; set; }
    string Note { get; set; }
    DateTime MaxDate { get; }

    // ==================== Флаги ====================
    bool IsEditing { get; }
    bool HasItems { get; }
    string ItemsTotalText { get; }
    bool HasAmountMismatch { get; }
    string AmountMismatchText { get; }
    bool HasReceipt { get; }
    ImageSource? CurrentReceiptImageSource { get; }

    // ==================== Коллекции ====================
    ObservableCollection<TransactionItem> CurrentItems { get; }
    ObservableRangeCollection<Category> AvailableCategories { get; }
    ObservableCollection<string> TypeOptions { get; }

    // ==================== Команды ====================
    IRelayCommand AddItemCommand { get; }
    IRelayCommand<TransactionItem?> RemoveItemCommand { get; }
    IAsyncRelayCommand PickReceiptFromGalleryCommand { get; }
    IAsyncRelayCommand CaptureReceiptFromCameraCommand { get; }
    IAsyncRelayCommand RemoveReceiptCommand { get; }
    IAsyncRelayCommand RecognizeReceiptCommand { get; }

    // ==================== Методы ====================
    void RefreshItemsTotal();
}