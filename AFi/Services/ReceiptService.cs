namespace AFi.Services;

public class ReceiptService : IReceiptService
{
    private const string ReceiptsFolder = "receipts";

    // ==================== Выбор фото ====================

    public async Task<string?> PickFromGalleryAsync()
    {
        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Выберите фото чека",
                // SelectionLimit не указываем — по умолчанию можно выбрать несколько,
                // но мы берём только первый файл
            });

            var result = results?.FirstOrDefault();
            if (result is null)
                return null;

            return await SaveResultAsync(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReceiptService] Pick failed: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> CaptureFromCameraAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
                return null;

            var result = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Сфотографируйте чек",
            });

            if (result is null)
                return null;

            return await SaveResultAsync(result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReceiptService] Capture failed: {ex.Message}");
            return null;
        }
    }

    // ==================== Сохранение ====================

    private static async Task<string> SaveResultAsync(FileResult result)
    {
        var folder = GetReceiptsFolder();
        Directory.CreateDirectory(folder);

        var extension = Path.GetExtension(result.FileName);
        if (string.IsNullOrEmpty(extension))
            extension = ".jpg";

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, fileName);

        await using var source = await result.OpenReadAsync();
        await using var destination = File.Create(fullPath);
        await source.CopyToAsync(destination);

        return fileName;
    }

    // ==================== Пути ====================

    public string? GetFullPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var fullPath = Path.Combine(GetReceiptsFolder(), fileName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private static string GetReceiptsFolder()
    {
        return Path.Combine(FileSystem.AppDataDirectory, ReceiptsFolder);
    }

    // ==================== Удаление ====================

    public Task DeleteAsync(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Task.CompletedTask;

        try
        {
            var fullPath = Path.Combine(GetReceiptsFolder(), fileName);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReceiptService] Delete failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}