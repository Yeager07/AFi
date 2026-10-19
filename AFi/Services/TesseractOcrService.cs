using SkiaSharp;
using TesseractOcrMaui;
using TesseractOcrMaui.Enums;
using TesseractOcrMaui.Results;

namespace AFi.Services;

/// <summary>
/// Оффлайн-сервис распознавания текста на основе TesseractOcrMaui.
/// Работает на всех платформах (Android, iOS, Windows).
/// </summary>
public interface ITesseractOcrService
{
    Task<string> RecognizeTextAsync(string imagePath);
}

public class TesseractOcrService : ITesseractOcrService
{
    private readonly ITesseract _tesseract;

    public TesseractOcrService(ITesseract tesseract)
    {
        _tesseract = tesseract;
    }

    public async Task<string> RecognizeTextAsync(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new Exception($"Файл изображения не найден: {imagePath}");

        var tempPath = Path.Combine(FileSystem.CacheDirectory, $"ocr_processed_{Guid.NewGuid():N}.png");

        try
        {
            PreprocessImage(imagePath, tempPath);

            var result = await _tesseract.RecognizeTextAsync(tempPath);

            if (result.Status != RecognizionStatus.Success)
                throw new Exception($"Распознавание не удалось: {result.Status}");

            return result.RecognisedText ?? string.Empty;
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка Tesseract: {ex.Message}");
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    /// <summary>
    /// Мягкая предобработка: градации серого + умеренный контраст + лёгкая резкость.
    /// Без жёсткой бинаризации, чтобы не разрушать тонкие буквы.
    /// </summary>
    private static void PreprocessImage(string inputPath, string outputPath)
    {
        using var inputStream = File.OpenRead(inputPath);
        using var original = SKBitmap.Decode(inputStream);
        
        if (original == null)
            throw new Exception("Не удалось декодировать изображение.");

        // Умеренное уменьшение, если фото слишком большое
        var maxWidth = 2000;
        var scale = original.Width > maxWidth ? (float)maxWidth / original.Width : 1f;
        var newWidth = (int)(original.Width * scale);
        var newHeight = (int)(original.Height * scale);

        using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear));
        if (resized == null)
            throw new Exception("Не удалось изменить размер изображения.");

        using var result = new SKBitmap(newWidth, newHeight);
        using (var canvas = new SKCanvas(result))
        {
            // Умеренный контраст (1.3 вместо 1.8), без изменения яркости
            var contrast = 1.3f;
            var colorMatrix = new float[]
            {
                0.2126f * contrast, 0.7152f * contrast, 0.0722f * contrast, 0, 0,
                0.2126f * contrast, 0.7152f * contrast, 0.0722f * contrast, 0, 0,
                0.2126f * contrast, 0.7152f * contrast, 0.0722f * contrast, 0, 0,
                0,                  0,                  0,                  1, 0
            };

            using var paint = new SKPaint
            {
                ColorFilter = SKColorFilter.CreateColorMatrix(colorMatrix),
                // Лёгкое повышение резкости через свёртку
                ImageFilter = SKImageFilter.CreateMatrixConvolution(
                    new SKSizeI(3, 3),
                    new float[] { 0, -1, 0, -1, 5, -1, 0, -1, 0 },
                    1f, 0f,
                    new SKPointI(1, 1),
                    SKShaderTileMode.Clamp,
                    false)
            };

            canvas.DrawBitmap(resized, 0, 0, paint);
        }

        using var image = SKImage.FromBitmap(result);
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);
        using var fileStream = File.Create(outputPath);
        data.SaveTo(fileStream);
    }
}