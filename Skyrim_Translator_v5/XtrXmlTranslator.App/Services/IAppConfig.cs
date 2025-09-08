using System;

namespace XtrXmlTranslator.App.Services;

public interface IAppConfig
{
    string? Get(string key);
}

public sealed class EnvConfig : IAppConfig
{
    public string? Get(string key) => Environment.GetEnvironmentVariable(key);
}
