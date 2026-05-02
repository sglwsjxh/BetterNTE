using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;

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

        double scale = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height / 1080.0;

        var imagePath1 = Path.Combine(AppContext.BaseDirectory, "tasks", "StartGame", "assets", "startgame1.png");
        var imagePath2 = Path.Combine(AppContext.BaseDirectory, "tasks", "StartGame", "assets", "startgame2.png");
		var template1 = ImageMatch.GetTemplate(imagePath1, scale);
		var template2 = ImageMatch.GetTemplate(imagePath2, scale);
		if (template1 == null || template2 == null)
            return;

		using var frame = new Mat();

        Thread.Sleep(2000);
        while (true) {
			Capture.CaptureScreen(frame);
			var point = ImageMatch.FindImageCenter(frame, template1, 0.8);
            if (point != null) {
                AutoClick.Click(point.Value.X, point.Value.Y);
                break;
            }

            Thread.Sleep(500);
        }

        Thread.Sleep(7000);
        while (true) {
			Capture.CaptureScreen(frame);
			var point = ImageMatch.FindImageCenter(frame, template2, 0.8);
            if (point != null)
                break;

            Thread.Sleep(500);
        }

        Thread.Sleep(1000);
        while (true) {
			Capture.CaptureScreen(frame);
			var point = ImageMatch.FindImageCenter(frame, template2, 0.8);
            if (point == null) {
                AutoClick.Click((int)(960 * scale), (int)(540 * scale));
                break;
            }

            Thread.Sleep(500);
        }
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