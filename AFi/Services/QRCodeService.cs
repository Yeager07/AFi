using SkiaSharp;
using ZXing.Net.Maui;

namespace AFi.Services;

/// <summary>
/// Сервис для декодирования фискального QR-кода с чека.
/// На чеке обычно 3 QR: фискальный и два промо. Отличаем по маркерам
/// реквизитов из 54-ФЗ (fn=, fp=, i=, t=, s=).
/// </summary>
public interface IQRCodeService
{
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

        // ==================== Шаг 1: Пробуем распознать оригинал ====================
        var originalResult = await TryDecodeAsync(imagePath);
        if (originalResult != null)
            return originalResult;

        // ==================== Шаг 2: Каскадная предобработка ====================
        // Если не вышло, вырезаем низ, увеличиваем и делаем ч/б.
        // Это помогает, если QR-код мелкий и находится в самом низу длинного чека.
        var tempPath = Path.Combine(FileSystem.CacheDirectory, $"qr_processed_{Guid.NewGuid():N}.png");
        try
        {
            PreprocessForQR(imagePath, tempPath);
            return await TryDecodeAsync(tempPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QRCodeService] Preprocess Error: {ex.Message}");
            return null;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// Пытается декодировать все QR-коды на фото и вернуть первый фискальный.
    /// </summary>
    private async Task<string?> TryDecodeAsync(string path)
    {
        try
        {
            // Multiple = true — вернуть ВСЕ QR-коды с изображения
            var results = await BarcodeReader.DecodeFromFileAsync(
                path,
                new BarcodeReaderOptions
                {
                    Formats = BarcodeFormats.TwoDimensional,
                    AutoRotate = true,
                    TryHarder = true,
                    Multiple = true,
                });

            if (results is null)
                return null;

            foreach (var result in results)
            {
                var value = result?.Value;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (LooksLikeFiscalQR(value))
                    return value;
            }

            return null;
        }
        catch
        {
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

    /// <summary>
    /// Вырезает нижние 40% фото, увеличивает в 2 раза, переводит в ч/б.
    /// Помогает, если QR-код мелкий и находится в самом низу длинного чека.
    /// </summary>
    private static void PreprocessForQR(string inputPath, string outputPath)
    {
        using var inputStream = File.OpenRead(inputPath);
        using var original = SKBitmap.Decode(inputStream);

        if (original == null)
            throw new Exception("Не удалось декодировать изображение.");

        // 1. Обрезка нижних 40% (где обычно находится QR)
        int cropY = (int)(original.Height * 0.6);
        int cropH = original.Height - cropY;

        using var cropped = new SKBitmap(original.Width, cropH);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.DrawBitmap(original, new SKRect(0, cropY, original.Width, original.Height), new SKRect(0, 0, original.Width, cropH));
        }

        // 2. Увеличение x2 (чтобы ZXing увидел мелкие модули)
        using var resized = cropped.Resize(new SKImageInfo(cropped.Width * 2, cropped.Height * 2), new SKSamplingOptions(SKFilterMode.Linear));

        // 3. Перевод в ч/б с высоким контрастом
        using var result = new SKBitmap(resized.Width, resized.Height);
        using (var canvas = new SKCanvas(result))
        {
            // Матрица: сильное увеличение контраста для ч/б
            var matrix = new float[]
            {
                1.5f, 1.5f, 1.5f, 0, -128,
                1.5f, 1.5f, 1.5f, 0, -128,
                1.5f, 1.5f, 1.5f, 0, -128,
                0,    0,    0,    1, 0
            };
            using var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(matrix)
            };
            canvas.DrawBitmap(resized, 0, 0, paint);
        }

        using var image = SKImage.FromBitmap(result);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fileStream = File.Create(outputPath);
        data.SaveTo(fileStream);
    }
}