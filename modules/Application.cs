using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

class Application {
    readonly GameConfig _config;
    bool _skipStartGame2;
    int _loopCount;
    CancellationTokenSource? _stopSource;
    Task? _runTask;
    readonly object _lifecycleSync = new();

    public event Action? OnGameExited;
    public event Action<EngineStatus>? StatusChanged;
    public event Action<string>? LogEmitted;
    public event Action? EngineExited;

    public Application(GameConfig config) {
        _config = config;
    }

    public void Run() {
        lock (_lifecycleSync) {
            _stopSource?.Dispose();
            _stopSource = new CancellationTokenSource();
        }

        Run(_stopSource.Token);
    }

    public void Run(CancellationToken cancellationToken) {
        Run(cancellationToken, true);
    }

    public void Run(CancellationToken cancellationToken, bool runStartup) {
        AppLog.OnLogWritten += HandleLogWritten;
        StatusChanged?.Invoke(EngineStatus.Starting);
        AppLog.Write("Application loop starting");

        try {
            if (runStartup)
                StartGame.RunStartup(_config);

            AppLog.Write("StartGame startup finished, entering task loop");
            StatusChanged?.Invoke(EngineStatus.Running);

            using var frame = new Mat();
            while (!cancellationToken.IsCancellationRequested) {
                Capture.CaptureScreen(frame);

                // Snapshot config at each iteration to avoid stale reads
                var autoTeleport = _config.Options.AutoTeleport;
                var autoDismiss = _config.Options.AutoDismiss;
                var autoSkip = _config.Options.AutoSkip;
                var autoClose = _config.Options.AutoClose;

                var autoTeleportFound = false;
                if (autoTeleport)
                    autoTeleportFound = AutoTeleportTask.Run(frame);

                if (autoTeleportFound && !_skipStartGame2) {
                    _skipStartGame2 = true;
                    AppLog.Write("StartGame2 checks disabled after AutoTeleport target was found");
                }

                if (autoDismiss && _loopCount % 5 == 0)
                    AutoDismissTask.Run(frame);

                if (autoSkip && _loopCount % 3 == 0)
                    AutoSkipTask.Run(frame, cancellationToken);

                if (autoClose && _loopCount % 2 == 0)
                    AutoCloseTask.Run(frame);

                if (!_skipStartGame2 && _loopCount % 3 == 0)
                    _skipStartGame2 = StartGame.RunPostLaunch(frame);

                _loopCount++;

                if (cancellationToken.WaitHandle.WaitOne(50))
                    break;
            }
        } catch (OperationCanceledException) {
        } finally {
            ImageMatch.ClearTemplateCache();
            AppLog.Write("Application loop stopped");
            AppLog.OnLogWritten -= HandleLogWritten;
            StatusChanged?.Invoke(EngineStatus.Stopped);
            EngineExited?.Invoke();
        }
    }

    public void Stop() {
        AppLog.Write("Application stop requested");
        StatusChanged?.Invoke(EngineStatus.Stopping);
        lock (_lifecycleSync) {
            _stopSource?.Cancel();
            _runTask?.Wait();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        lock (_lifecycleSync) {
            _runTask = Task.Run(() => Run(cancellationToken), CancellationToken.None);
            return _runTask;
        }
    }

    public void NotifyGameExited() {
        OnGameExited?.Invoke();
    }

    void HandleLogWritten(string line) {
        LogEmitted?.Invoke(line);
    }
}
