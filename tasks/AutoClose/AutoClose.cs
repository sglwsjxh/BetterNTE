using OpenCvSharp;
using System;
using System.IO;

static class AutoCloseTask {
	const double MATCH_THRESHOLD = 0.75;
	static readonly TimeSpan _clickInterval = TimeSpan.FromMilliseconds(800);
	static readonly TimeSpan _logInterval = TimeSpan.FromSeconds(1);
	static DateTime _lastClickAt = DateTime.MinValue;
	static DateTime _lastLogAt = DateTime.MinValue;

	public static bool Run(Mat frame) {
		var imagePath = Path.Combine(AppContext.BaseDirectory, "tasks", "AutoClose", "assets", "autoclose.png");
		var template = ImageMatch.GetTemplate(imagePath);
		if (template == null) {
			LogThrottled($"AutoClose template unavailable. Path={imagePath}");
			return false;
		}

		var match = ImageMatch.FindBestMatch(frame, template);
		if (match == null) {
			LogThrottled($"AutoClose match skipped. Frame={frame.Width}x{frame.Height}, Template={template.Width}x{template.Height}");
			return false;
		}

		var point = (X: match.Value.X + template.Width / 2, Y: match.Value.Y + template.Height / 2);
		LogThrottled($"AutoClose match. Score={match.Value.Score:F4}, Threshold={MATCH_THRESHOLD:F2}, Frame={frame.Width}x{frame.Height}, Template={template.Width}x{template.Height}, TopLeft=({match.Value.X},{match.Value.Y}), Center=({point.X},{point.Y})");

		if (match.Value.Score < MATCH_THRESHOLD)
			return false;

		var now = DateTime.UtcNow;
		if (now - _lastClickAt < _clickInterval)
			return true;

		AutoClick.Click(point.X, point.Y);
		_lastClickAt = now;
		AppLog.Write($"AutoClose clicked. Score={match.Value.Score:F4}, Center=({point.X},{point.Y})");
		return true;
	}

	static void LogThrottled(string message) {
		var now = DateTime.UtcNow;
		if (now - _lastLogAt < _logInterval)
			return;

		_lastLogAt = now;
		AppLog.Write(message);
	}
}
