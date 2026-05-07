using System.Text.Json;
using System.Text.Json.Serialization;

class Options {
    [JsonPropertyName("autoteleport")]
    public bool AutoTeleport { get; set; }
    [JsonPropertyName("autopickup")]
    public bool AutoPickup { get; set; }
    [JsonPropertyName("autoskip")]
    public bool AutoSkip { get; set; }
    [JsonPropertyName("autodismiss")]
    public bool AutoDismiss { get; set; }
    [JsonPropertyName("autoclose")]
    public bool AutoClose { get; set; }
    [JsonPropertyName("autoclick")]
    public bool AutoClick { get; set; }
}

class GameConfig {
    [JsonPropertyName("game_install_dir")]
    public string GameInstallDir { get; set; } = "";
    [JsonPropertyName("options")]
    public Options Options { get; set; } = new();
}

static class Config {
    static readonly string _path = Path.Combine(AppContext.BaseDirectory, "config.json");

    public static GameConfig Load() {
        if (!File.Exists(_path)) {
            var examplePath = Path.Combine(AppContext.BaseDirectory, "config.json.example");
            if (File.Exists(examplePath)) {
                File.Copy(examplePath, _path);
            } else {
                // 如果不仅没有 config.json 连 example 都没有，就生成一个默认的
                var defaultConfig = new GameConfig();
                File.WriteAllText(_path, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
            }
            // 提示用户配置
            System.Windows.Forms.MessageBox.Show("未找到 config.json，已自动为您生成默认配置，请配置游戏路径后再运行！", "提示", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            System.Environment.Exit(0);
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<GameConfig>(json)!;
    }
}