using ZXing.Net.Maui;

namespace AFi.Services;

/// <summary>
/// Сервис для декодирования фискального QR-кода с чека.
/// На чеке обычно 3 QR: фискальный и два промо. Отличаем по маркерам
/// реквизитов из 54-ФЗ (fn=, fp=, i=, t=, s=).
/// </summary>
public interface IQRCodeService
{
    /// <summary>
    /// Декодирует все QR-коды с фото и возвращает первый фискальный.
    /// null — если фискальный QR не найден.
    /// </summary>
    Task<string?> DecodeFiscalQRCodeAsync(string imagePath);
}

public class QRCodeService : IQRCodeService
{
    // Маркеры фискального QR (54-ФЗ). Хотя бы один из группы A
    // и хотя бы один из группы B должны присутствовать.
    private static readonly string[] FiscalIdMarkers = { "fn=", "fpd=" };
    private static readonly string[] FiscalSumMarkers = { "s=", "t=" };

    public async Task<string?> DecodeFiscalQRCodeAsync(string imagePath)
    {
        if (!File.Exists(imagePath))
            return null;

        try
        {
            // Multiple = true — вернуть ВСЕ QR-коды с изображения
            var results = await BarcodeReader.DecodeFromFileAsync(
                imagePath,
                new BarcodeReaderOptions
                {
                    Formats = BarcodeFormats.TwoDimensional,
                    AutoRotate = true,
                    TryHarder = true,
                    Multiple = true,
                });

            if (results is null || results.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[QR] Ни одного QR-кода не найдено на фото.");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[QR] Найдено QR-кодов: {results.Length}");
            
            // Логируем все найденные коды для отладки
            foreach (var r in results)
            {
                System.Diagnostics.Debug.WriteLine($"[QR] Raw value: {r?.Value}");
            }

            // Ищем первый QR с фискальными маркерами
            foreach (var result in results)
            {
                var value = result?.Value;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (LooksLikeFiscalQR(value))
                {
                    System.Diagnostics.Debug.WriteLine($"[QR] ФИСКАЛЬНЫЙ QR ВЫБРАН: {value}");
                    return value;
                }
            }

            // Если по маркерам не нашли — возвращаем null (промо не трогаем)
            System.Diagnostics.Debug.WriteLine("[QR] Ни один из QR-кодов не похож на фискальный.");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QRCodeService] Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Проверяет, содержит ли содержимое QR-кода фискальные реквизиты.
    /// </summary>
    private static bool LooksLikeFiscalQR(string content)
    {
        var lower = content.ToLowerInvariant();

        var hasId = false;
        foreach (var m in FiscalIdMarkers)
            if (lower.Contains(m)) { hasId = true; break; }

        if (!hasId) return false;

        var hasSum = false;
        foreach (var m in FiscalSumMarkers)
            if (lower.Contains(m)) { hasSum = true; break; }

        return hasSum;
    }
}