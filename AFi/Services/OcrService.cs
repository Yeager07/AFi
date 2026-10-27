using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AFi.Services;

/// <summary>
/// Сервис для распознавания чеков через локальный Python-микросервис (PaddleOCR).
/// </summary>
public interface IOcrService
{
    Task<OcrResult?> RecognizeReceiptAsync(string imagePath);
}

public class OcrService : IOcrService
{
    // IP-адрес компьютера в локальной сети.
    // Для реального телефона — IP компьютера (как ты узнал через ipconfig).
    // Для эмулятора Android используй 10.0.2.2.
    private const string BaseUrl = "http://192.168.31.219:8000";

    public async Task<OcrResult?> RecognizeReceiptAsync(string imagePath)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(120);

            using var fileStream = File.OpenRead(imagePath);
            using var content = new MultipartFormDataContent();
            
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "file", Path.GetFileName(imagePath));

            // ✅ Логируем, что отправляем
            Console.WriteLine($"[OcrService] Отправка файла: {imagePath} (размер: {fileStream.Length} байт)");

            var response = await httpClient.PostAsync($"{BaseUrl}/api/ocr", content);
            var rawJson = await response.Content.ReadAsStringAsync();

            // ✅ Логируем, что пришло
            Console.WriteLine($"[OcrService] Ответ сервера ({response.StatusCode}): {rawJson}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Ошибка сервера {response.StatusCode}: {rawJson}");

            return System.Text.Json.JsonSerializer.Deserialize<OcrResult>(rawJson, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка OCR-сервиса: {ex.Message}");
        }
    }
}

// ==================== Модели для десериализации ====================

public class OcrResult
{
    [JsonPropertyName("items")]
    public List<OcrItem> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}

public class OcrItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }
}