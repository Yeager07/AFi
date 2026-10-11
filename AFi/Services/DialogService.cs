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

        var modal = new Controls.ThemedConfirmPage(title, message, accept, cancel);

        await page.Navigation.PushModalAsync(modal);

        return await modal.WaitForResultAsync();
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

    public async Task<string?> ShowActionSheetAsync(
        string title,
        string cancel,
        params string[] actions)
    {
        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is null)
            return null;

        var modal = new Controls.ThemedActionSheetPage(title, cancel, actions);

        await page.Navigation.PushModalAsync(modal);

        return await modal.WaitForResultAsync();
    }

    public async Task ShowReceiptAsync(ImageSource source)
    {
        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is null)
            return;

        var modal = new Controls.ReceiptViewerPage(source);

        await page.Navigation.PushModalAsync(modal);
    }
}