using System;
using System.IO;
using System.Text.Json;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Prompt;

namespace XtrXmlTranslator.App.Services;

public interface IConfigService
{
    (PromptConfig Prompt, GlossaryStore Glossary) Load();
    void SavePrompt(PromptConfig prompt);
    void SaveGlossary(GlossaryStore glossary);
    GlossaryCompiled Compile(GlossaryStore glossary);
}

public sealed class ConfigService : IConfigService
{
    public (PromptConfig Prompt, GlossaryStore Glossary) Load()
    {
        Directory.CreateDirectory(AppConfigPaths.ConfigDir);
        var prompt = new PromptConfig();
        if (File.Exists(AppConfigPaths.PromptPath))
        {
            var json = File.ReadAllText(AppConfigPaths.PromptPath);
            var cfg = JsonSerializer.Deserialize<PromptConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cfg is not null) prompt = cfg;
        }
        var glossary = new GlossaryStore();
        if (File.Exists(AppConfigPaths.GlossaryPath))
        {
            var g = GlossaryIo.Load(AppConfigPaths.GlossaryPath);
            if (g is not null) glossary = g;
        }
        return (prompt, glossary);
    }

    public void SavePrompt(PromptConfig prompt)
    {
        Directory.CreateDirectory(AppConfigPaths.ConfigDir);
        var json = JsonSerializer.Serialize(prompt, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppConfigPaths.PromptPath, json);
    }

    public void SaveGlossary(GlossaryStore glossary)
    {
        Directory.CreateDirectory(AppConfigPaths.ConfigDir);
        GlossaryIo.Save(glossary, AppConfigPaths.GlossaryPath);
    }

    public GlossaryCompiled Compile(GlossaryStore glossary) => GlossaryCompiled.Build(glossary);
}
