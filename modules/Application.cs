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

        _ = Task.Run(() => {
            while (_isRunning) {
                var proc = Process.GetProcessesByName(PROCESS_NAME).FirstOrDefault();
                if (proc != null) {
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (s, e) => {
                        Stop();
                        OnExit?.Invoke();
                        Environment.Exit(0);
                    };
                    break;
                }
                Thread.Sleep(1000);
            }
        });

        StartGame.Run();

        using var frame = new Mat();
        while (_isRunning) {
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
