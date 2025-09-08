namespace XtrXmlTranslator.Core.Glossary;

public enum GlossaryType { ENFORCE, PREFER, PROTECT }

public sealed class GlossaryEntry
{
    public string Source { get; set; } = string.Empty;
    public string? Target { get; set; } = string.Empty; // PROTECT는 Target 불필요
    public GlossaryType Type { get; set; } = GlossaryType.ENFORCE;
    public bool CaseSensitive { get; set; } = false;
    public string? Notes { get; set; }
}

public sealed class GlossaryStore
{
    public List<GlossaryEntry> Entries { get; } = new();
}
