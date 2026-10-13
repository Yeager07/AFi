namespace AFi.Services;

/// <summary>
/// Сервис пользовательских настроек приложения.
/// Сейчас хранит только начальный баланс, но позже сюда можно
/// добавить другие опции (валюта, формат даты, и т.д.).
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
}