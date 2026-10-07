namespace AFi.Services;

public class DialogService : IDialogService
{
    public async Task<bool> ConfirmAsync(string title, string message,
                                          string accept, string cancel)
    {
        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is null)
            return false;

        return await page.DisplayAlertAsync(title, message, accept, cancel);
    }

    public async Task<string?> PromptAsync(
    string title,
    string message,
    string placeholder = "",
    int maxLength = -1,
    string initialValue = "")
    {
        var page = Shell.Current?.CurrentPage
                ?? Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is null)
            return null;

        return await page.DisplayPromptAsync(
            title,
            message,
            accept: "Сохранить",
            cancel: "Отмена",
            placeholder: placeholder,
            maxLength: maxLength,
            keyboard: Keyboard.Text,
            initialValue: initialValue);
    }
}