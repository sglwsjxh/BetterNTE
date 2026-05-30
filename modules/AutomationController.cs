using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

enum EngineStatus {
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}

class AutomationController {
    readonly object _lifecycleSync = new();
    readonly object _configSync = new();
    GameConfig _config = new();
    Application? _application;
    CancellationTokenSource? _stopSource;
    Task? _runTask;
    EngineStatus _status = EngineStatus.Stopped;

    public bool IsRunning => Status == EngineStatus.Starting || Status == EngineStatus.Running || Status == EngineStatus.Stopping;
    public EngineStatus Status {
        get {
            lock (_lifecycleSync)
                return _status;
        }
    }

    public event Action<EngineStatus>? StatusChanged;
    public event Action<string>? LogEmitted;
    public event Action<string>? ErrorOccurred;

    public AutomationController() {
        AppLog.OnLogWritten += line => LogEmitted?.Invoke(line);
    }

    public void Start(GameConfig config) {
        lock (_lifecycleSync) {
            if (IsRunning)
                return;

            _config = CloneConfig(config);
            _stopSource?.Dispose();
            _stopSource = new CancellationTokenSource();
            SetStatus(EngineStatus.Starting);

            var token = _stopSource.Token;
            _runTask = Task.Run(() => RunEngine(token), CancellationToken.None);
        }
    }

    public void Stop() {
        Task? runTask;
        lock (_lifecycleSync) {
            if (_status == EngineStatus.Stopped)
                return;

            SetStatus(EngineStatus.Stopping);
            _stopSource?.Cancel();
            runTask = _runTask;
        }

        if (runTask == null) {
            ImageMatch.ClearTemplateCache();
            SetStatus(EngineStatus.Stopped);
            return;
        }

        try {
            if (!runTask.Wait(TimeSpan.FromSeconds(3))) {
                AppLog.Write("Stop warning — engine did not stop within 3 seconds, waiting in background");
                // Keep Stopping status — RunEngine's finally will set Stopped when task actually finishes
                return;
            }
        } catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) {
            AppLog.Write("Stop: run task cancelled as expected");
        }

        // Task completed — RunEngine's finally already set status
        ImageMatch.ClearTemplateCache();
        if (Status != EngineStatus.Stopped)
            SetStatus(EngineStatus.Stopped);
    }

    public GameConfig GetConfig() {
        lock (_configSync)
            return CloneConfig(_config);
    }

    public void UpdateConfig(GameConfig newConfig) {
        GameConfig snapshot;
        lock (_configSync) {
            _config.GameInstallDir = newConfig.GameInstallDir;
            _config.Options.AutoTeleport = newConfig.Options.AutoTeleport;
            _config.Options.AutoPickup = newConfig.Options.AutoPickup;
            _config.Options.AutoSkip = newConfig.Options.AutoSkip;
            _config.Options.AutoDismiss = newConfig.Options.AutoDismiss;
            _config.Options.AutoClose = newConfig.Options.AutoClose;
            _config.Options.AutoClick = newConfig.Options.AutoClick;
            snapshot = CloneConfig(_config);
        }

        Config.Save(snapshot);
        AppLog.Write("AutomationController config updated");
    }

    void RunEngine(CancellationToken cancellationToken) {
        try {
            var config = GetConfig();
            var existingProcs = Process.GetProcessesByName(StartGame.ProcessName);
            var gameExists = existingProcs.Length > 0;
            foreach (var p in existingProcs) p.Dispose();
            if (gameExists) {
                AppLog.Write("AutomationController starting with existing game process");
                StartGame.RunStartup(config, cancellationToken);
            } else {
                AppLog.Write("AutomationController launching game before engine loop");
                StartGame.RunStartup(config, cancellationToken);
                if (!WaitForGameProcess(cancellationToken)) {
                    AppLog.Write("Game process did not start — aborting engine startup");
                    return;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            SetStatus(EngineStatus.Running);
            _application = new Application(_config);
            _application.StatusChanged += HandleApplicationStatusChanged;
            _application.EngineExited += HandleApplicationExited;
            _application.Run(cancellationToken, false);
        } catch (OperationCanceledException) {
        } catch (OpenCVException ex) {
            HandleFatalError("An OpenCV error occurred. Check the logs for details.", ex);
        } catch (Exception ex) {
            HandleFatalError("An unexpected error occurred. Check the logs for details.", ex);
        } finally {
            if (Status != EngineStatus.Error)
                SetStatus(EngineStatus.Stopped);
        }
    }

    bool WaitForGameProcess(CancellationToken cancellationToken) {
        var attempts = 0;
        while (!cancellationToken.IsCancellationRequested) {
            attempts++;
            if (attempts > StartGame.WAIT_PROCESS_MAX_ATTEMPTS) {
                AppLog.Write($"WaitForGameProcess timeout after {StartGame.WAIT_PROCESS_MAX_ATTEMPTS} attempts");
                return false;
            }

            var procs = Process.GetProcessesByName(StartGame.ProcessName);
            var found = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            if (found)
                return true;

            if (cancellationToken.WaitHandle.WaitOne(500))
                return false;
        }
        return false;
    }

    void HandleApplicationStatusChanged(EngineStatus status) {
        if (status == EngineStatus.Starting)
            return;

        SetStatus(status);
    }

    void HandleApplicationExited() {
        SetStatus(EngineStatus.Stopped);
    }

    void HandleFatalError(string userMessage, Exception exception) {
        AppLog.Write($"AutomationController fatal error. {exception}");
        ErrorOccurred?.Invoke(userMessage);
        SetStatus(EngineStatus.Error);
    }

    void SetStatus(EngineStatus status) {
        var changed = false;
        lock (_lifecycleSync) {
            if (_status != status) {
                _status = status;
                changed = true;
            }
        }

        if (changed)
            StatusChanged?.Invoke(status);
    }

    static GameConfig CloneConfig(GameConfig config) {
        return new GameConfig {
            GameInstallDir = config.GameInstallDir,
            Options = new Options {
                AutoTeleport = config.Options.AutoTeleport,
                AutoPickup = config.Options.AutoPickup,
                AutoSkip = config.Options.AutoSkip,
                AutoDismiss = config.Options.AutoDismiss,
                AutoClose = config.Options.AutoClose,
                AutoClick = config.Options.AutoClick
            }
        };
    }
}
