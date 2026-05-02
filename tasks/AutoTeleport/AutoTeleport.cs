using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;

static class AutoTeleportTask {
	const string PROCESS_NAME = "HTGame";
	const int SW_RESTORE = 9;
	static readonly TimeSpan _clickInterval = TimeSpan.FromMilliseconds(800);
	static Mat? _template;
	static bool _templateLoaded;
	static DateTime _lastClickAt = DateTime.MinValue;

	[DllImport("user32.dll")]
	static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
	[DllImport("user32.dll")]
	static extern bool SetForegroundWindow(IntPtr hWnd);

	public static void Run(Mat frame) {
		var proc = Process.GetProcessesByName(PROCESS_NAME).FirstOrDefault();
		if (proc == null)
			return;
		if (!EnsureTemplate())
			return;

		var point = ImageMatch.FindImageCenter(frame, _template!, 0.8);
		if (point == null)
			return;

		var now = DateTime.UtcNow;
		if (now - _lastClickAt < _clickInterval)
			return;

		if (proc.MainWindowHandle != IntPtr.Zero) {
			ShowWindowAsync(proc.MainWindowHandle, SW_RESTORE);
			SetForegroundWindow(proc.MainWindowHandle);
		}

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
