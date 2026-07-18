using OpenCvSharp;

class ImageMatch {
    static readonly object _sync = new();
    static readonly Dictionary<string, Mat> _templateCache = new();

    public static double ScaleX { get; private set; } = 1.0;
    public static double ScaleY { get; private set; } = 1.0;

    public static void SetCaptureScale(int captureWidth, int captureHeight) {
        ScaleX = captureWidth / 1920.0;
        ScaleY = captureHeight / 1080.0;
    }

    public static (int X, int Y)? FindImageCenter(Mat bitmap, Mat template, double threshold = 0.9) {
        var match = FindBestMatch(bitmap, template);
        if (match == null)
            return null;

        if (match.Value.Score < threshold)
            return null;

        return (match.Value.X + template.Width / 2, match.Value.Y + template.Height / 2);
    }

    public static (int X, int Y, double Score)? FindBestMatch(Mat bitmap, Mat template) {
        if (bitmap.Empty())
            return null;
        if (template.Empty())
            return null;

        if (bitmap.Width < template.Width || bitmap.Height < template.Height)
            return null;

        using var result = new Mat();
        Cv2.MatchTemplate(bitmap, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);

        return (maxLoc.X, maxLoc.Y, maxVal);
    }

    public static Mat? GetTemplatePreprocessed(string imagePath) {
        string cacheKey = $"{imagePath}_pre";
        lock (_sync) {
            if (_templateCache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!File.Exists(imagePath)) {
                AppLog.Write($"Template missing. Path={imagePath}");
                return null;
            }

            var template = Cv2.ImRead(imagePath, ImreadModes.Grayscale);
            if (template.Empty()) {
                AppLog.Write($"Template failed to load. Path={imagePath}");
                template.Dispose();
                return null;
            }

            var blurred = new Mat();
            Cv2.GaussianBlur(template, blurred, new OpenCvSharp.Size(3, 3), 0.8);
            template.Dispose();
            _templateCache[cacheKey] = blurred;
            AppLog.Write($"Template preprocessed. Path={imagePath}, Size={blurred.Width}x{blurred.Height}");
            return blurred;
        }
    }

    public static void ClearTemplateCache() {
        lock (_sync) {
            foreach (var item in _templateCache.Values)
                item.Dispose();

            _templateCache.Clear();
            AppLog.Write("Template cache cleared");
        }
    }
}