using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;

class StartGame {
    const int SW_MAXIMIZE = 3;
    const double MATCH_THRESHOLD = 0.78;
    const string PROCESS_NAME = "HTGame";
    const string LAUNCHER_PROCESS_NAME = "NTEGame";
    static PostLaunchState _postLaunchState = PostLaunchState.WaitingForMarker;
    static int _postLaunchAttempts;

    enum PostLaunchState {
        WaitingForMarker,
        WaitingForMarkerGone,
        Finished
    }

    public static string ProcessName => PROCESS_NAME;

    public static void ResetForRestart() {
        _postLaunchState = PostLaunchState.WaitingForMarker;
        _postLaunchAttempts = 0;
        AppLog.Write("StartGame state reset for restart");
    }

    [DllImport("user32.dll")]
    static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    static bool IsGameRunning() {
        var procs = Process.GetProcessesByName(PROCESS_NAME);
        var exists = procs.Length > 0;
        foreach (var p in procs) p.Dispose();
        return exists;
    }

    const int LAUNCH_MAX_ATTEMPTS = 60;
    internal const int WAIT_PROCESS_MAX_ATTEMPTS = 60;

    static void LaunchGame(string gameDir, CancellationToken cancellationToken) {
        if (cancellationToken.IsCancellationRequested) {
            AppLog.Write("StartGame launch cancelled before starting process");
            return;
        }
        AppLog.Write($"StartGame launching. GameDir={gameDir}");
        Process.Start(Path.Combine(gameDir, "NTELauncher", "NTEGame.exe"))?.Dispose();

        OcrHelper.EnsureInitialized();
		using var frame = new Mat();

        if (cancellationToken.WaitHandle.WaitOne(2000))
            return;

        var attempts = 0;
        IntPtr launcherHwnd = IntPtr.Zero;
        while (!cancellationToken.IsCancellationRequested) {
			attempts++;
            if (attempts > LAUNCH_MAX_ATTEMPTS) {
                AppLog.Write($"StartGame launch timeout after {LAUNCH_MAX_ATTEMPTS} attempts");
                return;
            }

            if (launcherHwnd == IntPtr.Zero) {
                launcherHwnd = WindowHelper.FindWindowByProcessName(LAUNCHER_PROCESS_NAME);
                if (launcherHwnd != IntPtr.Zero)
                    AppLog.Write($"StartGame found launcher window. Handle=0x{launcherHwnd:X8}");
            }

            FramePreprocessor.CaptureAndPreprocess(frame, launcherHwnd);
            if (frame.Empty()) {
                if (cancellationToken.WaitHandle.WaitOne(500))
                    return;
                continue;
            }

            var point = OcrHelper.FindText(frame, "开始");
            if (point != null) {
                AutoClick.ClickInWindow(launcherHwnd, point.Value.X, point.Value.Y);
                AppLog.Write($"StartGame clicked '开始游戏' via OCR. Attempts={attempts}, Center=({point.Value.X},{point.Value.Y})");
                return;
            }

            if (cancellationToken.WaitHandle.WaitOne(500))
                return;
        }
    }

    static void MaximizeGame() {
        var procs = Process.GetProcessesByName(PROCESS_NAME);
        var proc = procs.Length > 0 ? procs[0] : null;
        for (int i = 1; i < procs.Length; i++) procs[i].Dispose();
        if (proc != null) {
            var pid = proc.Id;
            var hwnd = proc.MainWindowHandle;
            AppLog.Write($"StartGame maximizing existing process. ProcessId={pid}, MainWindowHandle={hwnd}");
            ShowWindowAsync(hwnd, SW_MAXIMIZE);
            SetForegroundWindow(hwnd);
            proc.Dispose();
        }
    }

    public static void RunStartup(GameConfig config) {
        RunStartup(config, CancellationToken.None);
    }

    public static void RunStartup(GameConfig config, CancellationToken cancellationToken) {
        ResetForRestart();

        if (IsGameRunning()) {
            AppLog.Write("StartGame found existing game process");
            MaximizeGame();
        } else {
            LaunchGame(config.GameInstallDir, cancellationToken);
        }
    }

    public static bool RunPostLaunch(Mat frame) {
        if (_postLaunchState == PostLaunchState.Finished)
            return true;

        _postLaunchAttempts++;
        var found = OcrHelper.FindText(frame, "进入游戏");

        if (_postLaunchState == PostLaunchState.WaitingForMarker) {
            if (found != null) {
                _postLaunchState = PostLaunchState.WaitingForMarkerGone;
                AppLog.Write($"StartGame detected '进入游戏' via OCR. Attempts={_postLaunchAttempts}");
            }
            return false;
        }

        if (found == null) {
            AutoClick.Click(960, 540);
            _postLaunchState = PostLaunchState.Finished;
            AppLog.Write($"StartGame clicked center after '进入游戏' disappeared. Attempts={_postLaunchAttempts}");
            return true;
        }
        return false;
    }

    static void LogStartGameMatch(string name, int attempts, Mat frame, Mat template, (int X, int Y, double Score)? match, double threshold) {
        if (attempts % 4 != 1)
            return;

        if (match == null) {
            AppLog.Write($"StartGame match {name}. Attempt={attempts}, NoResult, Frame={frame.Width}x{frame.Height}, Template={template.Width}x{template.Height}");
            return;
        }

        AppLog.Write($"StartGame match {name}. Attempt={attempts}, Score={match.Value.Score:F4}, Threshold={threshold:F2}, Frame={frame.Width}x{frame.Height}, Template={template.Width}x{template.Height}, TopLeft=({match.Value.X},{match.Value.Y})");
    }
}
