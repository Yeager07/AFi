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
}