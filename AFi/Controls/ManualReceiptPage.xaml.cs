using System.Globalization;
using AFi.Models;

namespace AFi.Controls;

public partial class ManualReceiptPage : ContentPage
{
    private readonly TaskCompletionSource<ManualReceiptInput?> _tcs = new();

    public ManualReceiptPage()
    {
        InitializeComponent();

        DatePicker.Date = DateTime.Today;
        TimePicker.Time = DateTime.Now.TimeOfDay;
    }

    public Task<ManualReceiptInput?> WaitForResultAsync() => _tcs.Task;

    // ==================== Валидация и отправка ====================

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        var fn = (FnEntry.Text ?? string.Empty).Trim();
        var fd = (FdEntry.Text ?? string.Empty).Trim();
        var fp = (FpEntry.Text ?? string.Empty).Trim();
        var sumText = (SumEntry.Text ?? string.Empty).Trim().Replace(',', '.');

        // ФН — ровно 16 цифр
        if (fn.Length != 16 || !fn.All(char.IsDigit))
        {
            ShowError("ФН должен состоять из 16 цифр");
            return;
        }

        // ФД — цифры, длина от 1 до 10
        if (string.IsNullOrEmpty(fd) || !fd.All(char.IsDigit) || fd.Length > 10)
        {
            ShowError("ФД должен быть числом (до 10 цифр)");
            return;
        }

        // ФП — цифры, длина от 1 до 10
        if (string.IsNullOrEmpty(fp) || !fp.All(char.IsDigit) || fp.Length > 10)
        {
            ShowError("ФП должен быть числом (до 10 цифр)");
            return;
        }

        // Сумма — необязательно, но если введена — должна быть числом
        decimal? sum = null;
        if (!string.IsNullOrEmpty(sumText))
        {
            if (!decimal.TryParse(sumText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedSum))
            {
                ShowError("Сумма должна быть числом");
                return;
            }
            sum = parsedSum;
        }

        // В MAUI 10 и DatePicker.Date, и TimePicker.Time — nullable.
        // Разыменовываем с fallback на текущие дату/время.
        DateTime selectedDate = DatePicker.Date ?? DateTime.Today;
        TimeSpan selectedTime = TimePicker.Time ?? DateTime.Now.TimeOfDay;

        DateTime dateTime = selectedDate.Date + selectedTime;

        var input = new ManualReceiptInput
        {
            Fn = fn,
            Fd = fd,
            Fp = fp,
            Date = dateTime,
            Sum = sum,
        };

        // ВРЕМЕННО: показываем строку, которая уйдёт на сервер
        var qrRaw = input.ToQrRaw();
        await DisplayAlertAsync("Проверка строки", qrRaw, "OK");

        _tcs.TrySetResult(input);
        await Navigation.PopModalAsync();
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}