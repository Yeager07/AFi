namespace AFi.Services;

/// <summary>
/// Сервис резервного копирования БД.
/// Экспорт — копирование файла во временную папку + системный Share.
/// Импорт — FilePicker + замена файла + переинициализация БД.
/// </summary>
public interface IDataBackupService
{
    /// <summary>
    /// Создать резервную копию БД и открыть диалог «Поделиться».
    /// Возвращает true, если Share был открыт (пользователь мог отменить отправку).
    /// </summary>
    Task<bool> ExportAsync();

    /// <summary>
    /// Выбрать файл `.db3` и заменить им текущую БД.
    /// Возвращает true, если данные восстановлены.
    /// </summary>
    Task<bool> ImportAsync();
}