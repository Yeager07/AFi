using AFi.Services;

namespace AFi.Views;

public partial class SplashPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly DatabaseService _db;
    private bool _navigated;

    public SplashPage(IServiceProvider services, DatabaseService db)
    {
        InitializeComponent();

        _services = services;
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_navigated) return;
        _navigated = true;

        try
        {
            // Прогрев БД — открытие connection, seed, миграции.
            // К моменту переключения на AppShell всё уже готово.
            await _db.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SplashPage] DB init failed: {ex.Message}");
        }

        // Минимальная пауза, чтобы splash не мигал (0.7 сек)
        await Task.Delay(700);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current?.Windows.Count > 0)
            {
                Application.Current.Windows[0].Page =
                    _services.GetRequiredService<AppShell>();
            }
        });
    }
}