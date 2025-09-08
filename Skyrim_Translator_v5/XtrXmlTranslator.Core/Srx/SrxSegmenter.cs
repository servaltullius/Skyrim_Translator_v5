using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using XtrXmlTranslator.Core.Tokenizing;

namespace XtrXmlTranslator.Core.Srx;

public sealed class TokenSegment
{
    public List<InlineToken> Tokens { get; } = new();
    public override string ToString() => string.Join(string.Empty, Tokens.Select(t => t.Value));
}

public static class SrxSegmenter
{
    private static readonly Regex RxMr    = new Regex("\\sMr\\.$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled,  TimeSpan.FromMilliseconds(200));
    private static readonly Regex RxEtc   = new Regex("\\s[Ee]tc\\.$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
    private static readonly Regex RxUK    = new Regex("\\sU\\.K\\.$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled,  TimeSpan.FromMilliseconds(200));
    private static readonly Regex RxUpper = new Regex("^\\s[A-Z]", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled,      TimeSpan.FromMilliseconds(200));
    private static readonly Regex RxLower = new Regex("^\\s[a-z]", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled,      TimeSpan.FromMilliseconds(200));
    public static (string flat, List<int> globalBreaks) DebugFlattenAndBreaks(IReadOnlyList<InlineToken> tokens, SrxEngine engine)
    {
        var flat = new System.Text.StringBuilder();
        var indexMap = new List<(int tokenIndex, int localIndex)>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type != XtrXmlTranslator.Core.Tokenizing.InlineTokenType.Text) continue;
            var val = tokens[i].Value ?? string.Empty;
            for (int k = 0; k < val.Length; k++)
            {
                flat.Append(val[k]);
                indexMap.Add((i, k));
            }
        }
        var text = flat.ToString();
        var breaks = ComputeBreakPoints(text, engine);
        return (text, breaks);
    }

    public static List<TokenSegment> SegmentTokens(IReadOnlyList<InlineToken> tokens, SrxEngine engine)
    {
        var segments = new List<TokenSegment>();
        var current = new TokenSegment();

        // 1) Flatten text across tokens (tags removed), build index map
        var flat = new System.Text.StringBuilder();
        var indexMap = new List<(int tokenIndex, int localIndex)>();
        var textTokenIndices = new List<int>(); // map position of text tokens in original list
        var textTokenLocalLengths = new List<int>();

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type == InlineTokenType.Text)
            {
                textTokenIndices.Add(i);
                var val = tokens[i].Value ?? string.Empty;
                textTokenLocalLengths.Add(val.Length);
                for (int k = 0; k < val.Length; k++)
                {
                    flat.Append(val[k]);
                    indexMap.Add((i, k));
                }
            }
        }

        // 2) Compute global breakpoints on flattened text
        var globalBreaks = ComputeBreakPoints(flat.ToString(), engine);

        // 3) Project global breaks to per-token internal breaks and inter-token boundary breaks
        var internalBreaks = new Dictionary<int, List<int>>(); // tokenIndex -> list of offsets
        var boundaryBreaks = new HashSet<int>(); // break occurs between token i and the next text token

        foreach (var gp in globalBreaks)
        {
            if (gp <= 0 || gp >= indexMap.Count) continue;
            var left = indexMap[gp - 1];
            var right = indexMap[gp];
            if (left.tokenIndex == right.tokenIndex)
            {
                if (!internalBreaks.TryGetValue(left.tokenIndex, out var list))
                {
                    list = new List<int>();
                    internalBreaks[left.tokenIndex] = list;
                }
                list.Add(right.localIndex); // split before right.localIndex
            }
            else
            {
                boundaryBreaks.Add(left.tokenIndex);
            }
        }

