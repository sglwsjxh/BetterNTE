using System.Diagnostics;

System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
System.Windows.Forms.Application.EnableVisualStyles();
System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

if (!Environment.IsPrivilegedProcess) {
    var psi = new ProcessStartInfo(Environment.ProcessPath!) {
        Verb = "runas",
        UseShellExecute = true,
        WorkingDirectory = Environment.CurrentDirectory
    };
    Process.Start(psi);
    return;
}

AppLog.Initialize();
AppLog.Write($"Application started. CurrentDirectory={Environment.CurrentDirectory}, BaseDirectory={AppContext.BaseDirectory}");

ImageMatch.InitializeScreenScale();

var config = Config.Load();
AppLog.Write($"Config loaded. AutoTeleport={config.Options.AutoTeleport}, GameInstallDir={config.GameInstallDir}");
var app = new Application(config);
var uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

var menu = new ContextMenuStrip();
menu.Items.Add("退出", null, (s, e) => {
    AppLog.Write("Exit requested from tray menu");
    app.Stop();
    System.Windows.Forms.Application.Exit();
});

app.OnGameExited += () => {
    AppLog.Write("Application exiting because game process exited");
    uiContext.Post(_ => System.Windows.Forms.Application.Exit(), null);
};

var iconPath = Path.Combine(AppContext.BaseDirectory, "logo", "logo.ico");
var trayIconAsset = SystemIcons.Application;
if (File.Exists(iconPath)) {
    using var bmp = new Bitmap(iconPath);
    trayIconAsset = Icon.FromHandle(bmp.GetHicon());
}

using var notifyIcon = new NotifyIcon {
    Icon = trayIconAsset,
    Text = "BetterNTE",
    ContextMenuStrip = menu,
    Visible = true
};

_ = Task.Run(() => app.Run());

System.Windows.Forms.Application.Run();