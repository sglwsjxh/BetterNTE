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
                var defaultConfig = new GameConfig();
                File.WriteAllText(_path, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        try {
            var json = File.ReadAllText(_path);
            var config = JsonSerializer.Deserialize<GameConfig>(json);
            if (config != null) {
                config.Options ??= new Options();
                config.GameInstallDir ??= "";
                return config;
            }
        } catch (Exception ex) {
            AppLog.Write($"Config load error: {ex.Message}");
        }

        AppLog.Write("Config load failed — returning default config");
        return new GameConfig();
    }

    public static void Save(GameConfig config) {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}
