using System.Collections.Generic;

namespace XtrXmlTranslator.Core.Srx;

public sealed class SrxDocument
{
    public bool Cascade { get; init; } = true;
    public List<SrxLanguageRule> LanguageRules { get; } = new();
    public List<SrxLanguageMap> LanguageMaps { get; } = new();
}

public sealed class SrxLanguageRule
{
    public string Name { get; init; } = "";
    public List<SrxRule> Rules { get; } = new();
}

public sealed class SrxRule
{
    public string? Before { get; init; }
    public string? After { get; init; }
    public bool IsBreak { get; init; } = true; // break="no" -> false
}

public sealed class SrxLanguageMap
{
    public string LanguagePattern { get; init; } = "";   // regex for BCP47 tag
    public string LanguageRuleName { get; init; } = "";
}

