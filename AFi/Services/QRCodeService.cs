using BarcodeScanning;
using SkiaSharp;

namespace AFi.Services;

/// <summary>
/// Сервис для декодирования фискального QR-кода с чека.
/// </summary>
public interface IQRCodeService
{
    Task<string?> DecodeFiscalQRCodeAsync(string imagePath);
}

public class QRCodeService : IQRCodeService
{
    private static readonly string[] FiscalIdMarkers = { "fn=", "fpd=" };
    private static readonly string[] FiscalSumMarkers = { "s=", "t=" };

    public async Task<string?> DecodeFiscalQRCodeAsync(string imagePath)
    {
        if (!File.Exists(imagePath))
            return null;

        // Шаг 1: Пробуем распознать оригинал через ML Kit
        var result = await TryMLKitAsync(imagePath);
        if (result != null)
            return result;

        // Шаг 2: Предобработка (для длинных чеков)
        var tempPath = Path.Combine(FileSystem.CacheDirectory, $"qr_processed_{Guid.NewGuid():N}.png");
        try
        {
            PreprocessForQR(imagePath, tempPath);
            return await TryMLKitAsync(tempPath);
        }
        catch
        {
            return null;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private async Task<string?> TryMLKitAsync(string path)
    {
        try
        {
            // Читаем файл в массив байтов
            var imageBytes = await File.ReadAllBytesAsync(path);
            
            // Передаём байты в ML Kit
            var barcodes = await BarcodeScanning.Methods.ScanFromImageAsync(imageBytes);

            if (barcodes == null) return null;

            foreach (var barcode in barcodes)
            {
                if (LooksLikeFiscalQR(barcode.DisplayValue))
                    return barcode.DisplayValue;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QRCodeService] MLKit scan error: {ex.Message}");
            return null;
        }
    }

    private static bool LooksLikeFiscalQR(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var lower = content.ToLowerInvariant();

        var hasId = FiscalIdMarkers.Any(m => lower.Contains(m));
        var hasSum = FiscalSumMarkers.Any(m => lower.Contains(m));

        return hasId && hasSum;
    }

    private static void PreprocessForQR(string inputPath, string outputPath)
    {
        using var inputStream = File.OpenRead(inputPath);
        using var original = SKBitmap.Decode(inputStream);
        if (original == null) throw new Exception("Не удалось декодировать изображение.");

        // Обрезка нижних 50%
        int cropY = (int)(original.Height * 0.5);
        int cropH = original.Height - cropY;

        using var cropped = new SKBitmap(original.Width, cropH);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.DrawBitmap(original, new SKRect(0, cropY, original.Width, original.Height), new SKRect(0, 0, original.Width, cropH));
        }

        // Увеличение x3
        int newWidth = cropped.Width * 3;
        int newHeight = cropped.Height * 3;
        using var resized = cropped.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear));

        // Контраст
        using var result = new SKBitmap(newWidth, newHeight);
        using (var canvas = new SKCanvas(result))
        {
            var matrix = new float[]
            {
                2.5f, 2.5f, 2.5f, 0, -200,
                2.5f, 2.5f, 2.5f, 0, -200,
                2.5f, 2.5f, 2.5f, 0, -200,
                0,    0,    0,    1, 0
            };
            using var paint = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(matrix) };
            canvas.DrawBitmap(resized, 0, 0, paint);
        }

        using var image = SKImage.FromBitmap(result);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var fileStream = File.Create(outputPath);
        data.SaveTo(fileStream);
    }
}