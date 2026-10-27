using AFi.Models;

namespace AFi.Controls;

/// <summary>
/// Модальное окно с деталями транзакции: позиции чека и фото.
/// Открывается из Истории по тапу «Подробнее» в ActionSheet.
/// </summary>
public partial class TransactionDetailsPage : ContentPage
{
    private readonly TaskCompletionSource _tcs = new();
    private readonly Transaction _transaction;

    public TransactionDetailsPage(Transaction transaction)
    {
        InitializeComponent();

        _transaction = transaction;
        BindingContext = transaction;

        // Если у операции нет ни позиций, ни фото — показываем заглушку
        NoExtrasLabel.IsVisible = !transaction.HasExtraDetails;
    }

    public Task WaitForCloseAsync() => _tcs.Task;

    // ==================== Обработчики ====================

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult();
        await Navigation.PopModalAsync();
    }

    private async void OnOutsideTapped(object? sender, TappedEventArgs e)
    {
        _tcs.TrySetResult();
        await Navigation.PopModalAsync();
    }

    private async void OnReceiptTapped(object? sender, TappedEventArgs e)
    {
        if (_transaction.ReceiptImageSource is null) return;

        // Открываем полноэкранный просмотр фото поверх модалки
        var viewerPage = new ReceiptViewerPage(_transaction.ReceiptImageSource);
        await Navigation.PushModalAsync(viewerPage);
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult();
        return base.OnBackButtonPressed();
    }
}