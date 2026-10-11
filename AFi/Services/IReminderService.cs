using AFi.Models;

namespace AFi.Services;

/// <summary>
/// Сервис локальных напоминаний о регулярных платежах.
/// Планирует уведомления, отменяет их и перепланирует при старте приложения.
/// </summary>
public interface IReminderService
{
    /// <summary>
    /// Запросить разрешение на показ уведомлений.
    /// Возвращает true, если разрешение получено.
    /// </summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Запланировать напоминание для платежа. Если для этого платежа
    /// уже есть запланированное уведомление — оно заменяется.
    /// </summary>
    Task ScheduleAsync(RecurringPayment payment);

    /// <summary>
    /// Отменить напоминание для платежа.
    /// </summary>
    Task CancelAsync(RecurringPayment payment);

    /// <summary>
    /// Перепланировать все напоминания. Вызывается при старте приложения.
    /// </summary>
    Task RescheduleAllAsync(IEnumerable<RecurringPayment> payments);
}