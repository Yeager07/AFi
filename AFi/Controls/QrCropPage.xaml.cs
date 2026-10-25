using Microsoft.Maui.Graphics;
using SkiaSharp;
using BarcodeScanning;

namespace AFi.Controls;

public partial class QrCropPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private readonly string _imagePath;
    private PointF _startPoint, _currentPoint;
    private bool _isDrawing;

    public QrCropPage(string imagePath)
    {
        InitializeComponent();
        _imagePath = imagePath;
        ImageViewer.Source = ImageSource.FromFile(imagePath);
        SelectionCanvas.Drawable = new SelectionDrawable(this);
    }

    public Task<string?> WaitForResultAsync() => _tcs.Task;

    private void OnStartInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches is null || e.Touches.Length == 0) return;
        _startPoint = e.Touches[0];
        _currentPoint = _startPoint;
        _isDrawing = true;
        SelectionCanvas.Invalidate();
    }

    private void OnDragInteraction(object? sender, TouchEventArgs e)
    {
        if (!_isDrawing || e.Touches is null || e.Touches.Length == 0) return;
        _currentPoint = e.Touches[0];
        SelectionCanvas.Invalidate();
    }

    private void OnEndInteraction(object? sender, TouchEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;
        var rect = GetSelectionRect();
        ScanButton.IsEnabled = rect.Width > 30 && rect.Height > 30;
    }

    private RectF GetSelectionRect()
    {
        var minX = Math.Min(_startPoint.X, _currentPoint.X);
        var minY = Math.Min(_startPoint.Y, _currentPoint.Y);
        var maxX = Math.Max(_startPoint.X, _currentPoint.X);
        var maxY = Math.Max(_startPoint.Y, _currentPoint.Y);
        return new RectF(minX, minY, maxX - minX, maxY - minY);
    }

    private class SelectionDrawable : IDrawable
    {
        private readonly QrCropPage _owner;
        public SelectionDrawable(QrCropPage owner) => _owner = owner;
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var rect = _owner.GetSelectionRect();
            if (rect.Width <= 0 || rect.Height <= 0) return;
            canvas.FillColor = Color.FromRgba(0, 0, 0, 100);
            canvas.FillRectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, rect.Y - dirtyRect.Y);
            canvas.FillRectangle(dirtyRect.X, rect.Bottom, dirtyRect.Width, dirtyRect.Bottom - rect.Bottom);
            canvas.FillRectangle(dirtyRect.X, rect.Y, rect.X - dirtyRect.X, rect.Height);
            canvas.FillRectangle(rect.Right, rect.Y, dirtyRect.Right - rect.Right, rect.Height);
            canvas.StrokeColor = Colors.Lime;
            canvas.StrokeSize = 2f;
            canvas.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        ScanButton.IsEnabled = false;
        StatusLabel.Text = "Сканирование...";

        try
        {
            var rect = GetSelectionRect();
            if (rect.Width <= 0 || rect.Height <= 0) { ScanButton.IsEnabled = true; return; }

            double containerWidth = SelectionCanvas.Width > 0 ? SelectionCanvas.Width : ImageContainer.Width;
            double containerHeight = SelectionCanvas.Height > 0 ? SelectionCanvas.Height : ImageContainer.Height;

            using var inputStream = File.OpenRead(_imagePath);
            using var original = SKBitmap.Decode(inputStream);
            if (original == null) throw new Exception("Не удалось прочитать фото");

            double imageAspect = (double)original.Width / original.Height;
            double containerAspect = containerWidth / containerHeight;

            double rw, rh, ox = 0, oy = 0;
            if (imageAspect > containerAspect) { rw = containerWidth; rh = rw / imageAspect; oy = (containerHeight - rh) / 2; }
            else { rh = containerHeight; rw = rh * imageAspect; ox = (containerWidth - rw) / 2; }

            double sx = original.Width / rw;
            double sy = original.Height / rh;

            var cropRect = new SKRect(
                (float)((rect.X - ox) * sx),
                (float)((rect.Y - oy) * sy),
                (float)((rect.X + rect.Width - ox) * sx),
                (float)((rect.Y + rect.Height - oy) * sy));
            cropRect.Intersect(new SKRect(0, 0, original.Width, original.Height));

            if (cropRect.Width < 30 || cropRect.Height < 30) { StatusLabel.Text = "Слишком маленькая область"; ScanButton.IsEnabled = true; return; }

            var logs = new List<string>();
            string debug = $"R={cropRect.Width:F0}x{cropRect.Height:F0}";

            // ==================== Стратегия 0: прямой скан кропа без нарезки ====================
            // Проверяем, работает ли ML Kit вообще на этом кропе.
            if (await TryScanRegion(original, cropRect, upscale: 1, SKFilterQuality.None, binarize: false, useJpeg: false, tag: "full", logs))
            {
                await Navigation.PopModalAsync();
                return;
            }

            // ==================== Стратегия 1: кроп с ×2 + контраст ====================
            if (await TryScanRegion(original, cropRect, upscale: 2, SKFilterQuality.Medium, binarize: false, useJpeg: false, tag: "full×2", logs))
            {
                await Navigation.PopModalAsync();
                return;
            }

            // ==================== Стратегия 2: кроп с ×2 в JPEG ====================
            if (await TryScanRegion(original, cropRect, upscale: 2, SKFilterQuality.Medium, binarize: false, useJpeg: true, tag: "full×2jpg", logs))
            {
                await Navigation.PopModalAsync();
                return;
            }

            // ==================== Стратегия 3: плитки ====================
            // Если прямой скан не сработал, нарезаем кроп на плитки 400x400
            // и пробуем каждую отдельно.
            int tileSize = 400;
            int stride = 300;

            var tiles = new List<SKRect>();
            for (int y = (int)cropRect.Top; y < cropRect.Bottom; y += stride)
            {
                for (int x = (int)cropRect.Left; x < cropRect.Right; x += stride)
                {
                    var tile = new SKRect(
                        x, y,
                        Math.Min(x + tileSize, cropRect.Right),
                        Math.Min(y + tileSize, cropRect.Bottom));
                    if (tile.Width >= 100 && tile.Height >= 100)
                        tiles.Add(tile);
                }
            }

            debug += $" tiles={tiles.Count}";

            foreach (var tile in tiles)
            {
                // Плитка ×3 + Rgba8888
                if (await TryScanRegion(original, tile, upscale: 3, SKFilterQuality.High, binarize: false, useJpeg: false, tag: "×3", logs))
                {
                    await Navigation.PopModalAsync();
                    return;
                }

                // Плитка ×3 + JPEG
                if (await TryScanRegion(original, tile, upscale: 3, SKFilterQuality.High, binarize: false, useJpeg: true, tag: "×3jpg", logs))
                {
                    await Navigation.PopModalAsync();
                    return;
                }

                // Плитка ×3 + бинаризация
                if (await TryScanRegion(original, tile, upscale: 3, SKFilterQuality.High, binarize: true, useJpeg: false, tag: "bin", logs))
                {
                    await Navigation.PopModalAsync();
                    return;
                }
            }

            StatusLabel.Text = $"{debug}\n{string.Join(" ", logs.Take(20))}";
            ScanButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Ошибка: {ex.Message}";
            ScanButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Сканирует одну область: апскейл → кодирование (PNG Rgba8888 или JPEG) → ML Kit/ZXing.
    /// </summary>
    private async Task<bool> TryScanRegion(SKBitmap original, SKRect region, int upscale, SKFilterQuality mode, bool binarize, bool useJpeg, string tag, List<string> logs)
    {
        try
        {
            int tw = (int)region.Width * upscale;
            int th = (int)region.Height * upscale;

            // Используем Rgba8888 — стандартный формат для Android
            using var final = new SKBitmap(tw, th, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(final))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { FilterQuality = mode };
                canvas.DrawBitmap(original, region, new SKRect(0, 0, tw, th), paint);
            }

            if (binarize)
                BinarizeOtsu(final);

            byte[] bytes;
            using (var img = SKImage.FromBitmap(final))
            using (var d = img.Encode(useJpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png, 95))
            {
                bytes = d.ToArray();
            }

            // ML Kit
            try
            {
                var bc = await BarcodeScanning.Methods.ScanFromImageAsync(bytes);
                if (bc != null && bc.Count > 0)
                {
                    foreach (var b in bc)
                    {
                        if (!string.IsNullOrEmpty(b.DisplayValue) &&
                            (b.DisplayValue.Contains("fn=") || b.DisplayValue.Contains("fpd=")))
                        {
                            _tcs.TrySetResult(b.DisplayValue);
                            return true;
                        }
                    }
                    logs.Add($"{tag}:ML{bc.Count}");
                }
                else
                {
                    logs.Add($"{tag}:ML0");
                }
            }
            catch (Exception ex)
            {
                logs.Add($"{tag}:MLe({ex.GetType().Name})");
            }

            // ZXing fallback
            try
            {
                var tempPath = Path.Combine(FileSystem.CacheDirectory, $"qr_{Guid.NewGuid():N}.png");
                File.WriteAllBytes(tempPath, bytes);
                var zr = await ZXing.Net.Maui.BarcodeReader.DecodeFromFileAsync(
                    tempPath,
                    new ZXing.Net.Maui.BarcodeReaderOptions
                    {
                        Formats = ZXing.Net.Maui.BarcodeFormats.TwoDimensional,
                        AutoRotate = true,
                        TryHarder = true,
                        Multiple = true
                    });
                try { File.Delete(tempPath); } catch { }

                if (zr != null && zr.Length > 0)
                {
                    foreach (var z in zr)
                    {
                        if (!string.IsNullOrEmpty(z?.Value) &&
                            (z.Value.Contains("fn=") || z.Value.Contains("fpd=")))
                        {
                            _tcs.TrySetResult(z.Value);
                            return true;
                        }
                    }
                    logs.Add($"{tag}:ZX{zr.Length}");
                }
            }
            catch { }

            return false;
        }
        catch (Exception ex)
        {
            logs.Add($"{tag}:{ex.Message}");
            return false;
        }
    }

    private static void BinarizeOtsu(SKBitmap bitmap)
    {
        var pixels = bitmap.Pixels;
        var hist = new int[256];
        foreach (var p in pixels)
        {
            int b = (int)(0.299 * p.Red + 0.587 * p.Green + 0.114 * p.Blue);
            hist[Math.Clamp(b, 0, 255)]++;
        }

        long sum = 0;
        for (int i = 0; i < 256; i++) sum += i * hist[i];
        long sumB = 0; int wB = 0; int wF = 0;
        double varMax = 0; int threshold = 128;

        for (int t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            wF = pixels.Length - wB;
            if (wF == 0) break;
            sumB += t * hist[t];
            double mB = (double)sumB / wB;
            double mF = (double)(sum - sumB) / wF;
            double v = (double)wB * wF * (mB - mF) * (mB - mF);
            if (v > varMax) { varMax = v; threshold = t; }
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            int b = (int)(0.299 * p.Red + 0.587 * p.Green + 0.114 * p.Blue);
            pixels[i] = b < threshold ? SKColors.Black : SKColors.White;
        }
        bitmap.Pixels = pixels;
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