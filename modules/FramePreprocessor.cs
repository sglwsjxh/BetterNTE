using OpenCvSharp;

static class FramePreprocessor {
    public static void CaptureAndPreprocess(Mat target, IntPtr hwnd) {
        bool captured;
        if (hwnd != IntPtr.Zero)
            captured = Capture.CaptureWindow(target, hwnd);
        else {
            target.Create(0, 0, MatType.CV_8UC3);
            Capture.CaptureScreen(target);
            captured = !target.Empty();
        }
        if (!captured) return;

        int cw = 0, ch = 0;
        if (hwnd != IntPtr.Zero) {
            var s = Capture.GetWindowClientSize(hwnd);
            if (s != null) { cw = s.Value.Width; ch = s.Value.Height; }
        } else {
            var ss = Capture.GetScreenSize();
            cw = ss.Width; ch = ss.Height;
        }
        if (cw > 0) {
            ImageMatch.SetCaptureScale(cw, ch);
            AutoClick.CaptureScaleX = ImageMatch.ScaleX;
            AutoClick.CaptureScaleY = ImageMatch.ScaleY;
        }

        if (target.Width != 1920 || target.Height != 1080) {
            using var rs = new Mat();
            Cv2.Resize(target, rs, new OpenCvSharp.Size(1920, 1080), 0, 0, InterpolationFlags.Linear);
            rs.CopyTo(target);
        }

        using var g = new Mat();
        Cv2.CvtColor(target, g, ColorConversionCodes.BGR2GRAY);
        using var b = new Mat();
        Cv2.GaussianBlur(g, b, new OpenCvSharp.Size(3, 3), 0.8);
        b.CopyTo(target);
    }
}
