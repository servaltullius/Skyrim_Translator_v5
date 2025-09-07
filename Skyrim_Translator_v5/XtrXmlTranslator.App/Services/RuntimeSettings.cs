namespace XtrXmlTranslator.App.Services;

public interface IRuntimeSettings
{
    bool SkipTagHeavyEnabled { get; set; }
    int TagHeavyMinText { get; set; }
}

public sealed class RuntimeSettings : IRuntimeSettings
{
    public bool SkipTagHeavyEnabled { get; set; }
    public int TagHeavyMinText { get; set; }
}
