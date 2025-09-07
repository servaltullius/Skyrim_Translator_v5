using System;

namespace XtrXmlTranslator.App.Services;

public interface ISecretsProvider
{
    string? Get(string name);
}

public sealed class EnvSecretsProvider : ISecretsProvider
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}

public sealed class FileSecretsProvider : ISecretsProvider
{
    public string? Get(string name)
    {
        var s = SecretsFileStore.Load();
        return name == "GEMINI_API_KEY" ? s.GeminiApiKey : null;
    }
}

public sealed class CombinedSecretsProvider : ISecretsProvider
{
    private readonly FileSecretsProvider _file = new();
    private readonly EnvSecretsProvider _env = new();
    public string? Get(string name)
    {
        var fromEnv = _env.Get(name);
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        return _file.Get(name);
    }
}
