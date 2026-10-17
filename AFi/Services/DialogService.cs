namespace AFi.Services;

public class DialogService : IDialogService
{
    private readonly DatabaseService _db;
    private readonly IReceiptService _receipts;

    public DialogService(DatabaseService db, IReceiptService receipts)
    {
        _db = db;
        _receipts = receipts;
    }

    public async Task<bool> ConfirmAsync(string title, string message,
                                          string accept, string cancel)
    {
        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is null)
            return false;

        var modal = new Controls.ThemedConfirmPage(title, message, accept, cancel);

        await page.Navigation.PushModalAsync(modal, animated: false);

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

        var modal = new Controls.ThemedPromptPage(
            title, message, placeholder, maxLength, initialValue);

        await page.Navigation.PushModalAsync(modal, animated: false);

        return await modal.WaitForResultAsync();
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

        await page.Navigation.PushModalAsync(modal, animated: false);

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

    public async Task<bool> ShowTransactionEditAsync(int transactionId)
    {
        var page = Shell.Current?.CurrentPage
                   ?? Application.Current?.Windows.FirstOrDefault()?.Page;

        if (page is null) return false;

        var vm = new ViewModels.TransactionEditViewModel(_db, _receipts);
        vm.InitializeForEdit(transactionId);

        var modal = new Views.TransactionEditPage(vm);

        await page.Navigation.PushModalAsync(modal, animated: false);
        return await modal.WaitForResultAsync();
    }
}