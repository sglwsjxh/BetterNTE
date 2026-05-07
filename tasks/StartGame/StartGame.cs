using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;

class StartGame {
    const int SW_MAXIMIZE = 3;
    const double MATCH_THRESHOLD = 0.78;
    const string PROCESS_NAME = "HTGame";
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

    [DllImport("user32.dll")]
    static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    static bool IsGameRunning() {
        return Process.GetProcessesByName(PROCESS_NAME).Length > 0;
    }

    static void LaunchGame(string gameDir) {
        AppLog.Write($"StartGame launching. GameDir={gameDir}");
        Process.Start(Path.Combine(gameDir, "NTELauncher", "NTEGame.exe"));

        var imagePath1 = Path.Combine(AppContext.BaseDirectory, "tasks", "StartGame", "assets", "startgame1.png");
		var template1 = ImageMatch.GetTemplate(imagePath1);
		if (template1 == null) {
            AppLog.Write("StartGame template unavailable. Template1=False");
            return;
		}

		using var frame = new Mat();

        Thread.Sleep(2000);
        var attempts = 0;
        while (true) {
			attempts++;
			Capture.CaptureScreen(frame);
            var match = ImageMatch.FindBestMatch(frame, template1);
            LogStartGameMatch("startgame1", attempts, frame, template1, match, MATCH_THRESHOLD);
            var point = match != null && match.Value.Score >= MATCH_THRESHOLD
                ? (X: match.Value.X + template1.Width / 2, Y: match.Value.Y + template1.Height / 2)
                : ((int X, int Y)?)null;
            if (point != null) {
                AutoClick.Click(point.Value.X, point.Value.Y);
                AppLog.Write($"StartGame clicked startgame1. Attempts={attempts}, Center=({point.Value.X},{point.Value.Y})");
                break;
            }

            Thread.Sleep(500);
        }
    }

    static void MaximizeGame() {
        var proc = Process.GetProcessesByName(PROCESS_NAME).FirstOrDefault();
        if (proc != null) {
            AppLog.Write($"StartGame maximizing existing process. ProcessId={proc.Id}, MainWindowHandle={proc.MainWindowHandle}");
            ShowWindowAsync(proc.MainWindowHandle, SW_MAXIMIZE);
            SetForegroundWindow(proc.MainWindowHandle);
        }
    }

    public static void RunStartup(GameConfig config) {
        _postLaunchState = PostLaunchState.WaitingForMarker;
        _postLaunchAttempts = 0;

        if (IsGameRunning()) {
            AppLog.Write("StartGame found existing game process");
            MaximizeGame();
        } else {
            LaunchGame(config.GameInstallDir);
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