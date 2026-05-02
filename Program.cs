using System.Diagnostics;

if (!Environment.IsPrivilegedProcess) {
    var psi = new ProcessStartInfo(Environment.ProcessPath!) { Verb = "runas", UseShellExecute = true };
    Process.Start(psi);
    return;
}

var config = Config.Load();
var app = new Application(config);

System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
System.Windows.Forms.Application.EnableVisualStyles();
System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

var menu = new ContextMenuStrip();
menu.Items.Add("退出", null, (s, e) => {
    app.Stop();
    System.Windows.Forms.Application.Exit();
});

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

app.OnExit += () => {
    notifyIcon.Visible = false;
    System.Windows.Forms.Application.Exit();
};

_ = Task.Run(() => app.Run());

System.Windows.Forms.Application.Run();