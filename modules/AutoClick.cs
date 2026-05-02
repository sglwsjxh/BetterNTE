using System.Runtime.InteropServices;
using System.Threading;

static class AutoClick {
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);

    public static void Click(int x, int y, int holdMilliseconds = 30) {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        if (holdMilliseconds > 0)
            Thread.Sleep(holdMilliseconds);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }
}