namespace AFi.Controls;

public partial class ReceiptViewerPage : ContentPage
{
    private const double MinScale = 1.0;
    private const double MaxScale = 5.0;

    private double _currentScale = 1.0;
    private double _startScale = 1.0;

    private double _xOffset = 0;
    private double _yOffset = 0;
    private double _startXOffset = 0;
    private double _startYOffset = 0;

    public ReceiptViewerPage(ImageSource source)
    {
        InitializeComponent();
        ImageViewer.Source = source;

        // Зум и панорамирование относительно центра
        ImageViewer.AnchorX = 0.5;
        ImageViewer.AnchorY = 0.5;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    // ==================== Pinch (зум) ====================

    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _startScale = ImageViewer.Scale;
        }
        else if (e.Status == GestureStatus.Running)
        {
            _currentScale = _startScale * e.Scale;
            _currentScale = Math.Max(MinScale, Math.Min(MaxScale, _currentScale));

            ImageViewer.Scale = _currentScale;
        }
        else if (e.Status == GestureStatus.Completed)
        {
            // Если вернулись к исходному масштабу — сбрасываем смещение
            if (Math.Abs(_currentScale - MinScale) < 0.05)
            {
                _currentScale = MinScale;
                ImageViewer.Scale = MinScale;

                _xOffset = 0;
                _yOffset = 0;
                ImageViewer.TranslationX = 0;
                ImageViewer.TranslationY = 0;
            }
        }
    }

    // ==================== Pan (панорамирование при зуме) ====================

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        // Панорамирование работает только при масштабе > 1
        if (_currentScale <= MinScale + 0.01)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _startXOffset = _xOffset;
                _startYOffset = _yOffset;
                break;

            case GestureStatus.Running:
                _xOffset = _startXOffset + e.TotalX;
                _yOffset = _startYOffset + e.TotalY;

                ImageViewer.TranslationX = _xOffset;
                ImageViewer.TranslationY = _yOffset;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                // Не ограничиваем смещение — пользователь сам вернётся жестом
                break;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = Navigation.PopModalAsync();
        return true;
    }
}