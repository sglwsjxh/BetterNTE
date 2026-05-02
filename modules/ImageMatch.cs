using OpenCvSharp;

class ImageMatch {
    static readonly object _sync = new();
    static readonly Dictionary<string, Mat> _templateCache = new();

    public static (int X, int Y)? FindImageCenter(Mat bitmap, string imagePath, double threshold = 0.9, double scale = 1.0) {
        var template = GetTemplate(imagePath, scale);
        if (template == null)
            return null;

        return FindImageCenter(bitmap, template, threshold);
    }

    public static (int X, int Y)? FindImageCenter(Mat bitmap, Mat template, double threshold = 0.9) {
        if (bitmap.Empty())
            return null;
        if (template.Empty())
            return null;

        if (bitmap.Width < template.Width || bitmap.Height < template.Height)
            return null;

        using var result = new Mat();
        Cv2.MatchTemplate(bitmap, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);

        if (maxVal < threshold)
            return null;

        return (maxLoc.X + template.Width / 2, maxLoc.Y + template.Height / 2);
    }

    public static Mat? GetTemplate(string imagePath, double scale = 1.0) {
        string cacheKey = $"{imagePath}_{scale:F4}";
        lock (_sync) {
            if (_templateCache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!File.Exists(imagePath))
                return null;

            var template = Cv2.ImRead(imagePath, ImreadModes.Color);
            if (template.Empty()) {
                template.Dispose();
                return null;
            }

            if (Math.Abs(scale - 1.0) > 0.001) {
                var resized = new Mat();
                Cv2.Resize(template, resized, new OpenCvSharp.Size(), scale, scale, InterpolationFlags.Area);
                template.Dispose();
                template = resized;
            }

            _templateCache[cacheKey] = template;
            return template;
        }
    }

    public static void ClearTemplateCache() {
        lock (_sync) {
            foreach (var item in _templateCache.Values)
                item.Dispose();

            _templateCache.Clear();
        }
    }
}