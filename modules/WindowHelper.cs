using System.Diagnostics;
using System.Runtime.InteropServices;

static class WindowHelper {
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static IntPtr FindWindowByProcessName(string processName) {
        var procs = Process.GetProcessesByName(processName);
        try {
            foreach (var p in procs) {
                var hwnd = p.MainWindowHandle;
                if (hwnd != IntPtr.Zero) {
                    if (GetWindowThreadProcessId(hwnd, out _) > 0)
                        return hwnd;
                }
            }
            return IntPtr.Zero;
        } finally {
            foreach (var p in procs) p.Dispose();
        }
    }

    public static IntPtr WaitForWindow(string processName, int timeoutMs, int pollIntervalMs = 200) {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs) {
            var hwnd = FindWindowByProcessName(processName);
            if (hwnd != IntPtr.Zero)
                return hwnd;
            Thread.Sleep(pollIntervalMs);
        }
        return IntPtr.Zero;
    }

    public static bool InitScaleFromWindow(IntPtr hWnd) {
        var size = Capture.GetWindowClientSize(hWnd);
        if (size == null) {
            AppLog.Write("WindowHelper: window not available, using screen-based scale");
            ImageMatch.InitializeScreenScale();
            return false;
        }
        ImageMatch.InitializeWithWindowSize(size.Value.Width, size.Value.Height);
        return true;
    }
}
