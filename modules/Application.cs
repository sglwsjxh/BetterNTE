using System;
using System.Diagnostics;
using System.Threading;
using OpenCvSharp;

class Application {
    volatile bool _isRunning = false;
    readonly GameConfig _config;
    bool _skipStartGame2;
    int _loopCount;

    public event Action? OnGameExited;

    public Application(GameConfig config) {
        _config = config;
    }

    public void Run() {
        _isRunning = true;
        AppLog.Write("Application loop starting");
        WatchGameProcessExit();

        StartGame.RunStartup(_config);
        AppLog.Write("StartGame startup finished, entering task loop");

        using var frame = new Mat();
        while (_isRunning) {
            Capture.CaptureScreen(frame);

            var autoTeleportFound = false;
            if (_config.Options.AutoTeleport)
                autoTeleportFound = AutoTeleportTask.Run(frame);

            if (autoTeleportFound && !_skipStartGame2) {
                _skipStartGame2 = true;
                AppLog.Write("StartGame2 checks disabled after AutoTeleport target was found");
            }

            if (_config.Options.AutoDismiss && _loopCount % 5 == 0)
                AutoDismissTask.Run(frame);

            if (_config.Options.AutoSkip && _loopCount % 3 == 0)
                AutoSkipTask.Run(frame);

            if (_config.Options.AutoClose && _loopCount % 2 == 0)
                AutoCloseTask.Run(frame);

            if (!_skipStartGame2 && _loopCount % 3 == 0)
                _skipStartGame2 = StartGame.RunPostLaunch(frame);

            // HoldToRepeatTask.Update();

            _loopCount++;

            Thread.Sleep(50);
        }

        ImageMatch.ClearTemplateCache();
        AppLog.Write("Application loop stopped");
    }

    public void Stop() {
        _isRunning = false;
        AppLog.Write("Application stop requested");
    }

    void WatchGameProcessExit() {
        _ = Task.Run(() => {
            while (_isRunning) {
                var proc = Process.GetProcessesByName(StartGame.ProcessName).FirstOrDefault();
                if (proc == null) {
                    Thread.Sleep(1000);
                    continue;
                }

                AppLog.Write($"Game process watcher attached. ProcessId={proc.Id}");
                proc.WaitForExit();

                if (!_isRunning)
                    return;

                AppLog.Write("Game process exited");
                Stop();
                OnGameExited?.Invoke();
                return;
            }
        });
    }
}
