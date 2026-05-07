using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

static class HoldToRepeatTask {
    const int VK_LBUTTON = 0x01;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);

    static int _gameProcessId = -1;
    static DateTime _lastGameCheck = DateTime.MinValue;

    public static void Update() {
        if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0) return;

        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return;

        GetWindowThreadProcessId(hwnd, out uint activePid);

        if ((DateTime.Now - _lastGameCheck).TotalSeconds > 5 || _gameProcessId == -1) {
            var proc = Process.GetProcessesByName(StartGame.ProcessName).FirstOrDefault();
            _gameProcessId = proc?.Id ?? -1;
            _lastGameCheck = DateTime.Now;
        }

        if (activePid != _gameProcessId) return;

        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        Thread.Sleep(20);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }
}

