namespace AFi.Helpers;

/// <summary>
/// Утилита для управления экранной клавиатурой.
/// На Android использует InputMethodManager — гарантированно скрывает.
/// На других платформах — no-op.
/// </summary>
public static class KeyboardHelper
{
    /// <summary>
    /// Скрыть экранную клавиатуру и снять фокус с активного поля.
    /// </summary>
    public static void Hide()
    {
#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity is null) return;

            var inputMethodManager = (Android.Views.InputMethods.InputMethodManager?)
                activity.GetSystemService(Android.Content.Context.InputMethodService);

            if (inputMethodManager is null) return;

            var focusedView = activity.CurrentFocus;
            if (focusedView is null) return;

            inputMethodManager.HideSoftInputFromWindow(
                focusedView.WindowToken,
                Android.Views.InputMethods.HideSoftInputFlags.None);

            focusedView.ClearFocus();
        }
        catch
        {
            // Тихо игнорируем — если что-то пошло не так, клавиатура
            // просто не скроется, но приложение не упадёт
        }
#endif
    }

    /// <summary>
    /// Снять фокус со всех полей ввода. Клавиатура скроется сама.
    /// Универсальный способ, работает на всех платформах.
    /// </summary>
    public static void UnfocusAll(VisualElement? root)
    {
        if (root is null) return;

        foreach (var child in root.GetVisualTreeDescendants())
        {
            if (child is InputView inputView && inputView.IsFocused)
                inputView.Unfocus();
        }
    }
}