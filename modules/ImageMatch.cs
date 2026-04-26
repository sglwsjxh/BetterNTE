using OpenCvSharp;

class ImageMatch {
    public static (int X, int Y)? FindImageCenter(Mat bitmap, string imagePath, double threshold = 0.9) {
        if (!File.Exists(imagePath))
            return null;
        if (bitmap.Empty())
            return null;

        using var template = Cv2.ImRead(imagePath, ImreadModes.Color);
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
}