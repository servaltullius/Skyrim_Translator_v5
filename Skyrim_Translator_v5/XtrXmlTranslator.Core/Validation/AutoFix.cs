using System.Text.RegularExpressions;
using XtrXmlTranslator.Core.Tokenizing;

namespace XtrXmlTranslator.Core.Validation;

public static class AutoFix
{
    // 1) CK 원자 태그를 소스 순서대로 재배치(동일 개수 전제)
    public static string ReorderCkAtomicsLikeSource(string source, string target)
    {
        var rx = new Regex(@"<[^>]+>", RegexOptions.CultureInvariant);
        var srcCk = rx.Matches(source).Select(m => m.Value)
            .Where(v => CkTagClassifier.IsCkAtomic(new InlineToken(InlineTokenType.XmlLikeTag, v))).ToList();

        int ckIdx = 0;
        return rx.Replace(target, m =>
        {
            var val = m.Value;
            if (!CkTagClassifier.IsCkAtomic(new InlineToken(InlineTokenType.XmlLikeTag, val))) return val;
            if (ckIdx >= srcCk.Count) return val;
            return srcCk[ckIdx++];
        });
    }

    // 2) 잘못 줄어든 리터럴 % 복원: 단독 %를 %%로(printf 패턴은 보존)
    public static string RestorePercentLiterals(string source, string target)
    {
        int srcPct = Regex.Matches(source, "%%").Count;
        int tgtPct = Regex.Matches(target, "%%").Count;
        if (tgtPct >= srcPct) return target;

        var rxPrintf = new Regex(@"%(?!%)(?:\d+\$)?[-+ #0]*\d*(?:\.\d+)?[A-Za-z]", RegexOptions.CultureInvariant);
        int i = 0;
        while (i < target.Length && tgtPct < srcPct)
        {
            if (target[i] != '%') { i++; continue; }
            // printf 패턴이면 skip
            var rest = target.Substring(i);
            if (rxPrintf.IsMatch(rest)) { i++; continue; }
            // 이미 %%이면 skip
            if (i + 1 < target.Length && target[i + 1] == '%') { i += 2; continue; }
            // 단독 % -> %%
            target = target[..i] + "%%" + target[(i + 1)..];
            i += 2;
            tgtPct++;
        }
        return target;
    }
}
