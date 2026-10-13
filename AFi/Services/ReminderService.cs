using AFi.Models;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;
using Plugin.LocalNotification.EventArgs;

#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
#endif

namespace AFi.Services;

public class ReminderService : IReminderService
{
    // ==================== Тестовый режим ====================
    // true — напоминание срабатывает через 2 минуты от текущего момента
    //        (для проверки работы уведомлений)
    // false — используется реальное время payment.ReminderTime
    // ПЕРЕД КОММИТОМ ПОСТАВЬ false!
    private static bool UseTestMode = true;

    private const int TestDelayMinutes = 2;

    public ReminderService()
    {
        // Подписка на тап по уведомлению — открываем страницу «Напоминания»
        LocalNotificationCenter.Current.NotificationActionTapped += OnNotificationActionTapped;
    }

    // ==================== Разрешения ====================

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            var granted = await LocalNotificationCenter.Current.RequestNotificationPermission();

#if ANDROID
            if (!CanScheduleExactAlarms())
            {
                RequestExactAlarmPermission();
            }
#endif

            return granted;
        }
        catch
        {
            return false;
        }
    }

    // ==================== Планирование ====================

    public async Task ScheduleAsync(RecurringPayment payment)
    {
        if (!payment.IsEnabled)
        {
            await CancelAsync(payment);
            return;
        }

        var notifyTime = CalculateNextNotificationTime(payment);
        if (notifyTime is null)
            return;

        var scheduleMode = GetBestScheduleMode();

        var request = new NotificationRequest
        {
            NotificationId = payment.Id,
            Title = $"🔔 {payment.Name}",
            Description = BuildDescription(payment),
            ReturningData = $"paymentId={payment.Id}",
            Schedule = new NotificationRequestSchedule
            {
                NotifyTime = notifyTime.Value,
                Android = new AndroidScheduleOptions
                {
                    ScheduleMode = scheduleMode
                }
            },
        };

        try
        {
            await LocalNotificationCenter.Current.Show(request);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReminderService] Show failed: {ex.Message}");
        }
    }

    public Task CancelAsync(RecurringPayment payment)
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(payment.Id);
        }
        catch { }
        return Task.CompletedTask;
    }

    public async Task RescheduleAllAsync(IEnumerable<RecurringPayment> payments)
    {
        var list = payments.ToList();

        // Получаем список уже запланированных уведомлений,
        // чтобы не пересоздавать их. Иначе Cancel + Show каждые несколько
        // минут стирает только что показанное уведомление.
        HashSet<int> alreadyScheduled;
        try
        {
            var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
            alreadyScheduled = pending.Select(n => n.NotificationId).ToHashSet();
        }
        catch
        {
            alreadyScheduled = new HashSet<int>();
        }

        // Планируем только те напоминания, у которых ещё нет уведомления.
        // Существующие не трогаем — они уже в очереди AlarmManager.
        foreach (var p in list)
        {
            if (p.IsEnabled && !alreadyScheduled.Contains(p.Id))
            {
                await ScheduleAsync(p);
            }
        }
    }

    // ==================== Расчёт даты ====================

    private static DateTimeOffset? CalculateNextNotificationTime(RecurringPayment payment)
    {
        // ==================== ТЕСТОВЫЙ РЕЖИМ ====================
        if (UseTestMode)
        {
            var testTime = DateTime.Now.AddMinutes(TestDelayMinutes);
            System.Diagnostics.Debug.WriteLine(
                $"[ReminderService] TEST MODE: notification scheduled for {testTime:HH:mm:ss}");
            return new DateTimeOffset(testTime);
        }
        // ==================== КОНЕЦ ТЕСТОВОГО РЕЖИМА ====================

        var today = DateTime.Today;
        var timeOfDay = payment.ReminderTime;

        for (int monthOffset = 0; monthOffset < 3; monthOffset++)
        {
            var baseMonth = new DateTime(today.Year, today.Month, 1).AddMonths(monthOffset);
            var daysInMonth = DateTime.DaysInMonth(baseMonth.Year, baseMonth.Month);
            var safeDay = Math.Min(Math.Max(payment.DayOfMonth, 1), daysInMonth);
            var paymentDate = new DateTime(baseMonth.Year, baseMonth.Month, safeDay);

            var notifyDate = paymentDate.AddDays(-payment.ReminderDaysBefore);
            var notifyDateTime = notifyDate.Date + timeOfDay;

            if (notifyDateTime > DateTime.Now)
                return new DateTimeOffset(notifyDateTime);
        }

        return null;
    }

    // ==================== Текст уведомления ====================

    private static string BuildDescription(RecurringPayment payment)
    {
        var daysText = payment.ReminderDaysBefore switch
        {
            0 => "Сегодня",
            1 => "Завтра",
            2 => "Через 2 дня",
            _ => $"Через {payment.ReminderDaysBefore} дней",
        };

        var typeSign = payment.Type == TransactionType.Income ? "+" : "−";
        var amountText = $"{typeSign}{payment.Amount:N0} ₽";

        return $"{daysText}: {amountText} · {payment.DayOfMonth}-го числа";
    }

    // ==================== Точные будильники ====================

    private static AndroidScheduleMode GetBestScheduleMode()
    {
#if ANDROID
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
                return AndroidScheduleMode.ExactAllowWhileIdle;

            return CanScheduleExactAlarms()
                ? AndroidScheduleMode.ExactAllowWhileIdle
                : AndroidScheduleMode.InexactAllowWhileIdle;
        }
        catch
        {
            return AndroidScheduleMode.InexactAllowWhileIdle;
        }
#else
        return AndroidScheduleMode.InexactAllowWhileIdle;
#endif
    }

#if ANDROID
    private static bool CanScheduleExactAlarms()
    {
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
                return true;

            var alarmManager = (AlarmManager?)Android.App.Application.Context
                .GetSystemService(Context.AlarmService);

            return alarmManager?.CanScheduleExactAlarms() ?? false;
        }
        catch { return false; }
    }

    private static void RequestExactAlarmPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            return;

        try
        {
            var context = Android.App.Application.Context;
            var intent = new Intent(Settings.ActionRequestScheduleExactAlarm);
            intent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch
        {
            // Тихо
        }
    }
#endif

    // ==================== Обработка тапа по уведомлению ====================

    private void OnNotificationActionTapped(NotificationActionEventArgs e)
    {
        if (e.Request is null) return;

        var notificationId = e.Request.NotificationId;
        System.Diagnostics.Debug.WriteLine($"[Reminder] Notification tapped: id={notificationId}");

        _ = NavigateToRecurringPageAsync();
    }

    /// <summary>
    /// Переходит на страницу «Напоминания». Использует polling, чтобы
    /// дождаться инициализации Shell — если процесс приложения был убит,
    /// тап по уведомлению перезапускает AFi, и Shell может быть ещё не готов.
    /// </summary>
    private static async Task NavigateToRecurringPageAsync()
    {
        // Ждём до 4 секунд, пока Shell появится
        for (int i = 0; i < 20; i++)
        {
            if (Shell.Current is not null) break;
            await Task.Delay(200);
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (Shell.Current is not null)
                    await Shell.Current.GoToAsync("//RecurringPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Reminder] Navigation failed: {ex.Message}");
            }
        });
    }
}