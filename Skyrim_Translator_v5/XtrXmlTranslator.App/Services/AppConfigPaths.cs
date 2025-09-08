using System;
using System.IO;

namespace XtrXmlTranslator.App.Services;

public static class AppConfigPaths
{
    public static string ConfigDir => Path.Combine(AppContext.BaseDirectory, "Config");
    public static string PromptPath => Path.Combine(ConfigDir, "prompt.json");
    public static string GlossaryPath => Path.Combine(ConfigDir, "glossary.csv");
    public static string SecretsPath => Path.Combine(ConfigDir, "secrets.json");
}
