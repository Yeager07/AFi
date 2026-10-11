using AFi.Models;

namespace AFi.Services;

/// <summary>
/// Сервис расчёта прогноза бюджета на следующий месяц.
///
/// Алгоритм: гибрид частотного фильтра и медианы.
/// 1. Берём 3 последних полных месяца (текущий неполный не учитываем).
/// 2. Для каждой категории считаем, в скольких месяцах она встречалась.
/// 3. Медиана для 3 месяцев, среднее для 2, отбрасываем для 1.
/// 4. Итог — сумма по всем «зачётным» категориям.
/// </summary>
public class ForecastService
{
    private readonly DatabaseService _db;

    public ForecastService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<ForecastResult> CalculateAsync()
    {
        // ==================== 1. Определяем период ====================

        var today = DateTime.Today;
        var firstOfCurrentMonth = new DateTime(today.Year, today.Month, 1);

        // Три полных месяца — от начала прошлого и до конца предыдущего
        // (не включая текущий неполный)
        var periodFrom = firstOfCurrentMonth.AddMonths(-3);
        var periodTo = firstOfCurrentMonth.AddDays(-1);

        // ==================== 2. Тянем транзакции ====================

        var transactions = await _db.GetTransactionsAsync(periodFrom, periodTo);

        var expenses = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .ToList();

        var result = new ForecastResult
        {
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
        };

        if (expenses.Count == 0)
            return result;

        // ==================== 3. Группируем по категориям ====================

        var grouped = expenses
            .GroupBy(t => t.CategoryName)
            .ToList();

        foreach (var group in grouped)
        {
            var name = group.Key;
            var icon = group.First().CategoryIcon;

            // Сумма по каждому из 3 месяцев
            var monthlySums = new List<decimal>();
            var monthsPresent = 0;

            for (int i = 0; i < 3; i++)
            {
                var monthStart = periodFrom.AddMonths(i);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var monthSum = group
                    .Where(t => t.Date.Date >= monthStart.Date
                             && t.Date.Date <= monthEnd.Date)
                    .Sum(t => t.Amount);

                if (monthSum > 0)
                {
                    monthlySums.Add(monthSum);
                    monthsPresent++;
                }
            }

            // ==================== 4. Применяем правило ====================

            decimal predicted = 0m;
            bool included = false;

            if (monthsPresent == 3)
            {
                // Медиана трёх значений: после сортировки — середина
                var sorted = monthlySums.OrderBy(x => x).ToList();
                predicted = sorted[1];
                included = true;
            }
            else if (monthsPresent == 2)
            {
                // Среднее двух (медиана из двух = среднее)
                predicted = monthlySums.Average();
                included = true;
            }
            // 1 месяц — не включаем

            result.Items.Add(new ForecastItem
            {
                CategoryName = name,
                Icon = icon,
                MonthsPresent = monthsPresent,
                PredictedSum = predicted,
                IsIncluded = included,
            });
        }

        // ==================== 5. Сортировка и итог ====================

        result.Items = result.Items
            .OrderByDescending(i => i.IsIncluded)
            .ThenByDescending(i => i.PredictedSum)
            .ThenBy(i => i.CategoryName)
            .ToList();

        result.TotalPredicted = result.Items
            .Where(i => i.IsIncluded)
            .Sum(i => i.PredictedSum);

        return result;
    }
}