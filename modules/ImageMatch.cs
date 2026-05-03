using OpenCvSharp;

class ImageMatch {
    const double BASE_SCREEN_HEIGHT = 1080.0;
    static readonly object _sync = new();
    static readonly Dictionary<string, Mat> _templateCache = new();

    public static double ScreenScale { get; private set; } = 1.0;

    public static void InitializeScreenScale() {
        var screenSize = Capture.GetScreenSize();
        ScreenScale = screenSize.Height / BASE_SCREEN_HEIGHT;
        AppLog.Write($"Screen scale initialized. Screen={screenSize.Width}x{screenSize.Height}, Scale={ScreenScale:F4}");
    }

    public static (int X, int Y)? FindImageCenter(Mat bitmap, string imagePath, double threshold = 0.9, double? scale = null) {
        var template = GetTemplate(imagePath, scale);
        if (template == null)
            return null;

        return FindImageCenter(bitmap, template, threshold);
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

    public static Mat? GetTemplate(string imagePath, double? scale = null) {
        double actualScale = scale ?? ScreenScale;
        string cacheKey = $"{imagePath}_{actualScale:F4}";
        lock (_sync) {
            if (_templateCache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!File.Exists(imagePath))
            {
                AppLog.Write($"Template missing. Path={imagePath}, Scale={actualScale:F4}");
                return null;
            }

            var template = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (template.Empty()) {
                AppLog.Write($"Template failed to load. Path={imagePath}, Scale={actualScale:F4}");
                template.Dispose();
                return null;
            }

            var originalWidth = template.Width;
            var originalHeight = template.Height;

            if (Math.Abs(actualScale - 1.0) > 0.001) {
                var resized = new Mat();
                Cv2.Resize(template, resized, new OpenCvSharp.Size(), actualScale, actualScale, InterpolationFlags.Area);
                template.Dispose();
                template = resized;
            }

            _templateCache[cacheKey] = template;
            AppLog.Write($"Template loaded. Path={imagePath}, Scale={actualScale:F4}, Original={originalWidth}x{originalHeight}, Actual={template.Width}x{template.Height}");
            return template;
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