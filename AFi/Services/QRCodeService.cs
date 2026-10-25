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
        // Если не вышло, вырезаем низ, увеличиваем, делаем ч/б + резкость.
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
    /// Вырезает нижние 50% фото, увеличивает в 3 раза, переводит в контрастный ч/б.
    /// Плюс дополнительная резкость (unsharp mask) — иногда помогает, если QR
    /// немного размыт при съёмке.
    /// </summary>
    private static void PreprocessForQR(string inputPath, string outputPath)
    {
        using var inputStream = File.OpenRead(inputPath);
        using var original = SKBitmap.Decode(inputStream);

        if (original == null)
            throw new Exception("Не удалось декодировать изображение.");

        // 1. Обрезка нижних 50% (захватываем половину чека, чтобы точно попасть на QR)
        int cropY = (int)(original.Height * 0.5);
        int cropH = original.Height - cropY;

        using var cropped = new SKBitmap(original.Width, cropH);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.DrawBitmap(original, new SKRect(0, cropY, original.Width, original.Height), new SKRect(0, 0, original.Width, cropH));
        }

        // 2. Увеличение x3 (чтобы ZXing / ML Kit увидели мелкие модули)
        int newWidth = cropped.Width * 3;
        int newHeight = cropped.Height * 3;
        using var resized = cropped.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear));

        // 3. Контраст + резкость (unsharp mask)
        using var result = new SKBitmap(newWidth, newHeight);
        using (var canvas = new SKCanvas(result))
        {
            // Матрица: жесткое увеличение контраста (почти бинаризация)
            var colorMatrix = new float[]
            {
                2.5f, 2.5f, 2.5f, 0, -200,
                2.5f, 2.5f, 2.5f, 0, -200,
                2.5f, 2.5f, 2.5f, 0, -200,
                0,    0,    0,    1, 0
            };

            using var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(colorMatrix),
                // Резкость: лёгкая свёртка 3×3 (unsharp mask-подобная)
                ImageFilter = SKImageFilter.CreateMatrixConvolution(
                    new SKSizeI(3, 3),
                    new float[] { -0.5f, -0.5f, -0.5f, -0.5f, 5f, -0.5f, -0.5f, -0.5f, -0.5f },
                    1f, 0f,
                    new SKPointI(1, 1),
                    SKShaderTileMode.Clamp,
                    false)
            };
            canvas.DrawBitmap(resized, 0, 0, paint);
        }

        using var image = SKImage.FromBitmap(result);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fileStream = File.Create(outputPath);
        data.SaveTo(fileStream);
    }
}