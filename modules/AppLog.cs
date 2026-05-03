static class AppLog {
    static readonly object _sync = new();
    static string _logPath = "";

    public static void Initialize() {
        var logDir = Path.Combine(Environment.CurrentDirectory, "logs");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, $"betternte-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        Write("Log initialized");
    }

    public static void Write(string message) {
        if (string.IsNullOrEmpty(_logPath))
            return;

        lock (_sync) {
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
    }
}