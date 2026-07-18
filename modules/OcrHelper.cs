using OpenCvSharp;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

static class OcrHelper {
    static OcrEngine? _engine;

    public static void EnsureInitialized() {
        if (_engine != null) return;
        _engine = OcrEngine.TryCreateFromLanguage(new Language("zh-CN"))
                  ?? OcrEngine.TryCreateFromUserProfileLanguages();
        if (_engine == null)
            AppLog.Write("OcrHelper: failed to create OCR engine");
    }

    public static (int X, int Y)? FindText(Mat grayFrame, string searchText) {
        if (_engine == null) return null;

        using var sb = MatToSoftwareBitmap(grayFrame);
        if (sb == null) return null;

        var result = _engine.RecognizeAsync(sb).GetAwaiter().GetResult();
        if (result?.Lines == null) {
            AppLog.Write("OcrHelper: no text lines found in frame");
            return null;
        }

        var allWords = result.Lines.SelectMany(l => l.Words).ToList();
        var fullText = string.Concat(allWords.Select(w => w.Text.Replace(" ", "")));
        AppLog.Write($"OcrHelper: recognized text: \"{fullText}\"");

        var searchNorm = searchText.Replace(" ", "");
        int idx = fullText.IndexOf(searchNorm);
        if (idx < 0) return null;

        int charPos = 0;
        foreach (var word in allWords) {
            int wordLen = word.Text.Replace(" ", "").Length;
            if (charPos + wordLen > idx) {
                var r = word.BoundingRect;
                return ((int)(r.X + r.Width / 2), (int)(r.Y + r.Height / 2));
            }
            charPos += wordLen;
        }
        return null;
    }

    static SoftwareBitmap? MatToSoftwareBitmap(Mat gray) {
        if (gray.Empty() || gray.Type() != MatType.CV_8UC1) return null;

        // Encode Mat to PNG bytes, then decode via WinRT BitmapDecoder
        // This avoids the IMemoryBufferByteAccess COM interop issue in .NET 10
        Cv2.ImEncode(".png", gray, out byte[] pngBytes);
        using var memStream = new MemoryStream(pngBytes);
        using var rasStream = memStream.AsRandomAccessStream();
        var decoder = BitmapDecoder.CreateAsync(rasStream).GetAwaiter().GetResult();
        return decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult();
    }
}
