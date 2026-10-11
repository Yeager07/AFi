namespace AFi.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    Task<string?> PromptAsync(
        string title,
        string message,
        string placeholder = "",
        int maxLength = -1,
        string initialValue = "");

    Task<string?> ShowActionSheetAsync(string title, string cancel, params string[] actions);

    /// <summary>
    /// Открыть модальное окно с изображением на весь экран.
    /// </summary>
    Task ShowReceiptAsync(ImageSource source);
}