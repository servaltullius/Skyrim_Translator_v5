using System.IO;
using System.Text.Json;

namespace XtrXmlTranslator.App.Services;

public sealed class SecretsPayload
{
    public string? GeminiApiKey { get; set; }
}

public static class SecretsFileStore
{
    public static SecretsPayload Load()
    {
        try
        {
            Directory.CreateDirectory(AppConfigPaths.ConfigDir);
            if (!File.Exists(AppConfigPaths.SecretsPath)) return new SecretsPayload();
            var json = File.ReadAllText(AppConfigPaths.SecretsPath);
            return JsonSerializer.Deserialize<SecretsPayload>(json) ?? new SecretsPayload();
        }
        catch
        {
            return new SecretsPayload();
        }
    }

    public static void Save(SecretsPayload payload)
    {
        Directory.CreateDirectory(AppConfigPaths.ConfigDir);
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppConfigPaths.SecretsPath, json);
    }
}
