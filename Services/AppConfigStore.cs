using System.Text.Json;
using PostgresRedisManager.Models;

namespace PostgresRedisManager.Services;

internal static class AppConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            return AppConfig.CreateDefault();

        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions)
            ?? AppConfig.CreateDefault();
    }

    public static void Save(string path, AppConfig config)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }
}
