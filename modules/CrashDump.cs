using Microsoft.Win32;

/// <summary>
/// 崩溃捕获系统。双保险策略：
/// 1. WER LocalDumps 捕获 OpenCV 原生崩溃（main）
/// 2. 托管异常 handler 兜底录堆栈
/// </summary>
static class CrashDump {
	static readonly object _sync = new();
	static bool _isCrashing;
	static string _crashDir = null!;

	public static void Initialize() {
		_crashDir = Path.Combine(Environment.CurrentDirectory, "logs", "crashdumps");
		Directory.CreateDirectory(_crashDir);

		ConfigureWER();

		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

		if (System.Windows.Application.Current != null)
			System.Windows.Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

		AppLog.Write($"CrashDump initialized. WER dumps + managed handler -> {_crashDir}");
	}

	static void ConfigureWER() {
		try {
			var exeName = Path.GetFileName(Environment.ProcessPath);
			if (string.IsNullOrEmpty(exeName))
				return;

			// dev 下 dotnet run 进程是 dotnet.exe，配 WER 会影响所有 .NET 进程
			if (exeName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)) {
				AppLog.Write("WER LocalDumps: skipped (running via dotnet.exe)");
				return;
			}

			// HKLM — Microsoft 文档明确规定 LocalDumps 不支持 HKCU
			// 应用已提权运行，写 HKLM 无权限问题
			var werPath = $@"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\{exeName}";
			using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
				.CreateSubKey(werPath);
			if (key == null)
				return;

			key.SetValue("DumpFolder", _crashDir, RegistryValueKind.ExpandString); // REG_EXPAND_SZ
			key.SetValue("DumpCount", 5, RegistryValueKind.DWord);
			key.SetValue("DumpType", 2, RegistryValueKind.DWord); // 2 = Full dump

			AppLog.Write($"WER LocalDumps configured. Exe={exeName}, Folder={_crashDir}, Count=5, Type=Full");
		} catch (Exception ex) {
			AppLog.Write($"WER LocalDumps config failed: {ex.Message}");
		}
	}

	static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
		lock (_sync) {
			if (_isCrashing) return;
			_isCrashing = true;
		}

		var timestamp = DateTime.Now;
		var ex = e.ExceptionObject as Exception;

		// 直接写文件绕过 AppLog 防死锁
		WriteCrashReport(timestamp, ex, e.IsTerminating, "AppDomain.UnhandledException");

		try {
			AppLog.Write($"[CRASH] AppDomain unhandled: {ex?.GetType().Name} IsTerminating={e.IsTerminating}");
		} catch { }
	}

	static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
		lock (_sync) {
			if (_isCrashing) return;
			_isCrashing = true;
		}

		var timestamp = DateTime.Now;
		WriteCrashReport(timestamp, e.Exception, true, "Dispatcher.UnhandledException");

		try {
			AppLog.Write($"[CRASH] Dispatcher unhandled: {e.Exception.GetType().Name}");
		} catch { }

		// 不设置 e.Handled，让异常继续传播触发 WER dump
	}

	static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
		try {
			var inner = e.Exception?.InnerException;
			AppLog.Write($"[CRASH] Unobserved task exception: {inner?.GetType().Name} - {inner?.Message}");
		} catch { }

		e.SetObserved();
	}

	static void WriteCrashReport(DateTime timestamp, Exception? ex, bool isTerminating, string source) {
		try {
			var path = Path.Combine(_crashDir, $"crash-{timestamp:yyyyMMdd-HHmmss}.log");
			var content = $"""
				===== CRASH REPORT =====
				Source: {source}
				Time: {timestamp:yyyy-MM-dd HH:mm:ss.fff}
				IsTerminating: {isTerminating}

				Exception Type: {ex?.GetType().FullName ?? "(not a managed exception)"}
				Message: {ex?.Message ?? "(no message)"}

				Full Exception:
				{ex?.ToString() ?? "(exception object unavailable)"}
				========================
				""";
			File.WriteAllText(path, content);
		} catch { }
	}
}
