namespace AFi.Services;

public interface IOcrService
{
    Task<string> RecognizeTextAsync(string imagePath);
}