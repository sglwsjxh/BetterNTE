using OpenCvSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

static class AutoSkipTask {
	const double MATCH_THRESHOLD = 0.85;
	static readonly TimeSpan _clickInterval = TimeSpan.FromMilliseconds(800);
	static readonly TimeSpan _logInterval = TimeSpan.FromSeconds(1);
	static DateTime _lastClickAt = DateTime.MinValue;
	static DateTime _lastLogAt = DateTime.MinValue;

	public static bool Run(Mat frame) {
		var autoskipPath = Path.Combine(AppContext.BaseDirectory, "tasks", "AutoSkip", "assets", "autoskip.png");
		var autoskipTemplate = ImageMatch.GetTemplate(autoskipPath);
		if (autoskipTemplate == null) {
			LogThrottled($"AutoSkip template unavailable. Path={autoskipPath}");
			return false;
		}

		var messagesPath = Path.Combine(AppContext.BaseDirectory, "tasks", "AutoSkip", "assets", "messages.png");
		var messagesTemplate = ImageMatch.GetTemplate(messagesPath);
		if (messagesTemplate == null) {
			LogThrottled($"AutoSkip messages template unavailable. Path={messagesPath}");
			return false;
		}

		var autoskipMatch = ImageMatch.FindBestMatch(frame, autoskipTemplate);
		if (autoskipMatch == null) {
			LogThrottled($"AutoSkip match skipped. Frame={frame.Width}x{frame.Height}, Template={autoskipTemplate.Width}x{autoskipTemplate.Height}");
			return false;
		}

		var messagesMatch = ImageMatch.FindBestMatch(frame, messagesTemplate);
		if (messagesMatch == null) {
			LogThrottled($"AutoSkip messages match skipped. Frame={frame.Width}x{frame.Height}, Template={messagesTemplate.Width}x{messagesTemplate.Height}");
			return false;
		}

		if (autoskipMatch.Value.Score < MATCH_THRESHOLD || messagesMatch.Value.Score < MATCH_THRESHOLD)
			return false;

		var point = (X: autoskipMatch.Value.X + autoskipTemplate.Width / 2, Y: autoskipMatch.Value.Y + autoskipTemplate.Height / 2);
		LogThrottled($"AutoSkip match. AutoSkipScore={autoskipMatch.Value.Score:F4}, MessagesScore={messagesMatch.Value.Score:F4}, Threshold={MATCH_THRESHOLD:F2}, Frame={frame.Width}x{frame.Height}, AutoSkipTemplate={autoskipTemplate.Width}x{autoskipTemplate.Height}, MessagesTemplate={messagesTemplate.Width}x{messagesTemplate.Height}, TopLeft=({autoskipMatch.Value.X},{autoskipMatch.Value.Y}), Center=({point.X},{point.Y})");

		var now = DateTime.UtcNow;
		if (now - _lastClickAt < _clickInterval)
			return true;

		AutoClick.Click(point.X, point.Y);
		_lastClickAt = now;
		AppLog.Write($"AutoSkip clicked. Score={autoskipMatch.Value.Score:F4}, Center=({point.X},{point.Y})");
		
		Task.Run(() => HandleEverydayCheck());

		return true;
	}

	static void HandleEverydayCheck() {
		var checkPath = Path.Combine(AppContext.BaseDirectory, "tasks", "AutoSkip", "assets", "everydaycheck.png");
		var checkTemplate = ImageMatch.GetTemplate(checkPath);
		if (checkTemplate == null) return;

		var start = DateTime.UtcNow;
		while (DateTime.UtcNow - start < TimeSpan.FromSeconds(2)) {
			using var frame = Capture.CaptureScreen();
			var match = ImageMatch.FindBestMatch(frame, checkTemplate);
			if (match != null && match.Value.Score >= MATCH_THRESHOLD) {
				int cx = match.Value.X;
				int cy = match.Value.Y;
				int w = checkTemplate.Width;
				int h = checkTemplate.Height;

				// 点击“今日不再提示”复选框
				AutoClick.Click((int)(cx + w * 0.38), (int)(cy + h * 0.55));
				Thread.Sleep(300);

				// 点击“确认”按钮
				AutoClick.Click((int)(cx + w * 0.80), (int)(cy + h * 0.85));

				AppLog.Write($"AutoSkip handled everyday check popup.");
				break;
			}
			Thread.Sleep(100);
		}
	}

	static void LogThrottled(string message) {
		var now = DateTime.UtcNow;
		if (now - _lastLogAt < _logInterval)
			return;

		_lastLogAt = now;
		AppLog.Write(message);
	}
}