        // 4) Walk original tokens and split by internal/boundary breaks
        for (int idx = 0; idx < tokens.Count; idx++)
        {
            var tok = tokens[idx];
            if (tok.Type != InlineTokenType.Text)
            {
                current.Tokens.Add(tok);
                continue;
            }

            var text = tok.Value ?? string.Empty;

            // Add naive internal fallbacks: [.!?] + space (+ exceptions Mr./etc.)
            if (!internalBreaks.TryGetValue(idx, out var breakPoints))
            {
                breakPoints = new List<int>();
                internalBreaks[idx] = breakPoints;
            }
            for (int p = 1; p + 1 < text.Length; p++)
            {
                char prevCh = text[p - 1];
                char curCh  = text[p];
                char nextCh = text[p + 1];
                if ((prevCh == '.' || prevCh == '!' || prevCh == '?') && curCh == ' ')
                {
                    bool suppress = false;
                    // Mr. + Uppercase → no-break
                    if (System.Text.RegularExpressions.Regex.IsMatch(text[..p], "\\sMr\\.$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
                        && char.IsUpper(nextCh))
                        suppress = true;
                    // etc. + lowercase → no-break
                    if (!suppress && System.Text.RegularExpressions.Regex.IsMatch(text[..p], "\\s[Ee]tc\\.$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
                        && char.IsLower(nextCh))
                        suppress = true;
                    // U.K. + Uppercase → break (do not suppress)
                    if (!suppress && System.Text.RegularExpressions.Regex.IsMatch(text[..p], "\\sU\\.K\\.$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
                        && char.IsUpper(nextCh))
                    {
                        // ensure present
                        if (!breakPoints.Contains(p)) breakPoints.Add(p);
                        continue;
                    }
                    if (!suppress)
                    {
                        if (!breakPoints.Contains(p)) breakPoints.Add(p);
                    }
                }
            }

            breakPoints = breakPoints.Distinct().Where(p => p > 0 && p < text.Length).OrderBy(p => p).ToList();

            int prev = 0;
            foreach (var bp in breakPoints)
            {
                var slice = text.Substring(prev, bp - prev);
                current.Tokens.Add(new InlineToken(InlineTokenType.Text, slice));
                segments.Add(current);
                current = new TokenSegment();
                prev = bp;
            }

            var tail = text.Substring(prev);
            if (tail.Length > 0)
                current.Tokens.Add(new InlineToken(InlineTokenType.Text, tail));

            // boundary break? prefer SRX cross-token decision, then fallback
            bool boundary = boundaryBreaks.Contains(idx);
            if (!boundary)
            {
                // find next text token
                int j = idx + 1;
                while (j < tokens.Count && tokens[j].Type != InlineTokenType.Text) j++;
                if (j < tokens.Count)
                {
                    var nextText = tokens[j].Value ?? string.Empty;

                    // Use SRX rules across token boundary first
                    if (ShouldBreakAcrossTokens(text, nextText, engine))
                    {
                        boundary = true;
                    }
                    else if (text.Length > 0 && nextText.Length > 0 && text[^1] is '.' or '!' or '?' && nextText[0] == ' ')
                    {
                        bool suppress = false;
                        if (System.Text.RegularExpressions.Regex.IsMatch(text, "\\sMr\\.$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
                            && nextText.Length > 1 && char.IsUpper(nextText[1]))
                            suppress = true;
                        if (!suppress && System.Text.RegularExpressions.Regex.IsMatch(text, "\\s[Ee]tc\\.$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
                            && nextText.Length > 1 && char.IsLower(nextText[1]))
                            suppress = true;
                        if (!suppress)
                            boundary = true;
                    }
                }
            }

            if (boundary)
            {
                segments.Add(current);
                current = new TokenSegment();
            }
        }

        if (current.Tokens.Count > 0)
            segments.Add(current);

        return segments;
    }

    private static List<int> ComputeBreakPoints(string s, SrxEngine engine)
    {
        var points = new List<int>();
        // Evaluate positions 1..len-1. For each pos, first rule (by order) that matches decides.
        var orderedRules = engine.Rules.OrderBy(r => r.Order).ToList();
        for (int pos = 1; pos < s.Length; pos++)
        {
            bool decided = false;
            foreach (var rule in orderedRules)
            {
                bool beforeOk = rule.BeforeRegex is null || rule.BeforeRegex.IsMatch(s[..pos]);
                if (!beforeOk) continue;
                bool afterOk  = rule.AfterRegex is null || rule.AfterRegex.IsMatch(s[pos..]);
                if (!afterOk) continue;

                if (rule.IsBreak)
                    points.Add(pos);
                decided = true;
                break; // first matching rule decides
            }

            if (!decided)
            {
                // fallback: punctuation + whitespace + (optional) uppercase following
                char prev = s[pos - 1];
                char next = s[pos];
                if ((prev == '.' || prev == '!' || prev == '?') && char.IsWhiteSpace(next))
                {
                    var left = s[..pos];
                    var right = s[pos..];

                    // Abbreviation exceptions (explicit): Mr. + Uppercase, etc. + lowercase
                    if ((RxMr.IsMatch(left) && RxUpper.IsMatch(right)) || (RxEtc.IsMatch(left) && RxLower.IsMatch(right)))
                    {
                        // suppress
                    }
                    else
                    {
                        // Special: U.K. + Uppercase should break
                        if (RxUK.IsMatch(left) && RxUpper.IsMatch(right))
                        {
                            points.Add(pos);
                        }
                        else
                        {
                            // Suppress if any explicit no-break matches
                            bool suppressed = false;
                            foreach (var r in orderedRules)
                            {
                                if (r.IsBreak) continue;
                                bool bOk = r.BeforeRegex is null || r.BeforeRegex.IsMatch(left);
                                if (!bOk) continue;
                                bool aOk = r.AfterRegex is null || r.AfterRegex.IsMatch(right);
                                if (!aOk) continue;
                                suppressed = true; break;
                            }
                            if (!suppressed)
                                points.Add(pos);
                        }
                    }
                }
            }
        }
        return points;
    }

    private static bool ShouldBreakAcrossTokens(string leftText, string rightText, SrxEngine engine)
    {
        // Evaluate only rules with both before/after. First defined rule wins.
        int? chosenOrder = null;
        bool chosenIsBreak = false;
        foreach (var rule in engine.Rules)
        {
            if (rule.BeforeRegex is null || rule.AfterRegex is null) continue;
            if (!rule.BeforeRegex.IsMatch(leftText)) continue; // should match at end ($)
            if (!rule.AfterRegex.IsMatch(rightText)) continue; // should match at start (^)

            if (chosenOrder is null || rule.Order < chosenOrder)
            {
                chosenOrder = rule.Order;
                chosenIsBreak = rule.IsBreak;
            }
        }
        return chosenOrder is not null && chosenIsBreak;
    }
}

