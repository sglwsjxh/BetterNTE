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
        _ = LoadZZZArtworkAsync();
    }

    async System.Threading.Tasks.Task LoadZZZArtworkAsync() {
        string[] urls = [
            "https://zenless.hoyoverse.com/upload/content/2026/05/zzz_promo_1920.jpg",
            "https://fastcdn.hoyoverse.com/static/2026/05/zzz_keyvisual.jpg",
        ];
        foreach (var url in urls) {
            try {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var bytes = await http.GetByteArrayAsync(url);
                var img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.StreamSource = new System.IO.MemoryStream(bytes);
                img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.EndInit();
                ZZZArtworkImage.Source = img;
                return;
            } catch {
            }
        }
    }
    
    void LoadConfigToUI() {
        var config = Config.Load();
        GamePathText.Text = config.GameInstallDir;
        UpdateCardState(TeleportCard, TeleportStateText, TeleportStatus, config.Options.AutoTeleport);
        // Pickup and Click not yet implemented — always show OFF
        UpdateCardState(PickupCard, PickupStateText, PickupStatus, false);
        UpdateCardState(SkipCard, SkipStateText, SkipStatus, config.Options.AutoSkip);
        UpdateCardState(DismissCard, DismissStateText, DismissStatus, config.Options.AutoDismiss);
        UpdateCardState(CloseCard, CloseStateText, CloseStatus, config.Options.AutoClose);
        UpdateCardState(ClickCard, ClickStateText, ClickStatus, false);
    }
    
    void UpdateCardState(System.Windows.Controls.Button card, TextBlock textBlock, Border border, bool isOn) {
        textBlock.Text = isOn ? "ON" : "OFF";
        
        var accent = ((System.Windows.Media.SolidColorBrush)FindResource("AccentColor")).Color;
        var secondary = ((System.Windows.Media.SolidColorBrush)FindResource("SecondaryTextColor")).Color;
        var cardBorder = ((System.Windows.Media.SolidColorBrush)FindResource("CardBorderColor")).Color;
        
        var textBrush = new SolidColorBrush(isOn ? secondary : accent);
        textBlock.Foreground = textBrush;
        textBrush.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(isOn ? accent : secondary, TimeSpan.FromMilliseconds(200)));
        
        var borderBrush = new SolidColorBrush(isOn ? System.Windows.Media.Colors.Transparent : accent);
        border.Background = borderBrush;
        borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(isOn ? accent : System.Windows.Media.Colors.Transparent, TimeSpan.FromMilliseconds(200)));
        
        var cardBrush = new SolidColorBrush(isOn ? cardBorder : accent);
        card.BorderBrush = cardBrush;
        cardBrush.BeginAnimation(SolidColorBrush.ColorProperty, new System.Windows.Media.Animation.ColorAnimation(isOn ? accent : cardBorder, TimeSpan.FromMilliseconds(200)));
        
        if (isOn) {
            var pulseOpacity = new System.Windows.Media.Animation.DoubleAnimation {
                From = 1.0, To = 0.4,
                Duration = TimeSpan.FromMilliseconds(600),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            border.BeginAnimation(UIElement.OpacityProperty, pulseOpacity);
        } else {
            border.BeginAnimation(UIElement.OpacityProperty, null);
            border.Opacity = 1.0;
        }
    }
    
    void StartButton_Click(object sender, RoutedEventArgs e) {
        if (_controller.IsRunning) {
            // Offload Stop to background thread to avoid blocking UI up to 3s
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
                    StartButton.Content = "▶ Start";
                    StartButton.Background = (SolidColorBrush)FindResource("AccentBrush");
                    StartButton.IsEnabled = true;
                    StatusIndicator.Fill = (SolidColorBrush)FindResource("InactiveBrush");
                    StatusText.Text = "已停止";
                    StopRuntimeTimer();
                    GameProcessText.Text = "-";
                    RuntimeText.Text = "00:00:00";
                    break;
                case EngineStatus.Error:
                    StartButton.Content = "▶ Start";
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
    
    void ToggleTask(System.Windows.Controls.Button card, TextBlock textBlock, Border border, Func<Options, bool> getter, Action<Options, bool> setter) {
        var config = Config.Load();
        bool newValue = !getter(config.Options);
        setter(config.Options, newValue);
        _controller.UpdateConfig(config);
        UpdateCardState(card, textBlock, border, newValue);
    }
    
    void TeleportCard_Click(object sender, RoutedEventArgs e) => ToggleTask(TeleportCard, TeleportStateText, TeleportStatus, o => o.AutoTeleport, (o, v) => o.AutoTeleport = v);
    void PickupCard_Click(object sender, RoutedEventArgs e) {
        // AutoPickup not yet implemented — show indicator without changing config
        PickupStateText.Text = "即将开放";
        PickupStateText.Foreground = (SolidColorBrush)FindResource("SecondaryForegroundBrush");
        var t = new System.Timers.Timer(2000) { AutoReset = false };
        t.Elapsed += (_, _) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) { t.Dispose(); return; }
            Dispatcher.BeginInvoke(() => { t.Dispose(); LoadConfigToUI(); });
        };
        t.Start();
    }
    void SkipCard_Click(object sender, RoutedEventArgs e) => ToggleTask(SkipCard, SkipStateText, SkipStatus, o => o.AutoSkip, (o, v) => o.AutoSkip = v);
    void DismissCard_Click(object sender, RoutedEventArgs e) => ToggleTask(DismissCard, DismissStateText, DismissStatus, o => o.AutoDismiss, (o, v) => o.AutoDismiss = v);
    void CloseCard_Click(object sender, RoutedEventArgs e) => ToggleTask(CloseCard, CloseStateText, CloseStatus, o => o.AutoClose, (o, v) => o.AutoClose = v);
    void ClickCard_Click(object sender, RoutedEventArgs e) {
        // AutoClick not yet implemented — show indicator without changing config
        ClickStateText.Text = "即将开放";
        ClickStateText.Foreground = (SolidColorBrush)FindResource("SecondaryForegroundBrush");
        var t = new System.Timers.Timer(2000) { AutoReset = false };
        t.Elapsed += (_, _) => {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) { t.Dispose(); return; }
            Dispatcher.BeginInvoke(() => { t.Dispose(); LoadConfigToUI(); });
        };
        t.Start();
    }
    
    void Window_Closed(object? sender, EventArgs e) {
        StopRuntimeTimer();
        _controller.StatusChanged -= OnStatusChanged;
        AppLog.OnLogWritten -= OnLogWritten;
        _controller.ErrorOccurred -= OnError;
        System.Windows.Application.Current.Shutdown();
    }
}
