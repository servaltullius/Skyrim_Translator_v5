using System.Text.RegularExpressions;
using XtrXmlTranslator.Core.Tokenizing;

namespace XtrXmlTranslator.Core.Validation;

public enum IssueKind { PlaceholderMissing, PlaceholderExtra, PlaceholderOrderMismatch, PlaceholderTypeMismatch, PlaceholderDetailMismatch, TagCountMismatch, TagOrderMismatch, TagNestingError, PercentLiteralBroken }
public sealed record ValidationIssue(IssueKind Kind, string Message, int? Index = null, string? Detail = null);

public static class Validator
{
    // printf: %(not %) [pos]? [flags]* [width]? [.precision]? [type]
    private static readonly Regex RxPercent = new(
        @"%(?!%)(?:\d+\$)?[-+ #0]*\d*(?:\.\d+)?[A-Za-z]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromSeconds(1));

    // {0}, {Name}, {0:N2}, {name:fmt}
    private static readonly Regex RxBrace = new(
        @"\{[0-9A-Za-z_]+(?:[^{}]*)\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromSeconds(1));

    // %{name}
    private static readonly Regex RxPercentBrace = new(
        @"%\{[0-9A-Za-z_]+\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromSeconds(1));

    // XML-like 태그 전체 매칭(간소화)
    private static readonly Regex RxXmlLike = new(
        @"<[^>]+>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeout: TimeSpan.FromSeconds(1));

    public static List<ValidationIssue> Validate(string source, string target)
    {
        var issues = new List<ValidationIssue>();

        // 1) 자리표시자 개수 일치
        CountEqual(RxPercent, "printf-format", source, target, issues, IssueKind.PlaceholderMissing, IssueKind.PlaceholderExtra);
        CountEqual(RxBrace, "brace", source, target, issues, IssueKind.PlaceholderMissing, IssueKind.PlaceholderExtra);
        CountEqual(RxPercentBrace, "percent-brace", source, target, issues, IssueKind.PlaceholderMissing, IssueKind.PlaceholderExtra);

        // 1-a) 순서/타입/세부 속성 비교(개수 같을 때만)
        try
        {
            ComparePrintfSequences(source, target, issues);
            CompareBraceSequences(source, target, issues);
            ComparePercentBraceSequences(source, target, issues);
        }
        catch (RegexMatchTimeoutException)
        {
            issues.Add(new ValidationIssue(IssueKind.PlaceholderDetailMismatch, "Regex timeout while validating placeholders"));
        }

        // 2) %% 리터럴 개수 동일성
        var pctSrc = Regex.Matches(source, "%%").Count;
        var pctTgt = Regex.Matches(target, "%%").Count;
        if (pctSrc != pctTgt)
            issues.Add(new ValidationIssue(IssueKind.PercentLiteralBroken, $"Literal %% count differs (src:{pctSrc}, tgt:{pctTgt})"));

        // 3) 태그: 개수
        var srcTags = RxXmlLike.Matches(source).Select(m => m.Value).ToList();
        var tgtTags = RxXmlLike.Matches(target).Select(m => m.Value).ToList();
        if (srcTags.Count != tgtTags.Count)
            issues.Add(new ValidationIssue(IssueKind.TagCountMismatch, $"XML-like tag count differs (src:{srcTags.Count}, tgt:{tgtTags.Count})"));

        // 3-a) CK 원자 태그: 순서/동일성
        var srcCk = srcTags.Where(v => CkTagClassifier.IsCkAtomic(new InlineToken(InlineTokenType.XmlLikeTag, v))).ToList();
        var tgtCk = tgtTags.Where(v => CkTagClassifier.IsCkAtomic(new InlineToken(InlineTokenType.XmlLikeTag, v))).ToList();
        if (!Enumerable.SequenceEqual(srcCk, tgtCk, StringComparer.Ordinal))
            issues.Add(new ValidationIssue(IssueKind.TagOrderMismatch, "CK atomic tags changed or reordered"));

        // 3-b) 일반 태그: 중첩/순서
        if (!CheckNestingAndOrder(source, out var sErr, out var sSeq))
            issues.Add(new ValidationIssue(IssueKind.TagNestingError, $"Invalid source nesting: {sErr}"));
        if (!CheckNestingAndOrder(target, out var tErr, out var tSeq))
            issues.Add(new ValidationIssue(IssueKind.TagNestingError, $"Invalid target nesting: {tErr}"));
        if (sSeq is not null && tSeq is not null && sSeq.Count == tSeq.Count)
        {
            // 순서 비교(이름/열림/닫힘/셀프) — CK 원자 제외
            if (!sSeq.SequenceEqual(tSeq))
                issues.Add(new ValidationIssue(IssueKind.TagOrderMismatch, "Open/close/self tag order differs"));
        }

        return issues;
    }

    private static void CountEqual(Regex rx, string name, string src, string tgt, List<ValidationIssue> issues,
        IssueKind missing, IssueKind extra)
    {
        int a = rx.Matches(src).Count, b = rx.Matches(tgt).Count;
        if (a != b)
            issues.Add(new ValidationIssue(a > b ? missing : extra, $"{name} placeholders mismatch (src:{a}, tgt:{b})"));
    }

    private static void CompareBraceSequences(string src, string tgt, List<ValidationIssue> issues)
    {
        var rx = new Regex(@"\{(?<name>[0-9A-Za-z_]+)(?:[^{}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var s = rx.Matches(src).Select(m => m.Groups["name"].Value).ToList();
        var t = rx.Matches(tgt).Select(m => m.Groups["name"].Value).ToList();
        if (s.Count == 0 && t.Count == 0) return;
        if (s.Count != t.Count) return; // 이미 개수 불일치 이슈 있음
        if (!s.SequenceEqual(t))
            issues.Add(new ValidationIssue(IssueKind.PlaceholderOrderMismatch, $"brace placeholders order/name differ: src=[{string.Join(",", s)}], tgt=[{string.Join(",", t)}]"));
    }

    private static void ComparePercentBraceSequences(string src, string tgt, List<ValidationIssue> issues)
    {
        var rx = new Regex(@"%\{(?<name>[0-9A-Za-z_]+)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var s = rx.Matches(src).Select(m => m.Groups["name"].Value).ToList();
        var t = rx.Matches(tgt).Select(m => m.Groups["name"].Value).ToList();
        if (s.Count == 0 && t.Count == 0) return;
        if (s.Count != t.Count) return;
        if (!s.SequenceEqual(t))
            issues.Add(new ValidationIssue(IssueKind.PlaceholderOrderMismatch, $"percent-brace placeholders order/name differ: src=[{string.Join(",", s)}], tgt=[{string.Join(",", t)}]"));
    }

    private static void ComparePrintfSequences(string src, string tgt, List<ValidationIssue> issues)
    {
        var rx = new Regex(@"%(?!%)(?:(?<ord>\d+)\$)?(?<flags>[-+ #0]*)(?<width>\d*)?(?:\.(?<prec>\d+))?(?<type>[A-Za-z])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var s = rx.Matches(src).Select(m => new
        {
            ord = m.Groups["ord"].Success ? m.Groups["ord"].Value : string.Empty,
            flags = m.Groups["flags"].Value,
            width = m.Groups["width"].Value,
            prec = m.Groups["prec"].Value,
            type = m.Groups["type"].Value
        }).ToList();
        var t = rx.Matches(tgt).Select(m => new
        {
            ord = m.Groups["ord"].Success ? m.Groups["ord"].Value : string.Empty,
            flags = m.Groups["flags"].Value,
            width = m.Groups["width"].Value,
            prec = m.Groups["prec"].Value,
            type = m.Groups["type"].Value
        }).ToList();
        if (s.Count == 0 && t.Count == 0) return;
        if (s.Count != t.Count) return;
        // 순서/타입
        var sTypes = s.Select(x => x.type).ToList();
        var tTypes = t.Select(x => x.type).ToList();
        if (!sTypes.SequenceEqual(tTypes))
        {
            // 타입 또는 순서 차이 식별
            for (int i = 0; i < s.Count; i++)
            {
                if (s[i].type != t[i].type)
                    issues.Add(new ValidationIssue(IssueKind.PlaceholderTypeMismatch, $"printf type mismatch at {i}: %{s[i].type} -> %{t[i].type}"));
            }
            if (!sTypes.SequenceEqual(tTypes))
                issues.Add(new ValidationIssue(IssueKind.PlaceholderOrderMismatch, $"printf type sequence differs: src=[{string.Join(",", sTypes)}], tgt=[{string.Join(",", tTypes)}]"));
        }
        // 세부 속성 비교(플래그/폭/정밀도)
        for (int i = 0; i < s.Count; i++)
        {
            if (s[i].type == t[i].type)
            {
                if (s[i].flags != t[i].flags || s[i].width != t[i].width || s[i].prec != t[i].prec || s[i].ord != t[i].ord)
                {
                    issues.Add(new ValidationIssue(IssueKind.PlaceholderDetailMismatch,
                        $"printf detail differs at {i}: src=%{s[i].ord}${s[i].flags}{s[i].width}{(string.IsNullOrEmpty(s[i].prec) ? "" : "." + s[i].prec)}{s[i].type} vs tgt=%{t[i].ord}${t[i].flags}{t[i].width}{(string.IsNullOrEmpty(t[i].prec) ? "" : "." + t[i].prec)}{t[i].type}"));
                }
            }
        }
    }

    private static bool CheckNestingAndOrder(string s, out string? error, out List<string>? sequence)
    {
        error = null;
        sequence = new List<string>();
        var matches = RxXmlLike.Matches(s);
        var st = new Stack<string>();
        foreach (Match m in matches)
        {
            var tok = new InlineToken(InlineTokenType.XmlLikeTag, m.Value);
            if (CkTagClassifier.IsCkAtomic(tok)) continue; // CK 원자 태그는 순서/중첩에서 제외

            var (isTag, name, isOpen, isClose, isSelf) = CkTagClassifier.TryGetXmlLike(tok);
            if (!isTag || name is null) continue;
            if (isSelf)
            {
                sequence.Add($"<{name}/>");
                continue;
            }
            if (isOpen)
            {
                st.Push(name);
                sequence.Add($"<{name}>");
            }
            else if (isClose)
            {
                if (st.Count == 0 || st.Peek() != name) { error = $"unexpected </{name}>"; return false; }
                st.Pop();
                sequence.Add($"</{name}>");
            }
        }
        if (st.Count != 0) { error = $"unclosed <{string.Join(",", st)}>"; return false; }
        return true;
    }
}
