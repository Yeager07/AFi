using System.Globalization;

namespace AFi.Services;

public class SettingsService : ISettingsService
{
    private const string InitialBalanceKey = "initial_balance";
    private const string LastBalanceKey = "last_balance";

    public decimal GetInitialBalance()
    {
        try
        {
            var raw = Preferences.Get(InitialBalanceKey, "0");
            return decimal.TryParse(raw, NumberStyles.Number,
                                    CultureInfo.InvariantCulture, out var value)
                ? value
                : 0m;
        }
        catch
        {
            return 0m;
        }
    }

    public void SetInitialBalance(decimal value)
    {
        try
        {
            Preferences.Set(InitialBalanceKey,
                value.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Save failed: {ex.Message}");
        }
    }

    public decimal GetLastBalance()
    {
        try
        {
            var raw = Preferences.Get(LastBalanceKey, "0");
            return decimal.TryParse(raw, NumberStyles.Number,
                                    CultureInfo.InvariantCulture, out var value)
                ? value
                : 0m;
        }
        catch
        {
            return 0m;
        }
    }

    public void SetLastBalance(decimal value)
    {
        try
        {
            Preferences.Set(LastBalanceKey,
                value.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Save balance failed: {ex.Message}");
        }
    }

    public void ClearLastBalance()
    {
        try { Preferences.Remove(LastBalanceKey); }
        catch { }
    }
}