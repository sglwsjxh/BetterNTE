using System.Diagnostics;
using System.Threading;

bool createdNew;
using var mutex = new Mutex(true, "Global\\BetterNTE_SingleInstance_Mutex", out createdNew);
if (!createdNew) {
    System.Windows.Forms.MessageBox.Show("BetterNTE 已经在运行中！", "提示", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
    return;
}

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

ToolStripMenuItem AddToggle(string label, bool initialChecked, Action<bool> setter) {
    var item = new ToolStripMenuItem(label) { Checked = initialChecked };
    item.Click += (s, e) => {
        item.Checked = !item.Checked;
        setter(item.Checked);
        Config.Save(config);
        AppLog.Write($"Config toggle: {label}={item.Checked}");
    };
    menu.Items.Add(item);
    return item;
}

AddToggle("自动传送", config.Options.AutoTeleport, v => config.Options.AutoTeleport = v);
AddToggle("自动拾取", config.Options.AutoPickup, v => config.Options.AutoPickup = v);
AddToggle("自动跳过剧情", config.Options.AutoSkip, v => config.Options.AutoSkip = v);
AddToggle("自动驱散", config.Options.AutoDismiss, v => config.Options.AutoDismiss = v);
AddToggle("自动关闭弹窗", config.Options.AutoClose, v => config.Options.AutoClose = v);
AddToggle("自动点击", config.Options.AutoClick, v => config.Options.AutoClick = v);

menu.Items.Add(new ToolStripSeparator());

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