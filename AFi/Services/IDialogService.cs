namespace AFi.Services;

/// <summary>
/// Абстракция над UI-диалогами. Позволяет ViewModel не зависеть
/// напрямую от Page/Shell и оставаться тестируемой.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Показать диалог подтверждения. Возвращает true, если пользователь
    /// нажал «подтвердить».
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>
    /// Показать диалог ввода строки. Возвращает введённый текст
    /// или null, если пользователь нажал «Отмена» / закрыл диалог.
    /// </summary>
    Task<string?> PromptAsync(
        string title,
        string message,
        string placeholder = "",
        int maxLength = -1,
        string initialValue = "");
}