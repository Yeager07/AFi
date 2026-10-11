namespace AFi.Services;

/// <summary>
/// Сервис работы с фото чеков: выбор из галереи, съёмка с камеры,
/// сохранение в локальное хранилище, удаление.
/// </summary>
public interface IReceiptService
{
    /// <summary>
    /// Выбрать фото из галереи и сохранить в локальное хранилище.
    /// Возвращает имя сохранённого файла или null, если пользователь отменил.
    /// </summary>
    Task<string?> PickFromGalleryAsync();

    /// <summary>
    /// Сделать фото через камеру и сохранить в локальное хранилище.
    /// Возвращает имя сохранённого файла или null, если пользователь отменил.
    /// </summary>
    Task<string?> CaptureFromCameraAsync();

    /// <summary>
    /// Получить полный путь к файлу по его имени.
    /// </summary>
    string? GetFullPath(string? fileName);

    /// <summary>
    /// Удалить файл по имени. Тихо игнорирует, если файла нет.
    /// </summary>
    Task DeleteAsync(string? fileName);
}