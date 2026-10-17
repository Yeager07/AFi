using CommunityToolkit.Mvvm.Messaging;

namespace AFi.Services;

public class DataBackupService : IDataBackupService
{
    private const string DatabaseFileName = "afi.db3";

    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;
    private readonly ISettingsService _settings;

    public DataBackupService(DatabaseService db,
                             IDialogService dialogs,
                             ISettingsService settings)
    {
        _db = db;
        _dialogs = dialogs;
        _settings = settings;
    }

    private static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    // ==================== Экспорт ====================

    public async Task<bool> ExportAsync()
    {
        try
        {
            var source = DatabasePath;
            if (!File.Exists(source))
                return false;

            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            var tempDir = Path.Combine(FileSystem.CacheDirectory, "backup");
            Directory.CreateDirectory(tempDir);

            var backupPath = Path.Combine(tempDir, $"afi_backup_{stamp}.db3");

            File.Copy(source, backupPath, overwrite: true);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Экспорт данных AFi",
                File = new ShareFile(backupPath),
            });

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataBackupService] Export failed: {ex.Message}");
            return false;
        }
    }

    // ==================== Импорт ====================

    public async Task<bool> ImportAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Выберите файл резервной копии",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android, new[] { "application/octet-stream", "*/*" } },
                    { DevicePlatform.iOS,     new[] { "public.data", "public.database" } },
                    { DevicePlatform.WinUI,   new[] { ".db3" } },
                }),
            });

            if (result is null)
                return false;

            // Подтверждение: текущие данные будут заменены
            var confirmed = await _dialogs.ConfirmAsync(
                "Восстановление данных",
                "Текущие данные будут заменены содержимым резервной копии.\n\n" +
                "Это действие нельзя отменить. Продолжить?",
                "Восстановить",
                "Отмена");

            if (!confirmed)
                return false;

            // 1. Закрыть текущую БД (снять блокировку файла)
            await _db.CloseAsync();

            // 2. Скопировать выбранный файл поверх БД
            var destination = DatabasePath;
            File.Copy(result.FullPath, destination, overwrite: true);

            // 3. Сбросить кэш баланса — он относится к старым данным
            _settings.ClearLastBalance();

            // 4. Переоткрыть БД (сбросит _initialized, _categoriesCache)
            await _db.InitializeAsync();

            // 5. Оповестить все VM — Sender = null, чтобы даже отправитель
            //    перезагрузился при следующем визите
            WeakReferenceMessenger.Default.Send(new DataChangedMessage(null));

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DataBackupService] Import failed: {ex.Message}");
            return false;
        }
    }
}