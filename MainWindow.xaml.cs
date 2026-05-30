using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;

public partial class MainWindow : Window {
    AutomationController _controller;
    ObservableCollection<string> _logEntries = new();
    bool _logAutoScroll = true;
    System.Timers.Timer? _runtimeTimer;
    DateTime _startTime;

    public MainWindow() {
        InitializeComponent();
        this.Loaded += MainWindow_Loaded;
        this.StateChanged += (_, _) => {
            MaximizeIconPath.Data = (Geometry)FindResource(WindowState == WindowState.Maximized ? "RestoreIcon" : "MaximizeIcon");
        };
        this.MouseDown += (_, e) => {
            // Only DragMove if not clicking near window controls (top-right 140px)
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) {
                var pos = e.GetPosition(this);
                if (pos.Y > 36 || pos.X < ActualWidth - 150)
                    DragMove();
            }
        };
        _controller = ((App)System.Windows.Application.Current).Controller;
        
        LogListBox.ItemsSource = _logEntries;
        
        _controller.StatusChanged += OnStatusChanged;
        AppLog.OnLogWritten += OnLogWritten;
        _controller.ErrorOccurred += OnError;
        
        LoadConfigToUI();
    }
    
    private void MainWindow_Loaded(object sender, RoutedEventArgs e) {
        this.Opacity = 0;
        var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)) {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        this.BeginAnimation(UIElement.OpacityProperty, anim);
        LoadNTEBackground();
    }

    void LoadNTEBackground() {
        var bgPath = Path.Combine(AppContext.BaseDirectory, "background", "background.png");
        if (!File.Exists(bgPath))
            bgPath = Path.Combine(Environment.CurrentDirectory, "background", "background.png");
        if (File.Exists(bgPath)) {
            var img = new System.Windows.Media.Imaging.BitmapImage();
            img.BeginInit();
            img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(bgPath);
            img.EndInit();
            BackgroundImage.Source = img;
        }
    }
    
        void LoadConfigToUI() {
        var config = Config.Load();
        GamePathText.Text = config.GameInstallDir;
        UpdateCardState(TeleportCard, TeleportMainLabel, TeleportStateText, TeleportStatus, config.Options.AutoTeleport);
        UpdateCardState(PickupCard, PickupMainLabel, PickupStateText, PickupStatus, false, configured: false);
        UpdateCardState(SkipCard, SkipMainLabel, SkipStateText, SkipStatus, config.Options.AutoSkip);
        UpdateCardState(DismissCard, DismissMainLabel, DismissStateText, DismissStatus, config.Options.AutoDismiss);
        UpdateCardState(CloseCard, CloseMainLabel, CloseStateText, CloseStatus, config.Options.AutoClose);
        UpdateCardState(ClickCard, ClickMainLabel, ClickStateText, ClickStatus, false, configured: false);
    }
    
    void UpdateCardState(System.Windows.Controls.Button card, TextBlock mainLabel, TextBlock stateText, Border statusBar, bool isOn, bool configured = true) {
        stateText.Text = isOn ? "ON" : "OFF";
        
        var accent = ((System.Windows.Media.SolidColorBrush)FindResource("AccentColor")).Color;
        var error = ((System.Windows.Media.SolidColorBrush)FindResource("ErrorBrush")).Color;
        var cardBorder = ((System.Windows.Media.SolidColorBrush)FindResource("CardBorderColor")).Color;
        var inactive = ((System.Windows.Media.SolidColorBrush)FindResource("InactiveBrush")).Color;
        
        System.Windows.Media.Color targetMain, targetState, targetBorder;
        if (isOn) {
            targetMain = accent;
            targetState = accent;
            targetBorder = accent;
        } else if (configured) {
            targetMain = error;
            targetState = error;
            targetBorder = error;
        } else {
            targetMain = inactive;
            targetState = inactive;
            targetBorder = inactive;
        }
        
        if (mainLabel.Tag == null) {
            mainLabel.Tag = mainLabel.Foreground;
            mainLabel.Foreground = new SolidColorBrush(targetMain);
            stateText.Foreground = new SolidColorBrush(targetState);
            statusBar.Background = new SolidColorBrush(isOn ? accent : System.Windows.Media.Colors.Transparent);
            card.BorderBrush = new SolidColorBrush(targetBorder);
        } else {
            var curMain = ((System.Windows.Media.SolidColorBrush)mainLabel.Foreground).Color;
            mainLabel.Foreground = new SolidColorBrush(curMain);
            ((System.Windows.Media.SolidColorBrush)mainLabel.Foreground).BeginAnimation(
                SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(targetMain, TimeSpan.FromMilliseconds(200)));
            
            var curState = ((System.Windows.Media.SolidColorBrush)stateText.Foreground).Color;
            stateText.Foreground = new SolidColorBrush(curState);
            ((System.Windows.Media.SolidColorBrush)stateText.Foreground).BeginAnimation(
                SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(targetState, TimeSpan.FromMilliseconds(200)));
            
            var curStatus = ((System.Windows.Media.SolidColorBrush)statusBar.Background).Color;
            statusBar.Background = new SolidColorBrush(curStatus);
            ((System.Windows.Media.SolidColorBrush)statusBar.Background).BeginAnimation(
                SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(isOn ? accent : System.Windows.Media.Colors.Transparent, TimeSpan.FromMilliseconds(200)));
            
            var curBorder = ((System.Windows.Media.SolidColorBrush)card.BorderBrush).Color;
            card.BorderBrush = new SolidColorBrush(curBorder);
            ((System.Windows.Media.SolidColorBrush)card.BorderBrush).BeginAnimation(
                SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(targetBorder, TimeSpan.FromMilliseconds(200)));
        }
        
        if (isOn) {
            var pulseOpacity = new System.Windows.Media.Animation.DoubleAnimation {
                From = 1.0, To = 0.4,
                Duration = TimeSpan.FromMilliseconds(600),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            statusBar.BeginAnimation(UIElement.OpacityProperty, pulseOpacity);
        } else {
            statusBar.BeginAnimation(UIElement.OpacityProperty, null);
            statusBar.Opacity = 1.0;
        }
    }
    
    void StartButton_Click(object sender, RoutedEventArgs e) {
        if (_controller.IsRunning) {
            var ctrl = _controller;
            System.Threading.Tasks.Task.Run(() => ctrl.Stop());
        } else {
            var config = Config.Load();
            _controller.Start(config);
        }
    }
    
    void OnStatusChanged(EngineStatus status) {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Dispatcher.BeginInvoke(() => {
            switch (status) {
                case EngineStatus.Running:
                    StartButton.Content = "■ 停止";
                    StartButton.Background = (SolidColorBrush)FindResource("ErrorBrush");
                    var runningBrush = new SolidColorBrush(((System.Windows.Media.SolidColorBrush)FindResource("RunningColor")).Color);
                    StatusIndicator.Fill = runningBrush;
                    var pulse = new System.Windows.Media.Animation.ColorAnimation {
                        To = System.Windows.Media.Color.FromArgb(100, 16, 185, 129),
                        Duration = TimeSpan.FromMilliseconds(800),
                        AutoReverse = true,
                        RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
                    };
                    runningBrush.BeginAnimation(SolidColorBrush.ColorProperty, pulse);
                    StatusText.Text = "运行中";
                    _startTime = DateTime.Now;
                    StartRuntimeTimer();
                    UpdateProcessText();
                    break;
                case EngineStatus.Starting:
                    StartButton.Content = "启动中...";
                    StartButton.Background = (SolidColorBrush)FindResource("SecondaryForegroundBrush");
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("SecondaryForegroundBrush");
                    StatusText.Text = "启动中...";
                    break;
                case EngineStatus.Stopping:
                    StartButton.Content = "停止中...";
                    StartButton.Background = (SolidColorBrush)FindResource("SecondaryForegroundBrush");
                    StartButton.IsEnabled = false;
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("SecondaryForegroundBrush");
                    StatusText.Text = "停止中...";
                    break;
                case EngineStatus.Stopped:
                    StartButton.Content = "▶ 启动";
                    StartButton.Background = (SolidColorBrush)FindResource("AccentBrush");
                    StartButton.IsEnabled = true;
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("InactiveBrush");
                    StatusText.Text = "已停止";
                    StopRuntimeTimer();
                    GameProcessText.Text = "-";
                    RuntimeText.Text = "00:00:00";
                    break;
                case EngineStatus.Error:
                    StartButton.Content = "▶ 启动";
                    StartButton.Background = (SolidColorBrush)FindResource("AccentBrush");
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("ErrorBrush");
                    StatusText.Text = "错误";
                    StopRuntimeTimer();
                    GameProcessText.Text = "-";
                    RuntimeText.Text = "00:00:00";
                    break;
            }
        });
    }
    
    void UpdateProcessText() {
        var processes = Process.GetProcessesByName(StartGame.ProcessName);
        var found = processes.Length > 0;
        var pid = found ? processes[0].Id : 0;
        foreach (var p in processes) p.Dispose();
        if (found) {
            GameProcessText.Text = $"HTGame (PID: {pid})";
        } else {
            GameProcessText.Text = "等待游戏启动...";
        }
    }
    
    void StartRuntimeTimer() {
        StopRuntimeTimer();
        _runtimeTimer = new System.Timers.Timer(1000);
        _runtimeTimer.Elapsed += (s, e) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;
            Dispatcher.BeginInvoke(() => {
                var elapsed = DateTime.Now - _startTime;
                RuntimeText.Text = $"{elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
                
                if (GameProcessText.Text == "等待游戏启动...") {
                    UpdateProcessText();
                }
            });
        };
        _runtimeTimer.Start();
    }
    
    void StopRuntimeTimer() {
        if (_runtimeTimer != null) {
            _runtimeTimer.Stop();
            _runtimeTimer.Dispose();
            _runtimeTimer = null;
        }
    }
    
    void OnLogWritten(string line) {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Dispatcher.BeginInvoke(() => {
            _logEntries.Add(line);
            if (_logEntries.Count > 500)
                _logEntries.RemoveAt(0);
            if (_logAutoScroll && LogListBox.Items.Count > 0) {
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
            }
        });
    }
    
    void OnError(string message) {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Dispatcher.BeginInvoke(() => {
            System.Windows.MessageBox.Show(this, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }
    
    void BrowseButton_Click(object sender, RoutedEventArgs e) {
        using var dialog = new FolderBrowserDialog {
            Description = "选择《异环》(Neverness To Everness) 游戏安装目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        
        if (!string.IsNullOrEmpty(GamePathText.Text) && System.IO.Directory.Exists(GamePathText.Text)) {
            dialog.SelectedPath = GamePathText.Text;
        }
        
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
            var config = Config.Load();
            config.GameInstallDir = dialog.SelectedPath;
            _controller.UpdateConfig(config);
            GamePathText.Text = dialog.SelectedPath;
        }
    }
    
    void ToggleTask(System.Windows.Controls.Button card, TextBlock mainLabel, TextBlock stateText, Border statusBar, Func<Options, bool> getter, Action<Options, bool> setter) {
        var config = Config.Load();
        bool newValue = !getter(config.Options);
        setter(config.Options, newValue);
        _controller.UpdateConfig(config);
        UpdateCardState(card, mainLabel, stateText, statusBar, newValue);
    }
    
    void TeleportCard_Click(object sender, RoutedEventArgs e) => ToggleTask(TeleportCard, TeleportMainLabel, TeleportStateText, TeleportStatus, o => o.AutoTeleport, (o, v) => o.AutoTeleport = v);
    void PickupCard_Click(object sender, RoutedEventArgs e) {
        PickupStateText.Text = "未配置";
        PickupStateText.Foreground = (SolidColorBrush)FindResource("InactiveBrush");
        var t = new System.Timers.Timer(2000) { AutoReset = false };
        t.Elapsed += (_, _) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) { t.Dispose(); return; }
            Dispatcher.BeginInvoke(() => { t.Dispose(); LoadConfigToUI(); });
        };
        t.Start();
    }
    void SkipCard_Click(object sender, RoutedEventArgs e) => ToggleTask(SkipCard, SkipMainLabel, SkipStateText, SkipStatus, o => o.AutoSkip, (o, v) => o.AutoSkip = v);
    void DismissCard_Click(object sender, RoutedEventArgs e) => ToggleTask(DismissCard, DismissMainLabel, DismissStateText, DismissStatus, o => o.AutoDismiss, (o, v) => o.AutoDismiss = v);
    void CloseCard_Click(object sender, RoutedEventArgs e) => ToggleTask(CloseCard, CloseMainLabel, CloseStateText, CloseStatus, o => o.AutoClose, (o, v) => o.AutoClose = v);
    void ClickCard_Click(object sender, RoutedEventArgs e) {
        ClickStateText.Text = "未配置";
        ClickStateText.Foreground = (SolidColorBrush)FindResource("InactiveBrush");
        var t = new System.Timers.Timer(2000) { AutoReset = false };
        t.Elapsed += (_, _) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) { t.Dispose(); return; }
            Dispatcher.BeginInvoke(() => { t.Dispose(); LoadConfigToUI(); });
        };
        t.Start();
    }
    
    void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void CloseButton_Click(object sender, RoutedEventArgs e) =>
        ((App)System.Windows.Application.Current).ExitApplication();

    void Window_Closed(object? sender, EventArgs e) {
        StopRuntimeTimer();
        _controller.StatusChanged -= OnStatusChanged;
        AppLog.OnLogWritten -= OnLogWritten;
        _controller.ErrorOccurred -= OnError;
        System.Windows.Application.Current.Shutdown();
    }
}
