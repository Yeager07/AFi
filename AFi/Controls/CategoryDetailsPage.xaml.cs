using AFi.Models;

namespace AFi.Controls;

/// <summary>
/// Модальное окно с деталями категории из Статистики: топ-5 позиций по расходам.
/// Открывается из ActionSheet по тапу на карточку в легенде.
/// </summary>
public partial class CategoryDetailsPage : ContentPage
{
    private readonly TaskCompletionSource _tcs = new();

    public CategoryDetailsPage(LegendItem legendItem)
    {
        InitializeComponent();

        BindingContext = legendItem;

        // Если у категории нет позиций — показываем заглушку вместо пустого списка
        NoItemsLabel.IsVisible = legendItem.TopItems.Count == 0;
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

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult();
        return base.OnBackButtonPressed();
    }
}