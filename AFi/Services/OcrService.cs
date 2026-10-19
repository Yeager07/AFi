#if ANDROID
using Android.Gms.Tasks;
using Android.Graphics;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
#endif

namespace AFi.Services;

public class OcrService : IOcrService
{
    public async Task<string> RecognizeTextAsync(string imagePath)
    {
#if ANDROID
        if (!File.Exists(imagePath))
            return string.Empty;

        var bitmap = await BitmapFactory.DecodeFileAsync(imagePath);
        if (bitmap is null)
            return string.Empty;

        try
        {
            var image = InputImage.FromBitmap(bitmap, 0);
            var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);

            var tcs = new TaskCompletionSource<string>();
            var listener = new TaskCompleteListener(tcs);

            recognizer.Process(image).AddOnCompleteListener(listener);

            return await tcs.Task;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OcrService] Error: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            bitmap.Recycle();
        }
#else
        await Task.CompletedTask;
        return string.Empty;
#endif
    }

#if ANDROID
    /// <summary>
    /// Вспомогательный слушатель для конвертации Android.Gms.Tasks.Task
    /// в стандартный .NET Task через TaskCompletionSource.
    /// </summary>
    private class TaskCompleteListener : Java.Lang.Object, IOnCompleteListener
    {
        private readonly TaskCompletionSource<string> _tcs;

        public TaskCompleteListener(TaskCompletionSource<string> tcs)
        {
            _tcs = tcs;
        }

        public void OnComplete(Android.Gms.Tasks.Task task)
        {
            if (task.IsSuccessful)
            {
                var resultText = task.Result as Xamarin.Google.MLKit.Vision.Text.Text;
                // В Xamarin-привязках метод называется GetText(), а не свойство Text
                _tcs.TrySetResult(resultText?.GetText() ?? string.Empty);
            }
            else
            {
                _tcs.TrySetResult(string.Empty);
            }
        }
    }
#endif
}