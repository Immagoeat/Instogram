using System;
using System.IO;
using System.Text.Json;

namespace InstogramApp.Services;

public record ServerSettings(string ServerUrl, string Token, string Username, string DisplayName, string AccentColor);

public static class ServerConfig
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "server.json");

    public static ServerSettings? Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return null;
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<ServerSettings>(json);
        }
        catch { return null; }
    }

    public static void Save(ServerSettings cfg)
    {
        try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg)); }
        catch { }
    }

    public static void Clear()
    {
        try { if (File.Exists(ConfigPath)) File.Delete(ConfigPath); }
        catch { }
    }
}
