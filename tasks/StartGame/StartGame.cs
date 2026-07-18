using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using OpenCvSharp;

class StartGame {
    const int SW_MAXIMIZE = 3;
    const double MATCH_THRESHOLD = 0.65;
    const string PROCESS_NAME = "HTGame";
    const string LAUNCHER_PROCESS_NAME = "NTEGame";
    static readonly TimeSpan _postLaunchLogInterval = TimeSpan.FromSeconds(1);
    static Mat? _startGame2Template;
    static PostLaunchState _postLaunchState = PostLaunchState.WaitingForMarker;
    static DateTime _lastPostLaunchLogAt = DateTime.MinValue;
    static int _postLaunchAttempts;

    enum PostLaunchState {
        WaitingForMarker,
        WaitingForMarkerGone,
        Finished
    }

    public static string ProcessName => PROCESS_NAME;

    public static void ResetForRestart() {
        _startGame2Template = null;
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

        var imagePath1 = Path.Combine(AppContext.BaseDirectory, "tasks", "StartGame", "assets", "startgame1.png");
		var template1 = ImageMatch.GetTemplate(imagePath1);
		if (template1 == null) {
            AppLog.Write("StartGame template unavailable. Template1=False");
            return;
		}

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
                if (launcherHwnd != IntPtr.Zero) {
                    AppLog.Write($"StartGame found launcher window. Handle=0x{launcherHwnd:X8}");
                    WindowHelper.InitScaleFromWindow(launcherHwnd);
                    template1.Dispose();
                    template1 = ImageMatch.GetTemplate(imagePath1);
                    if (template1 == null) {
                        AppLog.Write("StartGame template unavailable after rescale. Template1=False");
                        return;
                    }
                }
            }

            bool captured;
            if (launcherHwnd != IntPtr.Zero)
                captured = Capture.CaptureWindow(frame, launcherHwnd);
            else {
                Capture.CaptureScreen(frame);
                captured = true;
            }

            if (!captured) {
                if (cancellationToken.WaitHandle.WaitOne(500))
                    return;
                continue;
            }

            var match = ImageMatch.FindBestMatchPreprocessed(frame, template1);
            LogStartGameMatch("startgame1", attempts, frame, template1, match, MATCH_THRESHOLD);
            var point = match != null && match.Value.Score >= MATCH_THRESHOLD
                ? (X: match.Value.X + template1.Width / 2, Y: match.Value.Y + template1.Height / 2)
                : ((int X, int Y)?)null;
            if (point != null) {
                if (launcherHwnd != IntPtr.Zero)
                    AutoClick.ClickInWindow(launcherHwnd, point.Value.X, point.Value.Y);
                else
                    AutoClick.Click(point.Value.X, point.Value.Y);
                AppLog.Write($"StartGame clicked startgame1. Attempts={attempts}, Center=({point.Value.X},{point.Value.Y})");
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

        var template = EnsureStartGame2Template();
        if (template == null)
            return false;

        _postLaunchAttempts++;
        var match = ImageMatch.FindBestMatch(frame, template);
        LogPostLaunchMatch(frame, template, match);

        if (_postLaunchState == PostLaunchState.WaitingForMarker) {
            if (match != null && match.Value.Score >= MATCH_THRESHOLD) {
                _postLaunchState = PostLaunchState.WaitingForMarkerGone;
                AppLog.Write($"StartGame detected startgame2. Attempts={_postLaunchAttempts}, Score={match.Value.Score:F4}");
            }

            return false;
        }

        if (match == null || match.Value.Score < MATCH_THRESHOLD) {
            AutoClick.Click((int)(960 * ImageMatch.ScreenScale), (int)(540 * ImageMatch.ScreenScale));
            _postLaunchState = PostLaunchState.Finished;
            AppLog.Write($"StartGame clicked center after startgame2 disappeared. Attempts={_postLaunchAttempts}");
            return true;
        }

        return false;
    }

    static Mat? EnsureStartGame2Template() {
        if (_startGame2Template != null)
            return _startGame2Template;

        var imagePath = Path.Combine(AppContext.BaseDirectory, "tasks", "StartGame", "assets", "startgame2.png");
        _startGame2Template = ImageMatch.GetTemplate(imagePath);
        if (_startGame2Template == null)
            AppLog.Write("StartGame template unavailable. Template2=False");

        return _startGame2Template;
    }

    static void LogPostLaunchMatch(Mat frame, Mat template, (int X, int Y, double Score)? match) {
        var now = DateTime.UtcNow;
        if (now - _lastPostLaunchLogAt < _postLaunchLogInterval)
            return;

        _lastPostLaunchLogAt = now;
        var state = _postLaunchState.ToString();
        if (match == null) {
            AppLog.Write($"StartGame2 match. State={state}, Attempt={_postLaunchAttempts}, NoResult, Frame={frame.Width}x{frame.Height}, Template={template.Width}x{template.Height}");
            return;
        }

        AppLog.Write($"StartGame2 match. State={state}, Attempt={_postLaunchAttempts}, Score={match.Value.Score:F4}, Threshold={MATCH_THRESHOLD:F2}, Frame={frame.Width}x{frame.Height}, Template={template.Width}x{template.Height}, TopLeft=({match.Value.X},{match.Value.Y})");
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
