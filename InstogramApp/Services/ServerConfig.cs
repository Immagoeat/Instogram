using System;
using System.IO;
using System.Text.Json;

namespace InstogramApp.Services;

public record ServerSettings(string ServerUrl, string Token, string Username, string DisplayName, string AccentColor);

public record DeviceSettings(int MicDeviceIndex, int SpeakerDeviceIndex, string CameraDevicePath);

public static class DeviceConfig
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "devices.json");

    public static DeviceSettings Load() =>
        JsonSerializer.Deserialize<DeviceSettings>(TryRead()) ?? new DeviceSettings(-1, -1, "");

    public static void Save(DeviceSettings cfg)
    {
        try { File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg)); }
        catch { }
    }

    private static string TryRead()
    {
        try { return File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : "{}"; }
        catch { return "{}"; }
    }
}

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
