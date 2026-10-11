using AFi.Models;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Plugin.LocalNotification.Core.Models.AndroidOption;

#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
#endif

namespace AFi.Services;

public class ReminderService : IReminderService
{
    // ==================== Разрешения ====================

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            var granted = await LocalNotificationCenter.Current.RequestNotificationPermission();

    #if ANDROID
            Android.Util.Log.Info("AFi-Reminder",
                $"RequestPermission: granted={granted}, " +
                $"exactAlarmAllowed={CanScheduleExactAlarms()}");
    #endif

            if (!CanScheduleExactAlarms())
            {
                RequestExactAlarmPermission();
            }

            return granted;
        }
        catch (Exception ex)
        {
    #if ANDROID
            Android.Util.Log.Error("AFi-Reminder", $"RequestPermission FAILED: {ex}");
    #endif
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
        {
    #if ANDROID
            Android.Util.Log.Warn("AFi-Reminder", $"Schedule: notifyTime is null for '{payment.Name}'");
    #endif
            return;
        }

        var scheduleMode = GetBestScheduleMode();

    #if ANDROID
        Android.Util.Log.Info("AFi-Reminder",
            $"Schedule: id={payment.Id}, name='{payment.Name}', " +
            $"when={notifyTime.Value:yyyy-MM-dd HH:mm}, mode={scheduleMode}, " +
            $"exactAlarmAllowed={CanScheduleExactAlarms()}");
    #endif

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
    #if ANDROID
            Android.Util.Log.Info("AFi-Reminder", "Show completed OK");
    #endif
        }
        catch (Exception ex)
        {
    #if ANDROID
            Android.Util.Log.Error("AFi-Reminder", $"Show FAILED: {ex}");
    #endif
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

        foreach (var p in list)
        {
            try { LocalNotificationCenter.Current.Cancel(p.Id); } catch { }
        }

        foreach (var p in list)
        {
            if (p.IsEnabled)
                await ScheduleAsync(p);
        }
    }

    // ==================== Расчёт даты ====================

    private static DateTimeOffset? CalculateNextNotificationTime(RecurringPayment payment)
    {
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

    // ==================== Exact alarm (Android) ====================

    /// <summary>
    /// Возвращает лучший доступный режим планирования.
    /// Если разрешение на точные будильники есть — используем ExactAllowWhileIdle,
    /// иначе падаем на Inexact (с задержкой до 15 минут).
    /// </summary>
    private static AndroidScheduleMode GetBestScheduleMode()
    {
#if ANDROID
        try
        {
            // До Android 12 (API 31) — точные будильники разрешены всегда
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
    /// <summary>
    /// Проверяет, может ли приложение планировать точные будильники.
    /// </summary>
    private static bool CanScheduleExactAlarms()
    {
        try
        {
            // До API 31 разрешение не требуется — точные alarm-ы всегда доступны
            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
                return true;

            var alarmManager = (AlarmManager?)Android.App.Application.Context
                .GetSystemService(Context.AlarmService);

            return alarmManager?.CanScheduleExactAlarms() ?? false;
        }
        catch { return false; }
    }

    /// <summary>
    /// Открывает системные настройки «Будильники и напоминания»,
    /// чтобы пользователь дал приложению разрешение на точные alarm-ы.
    /// </summary>
    private static void RequestExactAlarmPermission()
    {
        // Настройки точных будильников появились только в Android 12 (API 31)
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
            // Если по какой-то причине не открылось — тихо игнорируем
        }
    }
#endif
}