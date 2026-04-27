using System;
using System.Diagnostics;
using System.Threading;
using OpenCvSharp;

class Application {
    const string PROCESS_NAME = "HTGame";
    volatile bool _isRunning = false;
    readonly GameConfig _config;

    public event Action? OnExit;

    public Application(GameConfig config) {
        _config = config;
    }

    public void Run() {
        _isRunning = true;
        StartGame.Run();

        using var frame = new Mat();
        while (_isRunning && Process.GetProcessesByName(PROCESS_NAME).Length > 0) {
            Capture.CaptureScreen(frame);

            if (_config.Options.AutoTeleport)
                AutoTeleportTask.Run(frame);

            Thread.Sleep(100);
        }

        ImageMatch.ClearTemplateCache();
        
        OnExit?.Invoke();
    }

    public void Stop() {
        _isRunning = false;
    }
}
