namespace AFi.Services;

/// <summary>
/// Сообщение о том, что данные изменились (создание/редактирование/удаление
/// операции или напоминания). Отправляется через WeakReferenceMessenger.
/// VM, которые показывают данные, подписываются и ставят dirty-флаг,
/// чтобы OnAppearing решил, нужно ли перезагружать.
/// </summary>
public sealed class DataChangedMessage
{
    /// <summary>
    /// VM, которая отправила сообщение. Получатель может сравнить с собой
    /// и не реагировать на собственные изменения (данные уже свежие).
    /// </summary>
    public object? Sender { get; }

    public DataChangedMessage(object? sender = null)
    {
        Sender = sender;
    }
}