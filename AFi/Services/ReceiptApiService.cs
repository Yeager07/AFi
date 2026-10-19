using System.Globalization;
using System.Text.Json;

namespace AFi.Services;

/// <summary>
/// Сервис для получения детальных данных чека по QR-коду через API proverkacheka.com.
/// </summary>
public interface IReceiptApiService
{
    Task<ReceiptData?> GetReceiptDataAsync(string qrRaw);
}

public class ReceiptApiService : IReceiptApiService
{
    // ВАЖНО: Зарегистрируйся на proverkacheka.com и вставь свой токен.
    // Бесплатного лимита (15-20 запросов в сутки) хватит для личного использования.
    private const string ApiUrl = "https://proverkacheka.com/api/v1/check/get";
    private const string Token = "55655.kyukLkQvEy2tIPILC"; // ЗАМЕНИ НА СВОЙ ТОКЕН!

    public async Task<ReceiptData?> GetReceiptDataAsync(string qrRaw)
    {
        try
        {
            using var httpClient = new HttpClient();
            using var content = new MultipartFormDataContent();

            content.Add(new StringContent(Token), "token");
            content.Add(new StringContent(qrRaw), "qrraw");

            var response = await httpClient.PostAsync(ApiUrl, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Ошибка сервера {response.StatusCode}.\nОтвет: {json}");
            }

            // Внешний JSON
            var root = JsonDocument.Parse(json).RootElement;
            
            // Проверяем код ответа (может быть строкой или числом)
            var codeStr = GetSafeString(root, "code");
            if (codeStr != "1" && codeStr != "0") // 0 или 1 обычно означают успех
            {
                var desc = GetSafeString(root, "desc");
                throw new Exception($"Ошибка API (код {codeStr}): {desc}");
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            {
                throw new Exception($"В ответе нет поля 'data'.\nОтвет: {json}");
            }

            if (!data.TryGetProperty("json", out var jsonData) || jsonData.ValueKind == JsonValueKind.Null)
            {
                throw new Exception($"В ответе нет поля 'data.json'.\nОтвет: {json}");
            }

            // data.json может быть как строкой, так и объектом
            JsonElement innerRoot;
            if (jsonData.ValueKind == JsonValueKind.String)
            {
                var innerJsonString = jsonData.GetString();
                if (string.IsNullOrEmpty(innerJsonString)) throw new Exception("Поле data.json пустое.");
                innerRoot = JsonDocument.Parse(innerJsonString).RootElement;
            }
            else if (jsonData.ValueKind == JsonValueKind.Object)
            {
                innerRoot = jsonData;
            }
            else
            {
                throw new Exception($"Неизвестный тип data.json: {jsonData.ValueKind}");
            }

            var receipt = new ReceiptData
            {
                User = GetSafeString(innerRoot, "user"),
                RetailPlace = GetSafeString(innerRoot, "retailPlace"),
                DateTimeString = GetSafeString(innerRoot, "dateTime"),
                TotalSum = GetSafeDecimal(innerRoot, "totalSum"),
                Items = new List<ReceiptItem>()
            };

            if (innerRoot.TryGetProperty("items", out var itemsArray) &&
                itemsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var itemElement in itemsArray.EnumerateArray())
                {
                    receipt.Items.Add(new ReceiptItem
                    {
                        Name = GetSafeString(itemElement, "name"),
                        Price = GetSafeDecimal(itemElement, "price"),
                        Quantity = GetSafeDecimal(itemElement, "quantity"),
                        Sum = GetSafeDecimal(itemElement, "sum")
                    });
                }
            }

            return receipt;
        }
        catch (Exception ex)
        {
            // Пробрасываем ошибку дальше, чтобы ViewModel её показал
            throw new Exception($"Ошибка API: {ex.Message}");
        }
    }

    // ==================== Вспомогательные методы ====================

    /// <summary>
    /// Безопасно извлекает строку из JSON, даже если значение пришло числом.
    /// </summary>
    private static string GetSafeString(JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? defaultValue;
            else
                return prop.ToString(); // Для чисел вернёт их строковое представление
        }
        return defaultValue;
    }

    /// <summary>
    /// Безопасно извлекает число из JSON, даже если значение пришло строкой.
    /// </summary>
    private static decimal GetSafeDecimal(JsonElement element, string propertyName, decimal defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            var rawString = prop.ValueKind == JsonValueKind.String 
                ? (prop.GetString() ?? "0") 
                : prop.ToString();

            // На случай, если пришла строка с запятой вместо точки
            var normalized = rawString.Replace(',', '.');

            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
        }
        return defaultValue;
    }
}

// ==================== Модели для десериализации ====================

public class ReceiptData
{
    public string? User { get; set; }
    public string? RetailPlace { get; set; }
    public string? DateTimeString { get; set; }

    public DateTime? DateTime => System.DateTime.TryParse(DateTimeString, out var dt) ? dt : null;

    public decimal TotalSum { get; set; }
    public List<ReceiptItem> Items { get; set; } = new();
}

public class ReceiptItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Sum { get; set; }
}