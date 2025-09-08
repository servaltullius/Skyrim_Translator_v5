using System.Text;
using System.Text.RegularExpressions;

namespace XtrXmlTranslator.Core.Glossary;

public static class GlossaryApply
{

    private sealed record PatternInfo(string Source, int EntryIndex, bool CaseSensitive, GlossaryType Type, string? Target);

    private static List<(int start, int len, int entryIndex)> SelectGlobalNonOverlapping(string text, List<(int start, int len, int entryIndex)> matches)
    {
        if (matches.Count == 0) return matches;
        var ordered = matches
            .OrderByDescending(m => m.len)
            .ThenBy(m => m.start)
            .ToList();
        var chosen = new List<(int start, int len, int entryIndex)>();
        var occupied = new bool[text.Length > 0 ? text.Length : 0];
        foreach (var m in ordered)
        {
            bool ok = true;
            int end = m.start + m.len;
            if (m.start < 0 || end > text.Length) continue;
            for (int i = m.start; i < end; i++) { if (occupied[i]) { ok = false; break; } }
            if (!ok) continue;
            for (int i = m.start; i < end; i++) occupied[i] = true;
            chosen.Add(m);
        }
        chosen.Sort((a, b) => a.start.CompareTo(b.start));
        return chosen;
    }

    public static (string masked, List<(string Token, string Original)> map)
        MaskProtectedTerms(string src, IEnumerable<GlossaryEntry> entries)
    {
        var protects = entries
            .Select((e, i) => (e, i))
            .Where(x => x.e.Type == GlossaryType.PROTECT && !string.IsNullOrEmpty(x.e.Source))
            .ToList();
        if (protects.Count == 0) return (src, new());

        // Build two automatons: CS and CI
        var csPatterns = new List<PatternInfo>();
        var ciPatterns = new List<PatternInfo>();
        foreach (var (e, idx) in protects)
        {
            var pi = new PatternInfo(e.Source!, idx, e.CaseSensitive, e.Type, e.Target);
            if (e.CaseSensitive) csPatterns.Add(pi); else ciPatterns.Add(pi);
        }
        var csAc = csPatterns.Count > 0 ? new AhoCorasickMatcher(csPatterns.Select(p => p.Source)) : null;
        var ciAc = ciPatterns.Count > 0 ? new AhoCorasickMatcher(ciPatterns.Select(p => p.Source.ToLowerInvariant())) : null;

        var matches = new List<(int start, int len, int entryIndex)>();
        if (csAc is not null)
        {
            foreach (var m in csAc.Find(src))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                matches.Add((m.Start, m.Length, m.PatternIndex));
            }
        }
        if (ciAc is not null)
        {
            var low = src.ToLowerInvariant();
            foreach (var m in ciAc.Find(low))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                // entry index is relative to ciPatterns, offset by cs count
                matches.Add((m.Start, m.Length, csPatterns.Count + m.PatternIndex));
            }
        }
        if (matches.Count == 0) return (src, new());

        // Global longest-first non-overlapping selection
        var selected = SelectGlobalNonOverlapping(src, matches);
        if (selected.Count == 0) return (src, new());

        // Build output
        var map = new List<(string Token, string Original)>();
        var sb = new StringBuilder();
        int cur = 0;
        int tokenId = 0;
        foreach (var (start, len, _) in selected)
        {
            if (start > cur) sb.Append(src, cur, start - cur);
            var orig = src.Substring(start, len);
            var tk = $"⟪T{tokenId++}⟫";
            sb.Append(tk);
            map.Add((tk, orig));
            cur = start + len;
        }
        if (cur < src.Length) sb.Append(src, cur, src.Length - cur);
        return (sb.ToString(), map);
    }

    public static string UnmaskProtectedTerms(string translated, IReadOnlyList<(string Token, string Original)> map)
    {
        var s = translated;
        foreach (var (tk, orig) in map)
            s = s.Replace(tk, orig);
        return s;
    }

    public static string ApplyPostReplace(string translated, IEnumerable<GlossaryEntry> entries)
    {
        var replEntries = entries
            .Select((e, i) => (e, i))
            .Where(x => x.e.Type != GlossaryType.PROTECT && !string.IsNullOrEmpty(x.e.Source) && !string.IsNullOrEmpty(x.e.Target))
            .ToList();
        if (replEntries.Count == 0) return translated;

        var csPatterns = new List<PatternInfo>();
        var ciPatterns = new List<PatternInfo>();
        foreach (var (e, idx) in replEntries)
        {
            var pi = new PatternInfo(e.Source!, idx, e.CaseSensitive, e.Type, e.Target);
            if (e.CaseSensitive) csPatterns.Add(pi); else ciPatterns.Add(pi);
        }
        var csAc = csPatterns.Count > 0 ? new AhoCorasickMatcher(csPatterns.Select(p => p.Source)) : null;
        var ciAc = ciPatterns.Count > 0 ? new AhoCorasickMatcher(ciPatterns.Select(p => p.Source.ToLowerInvariant())) : null;

        var s = translated;
        var matches = new List<(int start, int len, int entryIndex)>();
        if (csAc is not null)
        {
            foreach (var m in csAc.Find(s))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(s, m.Start, m.Length)) continue;
                matches.Add((m.Start, m.Length, m.PatternIndex));
            }
        }
        if (ciAc is not null)
        {
            var low = s.ToLowerInvariant();
            foreach (var m in ciAc.Find(low))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(s, m.Start, m.Length)) continue;
                matches.Add((m.Start, m.Length, csPatterns.Count + m.PatternIndex));
            }
        }
        if (matches.Count == 0) return s;

        // Global longest-first with ENFORCE priority, non-overlapping selection
        var ordered = matches
            .Select(m =>
            {
                var isCs = m.entryIndex < csPatterns.Count;
                var info = isCs ? csPatterns[m.entryIndex] : ciPatterns[m.entryIndex - csPatterns.Count];
                int pri = info.Type == GlossaryType.ENFORCE ? 2 : 1;
                return (m.start, m.len, m.entryIndex, isCs, pri);
            })
            .OrderByDescending(x => x.len)
            .ThenByDescending(x => x.pri)
            .ThenBy(x => x.start)
            .ToList();

        var selected = new List<(int start, int len, int entryIndex, bool isCs)>();
        var occupied = new bool[s.Length > 0 ? s.Length : 0];
        foreach (var m in ordered)
        {
            bool ok = true;
            int end = m.start + m.len;
            if (m.start < 0 || end > s.Length) continue;
            for (int i = m.start; i < end; i++) { if (occupied[i]) { ok = false; break; } }
            if (!ok) continue;
            for (int i = m.start; i < end; i++) occupied[i] = true;
            selected.Add((m.start, m.len, m.entryIndex, m.isCs));
        }
        if (selected.Count == 0) return s;
        selected.Sort((a, b) => a.start.CompareTo(b.start));

        var sb = new StringBuilder();
        int cur = 0;
        foreach (var ch in selected)
        {
            if (ch.start > cur) sb.Append(s, cur, ch.start - cur);
            var info = ch.isCs ? csPatterns[ch.entryIndex] : ciPatterns[ch.entryIndex - csPatterns.Count];
            sb.Append(info.Target);
            cur = ch.start + ch.len;
        }
        if (cur < s.Length) sb.Append(s, cur, s.Length - cur);
        return sb.ToString();
    }

    public static List<(string Source, string Target, string Type)>
        SummarizeForBatch(IEnumerable<string> batchSources, GlossaryStore g, int limit = 120)
    {
        var result = new List<(string, string, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = g.Entries.Where(e => !string.IsNullOrEmpty(e.Source)).ToList();
        if (entries.Count == 0) return result;

        var csPatterns = new List<string>();
        var csIndex = new List<int>();
        var ciPatterns = new List<string>();
        var ciIndex = new List<int>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.CaseSensitive) { csPatterns.Add(e.Source!); csIndex.Add(i); }
            else { ciPatterns.Add(e.Source!.ToLowerInvariant()); ciIndex.Add(i); }
        }
        var csAc = csPatterns.Count > 0 ? new AhoCorasickMatcher(csPatterns) : null;
        var ciAc = ciPatterns.Count > 0 ? new AhoCorasickMatcher(ciPatterns) : null;

        foreach (var src in batchSources)
        {
            if (result.Count >= limit) break;
            if (csAc is not null)
            {
                foreach (var m in csAc.Find(src))
                {
                    var idx = csIndex[m.PatternIndex];
                    var e = entries[idx];
                    if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                    if (seen.Add(e.Source!))
                    {
                        result.Add((e.Source!, e.Target ?? string.Empty, e.Type.ToString()));
                        if (result.Count >= limit) return result;
                    }
                }
            }
            if (ciAc is not null)
            {
                var low = src.ToLowerInvariant();
                foreach (var m in ciAc.Find(low))
                {
                    var idx = ciIndex[m.PatternIndex];
                    var e = entries[idx];
                    if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                    if (seen.Add(e.Source!))
                    {
                        result.Add((e.Source!, e.Target ?? string.Empty, e.Type.ToString()));
                        if (result.Count >= limit) return result;
                    }
                }
            }
        }
        return result;
    }
}

internal static class SbExt
{
    public static void ReplaceByMatch(this StringBuilder sb, Regex rx, Func<Match, string> repl)
    {
        var input = sb.ToString();
        var ms = rx.Matches(input);
        if (ms.Count == 0) return;

        int last = 0;
        var outSb = new StringBuilder(input.Length);
        foreach (Match m in ms)
        {
            outSb.Append(input, last, m.Index - last);
            outSb.Append(repl(m));
            last = m.Index + m.Length;
        }
        outSb.Append(input, last, input.Length - last);
        sb.Clear().Append(outSb.ToString());
    }
}
