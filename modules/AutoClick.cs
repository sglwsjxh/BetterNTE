using System.Runtime.InteropServices;
using System.Threading;

static class AutoClick {
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint KEYEVENTF_KEYDOWN = 0x0000;
    const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);
    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    public static void Click(int x, int y, int holdMilliseconds = 30) {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        if (holdMilliseconds > 0)
            Thread.Sleep(holdMilliseconds);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public static void SendKeyboard(byte vk) {
        keybd_event(vk, 0, KEYEVENTF_KEYDOWN, 0);
        Thread.Sleep(10);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
    }

    public static void SendKeyboard(byte vk, int holdMs) {
        keybd_event(vk, 0, KEYEVENTF_KEYDOWN, 0);
        if (holdMs > 0)
            Thread.Sleep(holdMs);
        keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
    }
}