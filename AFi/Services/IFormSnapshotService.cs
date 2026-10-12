using AFi.Models;

namespace AFi.Services;

/// <summary>
/// Сервис сохранения и восстановления состояния формы операции.
/// Используется при вызове камеры/галереи: если Android убьёт процесс,
/// данные формы не потеряются.
/// </summary>
public interface IFormSnapshotService
{
    /// <summary>Сохранить снапшот формы.</summary>
    void Save(FormSnapshot snapshot);

    /// <summary>Загрузить снапшот. Возвращает null, если ничего нет.</summary>
    FormSnapshot? Load();

    /// <summary>Очистить снапшот (после сохранения/отмены).</summary>
    void Clear();
}