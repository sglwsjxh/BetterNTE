using System.Windows;
using Forms = System.Windows.Forms;

public partial class FirstRunConfigWindow : Window {
    string _validGameInstallDir = "";

    public FirstRunConfigWindow() {
        InitializeComponent();
    }

    void BrowseButton_Click(object sender, RoutedEventArgs e) {
        using var dialog = new Forms.FolderBrowserDialog {
            Description = "请选择异环游戏安装目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (Directory.Exists(PathTextBox.Text))
            dialog.SelectedPath = PathTextBox.Text;

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
            PathTextBox.Text = dialog.SelectedPath;
    }

    void PathTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) {
        ValidateGameDirectory(PathTextBox.Text.Trim());
    }

    void ConfirmButton_Click(object sender, RoutedEventArgs e) {
        var config = Config.Load();
        config.GameInstallDir = _validGameInstallDir;
        Config.Save(config);
        DialogResult = true;
    }

    void CancelButton_Click(object sender, RoutedEventArgs e) {
        DialogResult = false;
    }

    void ValidateGameDirectory(string selectedPath) {
        _validGameInstallDir = "";
        ConfirmButton.IsEnabled = false;

        if (string.IsNullOrWhiteSpace(selectedPath)) {
            ValidationTextBlock.Text = "请选择包含 NTEGame.exe 的游戏目录";
            ValidationTextBlock.Foreground = System.Windows.Media.Brushes.IndianRed;
            return;
        }

        if (!Directory.Exists(selectedPath)) {
            ValidationTextBlock.Text = "目录不存在，请重新选择";
            ValidationTextBlock.Foreground = System.Windows.Media.Brushes.IndianRed;
            return;
        }

        var gameExe = FindGameExecutable(selectedPath);
        if (gameExe == null) {
            ValidationTextBlock.Text = "未找到 NTELauncher\\NTEGame.exe —— 请选择包含该文件的目录";
            ValidationTextBlock.Foreground = System.Windows.Media.Brushes.IndianRed;
            return;
        }

        _validGameInstallDir = selectedPath;
        ConfirmButton.IsEnabled = true;
        ValidationTextBlock.Text = $"已找到 {Path.GetFileName(gameExe)}";
        ValidationTextBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
    }

    static string? FindGameExecutable(string selectedPath) {
        var exePath = Path.Combine(selectedPath, "NTELauncher", "NTEGame.exe");
        return File.Exists(exePath) ? exePath : null;
    }
}
