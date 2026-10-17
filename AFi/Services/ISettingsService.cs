namespace AFi.Services;

public interface ISettingsService
{
    decimal GetInitialBalance();
    void SetInitialBalance(decimal value);

    decimal GetLastBalance();
    void SetLastBalance(decimal value);

    /// <summary>
    /// Сбросить кэш последнего баланса. Вызывается после импорта БД,
    /// потому что старый кэш относится к другим данным.
    /// </summary>
    void ClearLastBalance();
}