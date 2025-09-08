using System.Text.RegularExpressions;
using XtrXmlTranslator.Core.Tokenizing;

namespace XtrXmlTranslator.Core.Validation;

public static partial class CkTagClassifier
{
    // Bethesda CK Text Replacement 태그(원자 태그로 취급)
    // <Alias=Name>, <Alias.Sub=Name>, <Alias.SubtagCap=Name>, <Global=G>, <Global.Sub=G>, <BaseName>, <Relationship.A=B>
    [GeneratedRegex(@"^<(?:Alias(?:\.[A-Za-z]+)?(?:\s*Cap)?=[^>]+|Global(?:\.[A-Za-z]+)?=[^>]+|BaseName|Relationship\.[^=]+=.+)>$", RegexOptions.CultureInvariant)]
    private static partial Regex CkAtomicRx();

    // 일반 XML-like 오픈/클로즈/셀프클로징 태그(단순 검사)
    [GeneratedRegex(@"^</?([A-Za-z][A-Za-z0-9:_\-\.]*)(?:\s+[^>]*)?/? >$", RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex XmlLikeRx();

    public static bool IsCkAtomic(InlineToken tok)
        => tok.Type == InlineTokenType.XmlLikeTag && CkAtomicRx().IsMatch(tok.Value);

    public static (bool isTag, string? name, bool isOpen, bool isClose, bool isSelf) TryGetXmlLike(InlineToken tok)
    {
        if (tok.Type != InlineTokenType.XmlLikeTag) return (false, null, false, false, false);
        var m = XmlLikeRx().Match(tok.Value);
        if (!m.Success) return (false, null, false, false, false);
        var val = tok.Value;
        bool isClose = val.StartsWith("</");
        bool isSelf = val.EndsWith("/>");
        bool isOpen = !isClose && !isSelf && val.StartsWith("<");
        return (true, m.Groups[1].Value, isOpen, isClose, isSelf);
    }
}
