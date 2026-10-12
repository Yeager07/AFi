using System.Text.Json;
using AFi.Models;

namespace AFi.Services;

public class FormSnapshotService : IFormSnapshotService
{
    private const string Key = "form_snapshot";

    public void Save(FormSnapshot snapshot)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot);
            Preferences.Set(Key, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FormSnapshot] Save failed: {ex.Message}");
        }
    }

    public FormSnapshot? Load()
    {
        try
        {
            var json = Preferences.Get(Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            return JsonSerializer.Deserialize<FormSnapshot>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FormSnapshot] Load failed: {ex.Message}");
            return null;
        }
    }

    public void Clear()
    {
        try { Preferences.Remove(Key); }
        catch { }
    }
}