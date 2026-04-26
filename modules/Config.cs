using System.Text.Json;
using System.Text.Json.Serialization;

class Options {
    [JsonPropertyName("autoteleport")]
    public bool AutoTeleport { get; set; }
    [JsonPropertyName("autopickup")]
    public bool AutoPickup { get; set; }
    [JsonPropertyName("autoskip")]
    public bool AutoSkip { get; set; }
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
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<GameConfig>(json)!;
    }
}