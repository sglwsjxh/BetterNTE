using System.Diagnostics;
using System.Runtime.InteropServices;

class StartGame {
    const int SW_MAXIMIZE = 3;
    const string PROCESS_NAME = "HTGame";

    [DllImport("user32.dll")]
    static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    static bool IsGameRunning() {
        return Process.GetProcessesByName(PROCESS_NAME).Length > 0;
    }

    static void LaunchGame(string gameDir) {
        Process.Start(Path.Combine(gameDir, "NTELauncher", "NTEGame.exe"));
    }

    static void MaximizeGame() {
        var proc = Process.GetProcessesByName(PROCESS_NAME).FirstOrDefault();
        if (proc != null) {
            ShowWindowAsync(proc.MainWindowHandle, SW_MAXIMIZE);
            SetForegroundWindow(proc.MainWindowHandle);
        }
    }

    public static void Run() {
        var config = Config.Load();
        if (IsGameRunning())
            MaximizeGame();
        else
            LaunchGame(config.GameInstallDir);
    }
}