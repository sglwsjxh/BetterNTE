using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

public partial class App : System.Windows.Application {
#if DEBUG
    const string MutexName = "BetterNTE_SingleInstance_Mutex";
    const string ActivationEventName = "BetterNTE_Activate_MainWindow";
#else
    const string MutexName = "Global\\BetterNTE_SingleInstance_Mutex";
    const string ActivationEventName = "Global\\BetterNTE_Activate_MainWindow";
#endif

    Mutex? _mutex;
    EventWaitHandle? _activationEvent;
    CancellationTokenSource? _activationListenerCts;
    Forms.NotifyIcon? _notifyIcon;
    Icon? _trayIcon;
    AutomationController? _controller;
    internal AutomationController Controller => _controller!;
    bool _isExiting;

    protected override void OnStartup(StartupEventArgs e) {
        // 先检查管理员权限，在 Mutex 之前执行
        if (!Environment.IsPrivilegedProcess) {
            StartElevatedInstance();
            Shutdown();
            return;
        }

        // Step: 单实例互斥锁
        bool createdNew;
        _mutex = new Mutex(true, MutexName, out createdNew);
        if (!createdNew) {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        Forms.Application.SetHighDpiMode(Forms.HighDpiMode.PerMonitorV2);
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);

        AppLog.Initialize();
        CrashDump.Initialize();
        AppLog.Write($"Application started. CurrentDirectory={Environment.CurrentDirectory}, BaseDirectory={AppContext.BaseDirectory}");

        ImageMatch.InitializeScreenScale();

        var config = Config.Load();
        AppLog.Write($"Config loaded. AutoTeleport={config.Options.AutoTeleport}, GameInstallDir={config.GameInstallDir}");

        if (string.IsNullOrWhiteSpace(config.GameInstallDir) || !Directory.Exists(config.GameInstallDir)) {
            var firstRun = new FirstRunConfigWindow();
            if (firstRun.ShowDialog() != true) {
                Shutdown();
                return;
            }

            config = Config.Load();
            AppLog.Write($"First-run config completed. GameInstallDir={config.GameInstallDir}");
        }

        _controller = new AutomationController();
        _controller.StatusChanged += status => AppLog.Write($"Controller status changed: {status}");
        // LogEmitted is relayed via AppLog.OnLogWritten subscription in AutomationController constructor
        _controller.ErrorOccurred += message => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;
            Dispatcher.BeginInvoke(() => {
                AppLog.Write($"Controller error: {message}");
            });
        };
        _controller.UpdateConfig(config);

        CreateActivationListener();
        CreateTrayIcon();

        MainWindow = new MainWindow();
        base.OnStartup(e);

        ShowMainWindow();
    }

    protected override void OnExit(ExitEventArgs e) {
        AppLog.Write("OnExit: stopping controller");
        _controller?.Stop();
        AppLog.Write("OnExit: cancelling activation listener");
        _activationListenerCts?.Cancel();
        AppLog.Write("OnExit: disposing activation event");
        _activationEvent?.Dispose();
        AppLog.Write("OnExit: disposing notify icon");
        _notifyIcon?.Dispose();
        AppLog.Write("OnExit: disposing tray icon");
        _trayIcon?.Dispose();
        AppLog.Write("OnExit: disposing mutex");
        _mutex?.Dispose();
        AppLog.Write("OnExit: cleanup complete");
        base.OnExit(e);
    }

    void SignalExistingInstance() {
        try {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        } catch (WaitHandleCannotBeOpenedException) {
            Forms.MessageBox.Show("BetterNTE 已经在运行中！", "提示", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
        }
    }

    static void StartElevatedInstance() {
        var psi = new ProcessStartInfo(Environment.ProcessPath!) {
            Verb = "runas",
            UseShellExecute = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        Process.Start(psi);
    }

    void CreateActivationListener() {
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationListenerCts = new CancellationTokenSource();
        var token = _activationListenerCts.Token;

        _ = Task.Run(() => {
            while (!token.IsCancellationRequested) {
                if (!_activationEvent.WaitOne(250))
                    continue;

                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                    return;
                Dispatcher.BeginInvoke(ShowMainWindow);
            }
        }, token);
    }

    void CreateTrayIcon() {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 BetterNTE", null, (_, _) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(ShowMainWindow);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(ExitApplication);
        });

        _trayIcon = LoadTrayIcon();

        _notifyIcon = new Forms.NotifyIcon {
            Icon = _trayIcon,
            Text = "BetterNTE",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(ShowMainWindow);
        };
    }

    static Icon LoadTrayIcon() {
        try {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "background", "logo.ico");
            if (!File.Exists(iconPath))
                return (Icon)SystemIcons.Application.Clone();

            return new Icon(iconPath);
        } catch (Exception ex) {
            AppLog.Write($"Failed to load tray icon: {ex.Message}");
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    void ShowMainWindow() {
        if (MainWindow == null)
            return;

        if (!MainWindow.IsVisible)
            MainWindow.Show();

        if (MainWindow.WindowState == WindowState.Minimized)
            MainWindow.WindowState = WindowState.Normal;

        MainWindow.Activate();
        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
        MainWindow.Focus();
    }

    public void ExitApplication() {
        if (_isExiting)
            return;

        _isExiting = true;
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        _controller?.Stop();
        if (_controller?.IsRunning == true) {
            AppLog.Write("App exiting while engine is running — stopping engine first");
        }
        Shutdown();
    }
}
