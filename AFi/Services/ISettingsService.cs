namespace AFi.Services;

/// <summary>
/// Сервис пользовательских настроек приложения.
/// Хранит начальный баланс и кэш последнего рассчитанного баланса.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Начальный баланс — сумма на счёте до начала учёта.
    /// </summary>
    decimal GetInitialBalance();

    /// <summary>
    /// Установить начальный баланс.
    /// </summary>
    void SetInitialBalance(decimal value);

    /// <summary>
    /// Последний рассчитанный баланс (начальный + все операции).
    /// Используется, чтобы на старте приложения сразу показать
    /// пользователю его баланс, пока данные подгружаются из БД.
    /// </summary>
    decimal GetLastBalance();

    /// <summary>
    /// Сохранить текущий баланс в кэш.
    /// Вызывается после каждого пересчёта.
    /// </summary>
    void SetLastBalance(decimal value);
}