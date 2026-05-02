using System.Diagnostics;
using OpenCvSharp;

static class AutoTeleportTask {
	const string PROCESS_NAME = "HTGame";
	static readonly TimeSpan _clickInterval = TimeSpan.FromMilliseconds(800);
	static Mat? _template;
	static bool _templateLoaded;
	static DateTime _lastClickAt = DateTime.MinValue;

	public static void Run(Mat frame) {
		if (Process.GetProcessesByName(PROCESS_NAME).Length == 0)
			return;
		if (!EnsureTemplate())
			return;

		var point = ImageMatch.FindImageCenter(frame, _template!, 0.8);
		if (point == null)
			return;

		var now = DateTime.UtcNow;
		if (now - _lastClickAt < _clickInterval)
			return;

		AutoClick.Click(point.Value.X, point.Value.Y);
		_lastClickAt = now;
	}

	static bool EnsureTemplate() {
		if (_templateLoaded)
			return _template != null;

		var imagePath = Path.Combine(AppContext.BaseDirectory, "tasks", "AutoTeleport", "assets", "autoteleport.png");
		_template = ImageMatch.GetTemplate(imagePath);
		_templateLoaded = true;
		return _template != null;
	}
}
