using System.Collections.Generic;

namespace XtrXmlTranslator.Core.Glossary;

public sealed class GlossaryBoundaryConfig
{
    public HashSet<char> ExtraWordChars { get; }
    public GlossaryBoundaryConfig(IEnumerable<char>? extra = null)
    {
        ExtraWordChars = extra is null ? new HashSet<char>() : new HashSet<char>(extra);
    }
    public static GlossaryBoundaryConfig Default { get; } = new GlossaryBoundaryConfig();
}

public static class GlossaryBoundary
{
    private static GlossaryBoundaryConfig _config = GlossaryBoundaryConfig.Default;
    public static GlossaryBoundaryConfig Current => _config;
    public static void SetConfig(GlossaryBoundaryConfig cfg) => _config = cfg ?? GlossaryBoundaryConfig.Default;

    public static bool IsBoundary(char c)
        => !(char.IsLetterOrDigit(c) || _config.ExtraWordChars.Contains(c));

    public static bool CheckWordBoundary(string s, int start, int len)
    {
        bool left = start == 0 || IsBoundary(s[start - 1]);
        int end = start + len;
        bool right = end >= s.Length || IsBoundary(s[end]);
        return left && right;
    }
}
