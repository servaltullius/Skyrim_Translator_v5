using System;
using System.IO;
using System.Text.Json;

namespace XtrXmlTranslator.App.Services;

public sealed class UxSettings
{
    public double FullTextFontSize { get; set; } = 14;
    public double FullTextWidth { get; set; } = 1200;
    public double FullTextHeight { get; set; } = 850;
}

public static class UxSettingsService
{
    private static string Path => System.IO.Path.Combine(AppConfigPaths.ConfigDir, "ux.json");

    public static UxSettings Load()
    {
        try
        {
            Directory.CreateDirectory(AppConfigPaths.ConfigDir);
            if (!File.Exists(Path)) return new UxSettings();
            var json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<UxSettings>(json) ?? new UxSettings();
        }
        catch { return new UxSettings(); }
    }

    public static void Save(UxSettings s)
    {
        Directory.CreateDirectory(AppConfigPaths.ConfigDir);
        var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path, json);
    }
}

