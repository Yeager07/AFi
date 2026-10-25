namespace AFi.Models;

/// <summary>
/// Данные, которые пользователь вводит вручную для запроса чека из ФНС.
/// Все эти поля напечатаны на каждом чеке под QR-кодом (ФН, ФД, ФП, дата, итог).
/// Превращаются в строку формата QR-кода, чтобы использовать тот же API.
/// </summary>
public class ManualReceiptInput
{
    /// <summary>ФН — номер фискального накопителя (16 цифр).</summary>
    public string Fn { get; set; } = string.Empty;

    /// <summary>ФД — номер фискального документа.</summary>
    public string Fd { get; set; } = string.Empty;

    /// <summary>ФП — фискальный признак документа.</summary>
    public string Fp { get; set; } = string.Empty;

    /// <summary>Дата и время чека.</summary>
    public DateTime Date { get; set; } = DateTime.Now;

    /// <summary>Итоговая сумма (может быть null, если пользователь не ввёл).</summary>
    public decimal? Sum { get; set; }

    /// <summary>
    /// Собирает строку в формате QR-кода ФНС из введённых реквизитов.
    /// Формат: <c>t=YYYYMMDDTHHmm&amp;s=Sum&amp;fn=FN&amp;i=FD&amp;fp=FP&amp;n=1</c>
    /// </summary>
    public string ToQrRaw()
    {
        var parts = new List<string> { $"t={Date:yyyyMMdd}T{Date:HHmm}" };

        if (Sum.HasValue)
            parts.Add($"s={Sum.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");

        parts.Add($"fn={Fn}");
        parts.Add($"i={Fd}");
        parts.Add($"fp={Fp}");
        parts.Add("n=1");

        return string.Join("&", parts);
    }
}