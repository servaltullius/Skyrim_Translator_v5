using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XtrXmlTranslator.Core.Glossary;

public sealed class GlossaryCompiled
{

    private AhoCorasickMatcher? _protCs;
    private AhoCorasickMatcher? _protCi;
    private readonly List<GlossaryEntry> _protCsEntries = new();
    private readonly List<GlossaryEntry> _protCiEntries = new();

    private AhoCorasickMatcher? _replCs;
    private AhoCorasickMatcher? _replCi;
    private readonly List<GlossaryEntry> _replCsEntries = new();
    private readonly List<GlossaryEntry> _replCiEntries = new();

    private GlossaryCompiled() { }

    public static GlossaryCompiled Build(GlossaryStore store)
    {
        var gc = new GlossaryCompiled();
        // Protect
        foreach (var e in store.Entries.Where(e => e.Type == GlossaryType.PROTECT && !string.IsNullOrEmpty(e.Source)))
        {
            if (e.CaseSensitive) gc._protCsEntries.Add(e);
            else gc._protCiEntries.Add(e);
        }
        if (gc._protCsEntries.Count > 0)
            gc._protCs = new AhoCorasickMatcher(gc._protCsEntries.Select(p => p.Source!));
        if (gc._protCiEntries.Count > 0)
            gc._protCi = new AhoCorasickMatcher(gc._protCiEntries.Select(p => p.Source!.ToLowerInvariant()));
        // Replace
        foreach (var e in store.Entries.Where(e => e.Type != GlossaryType.PROTECT && !string.IsNullOrEmpty(e.Source) && !string.IsNullOrEmpty(e.Target)))
        {
            if (e.CaseSensitive) gc._replCsEntries.Add(e);
            else gc._replCiEntries.Add(e);
        }
        if (gc._replCsEntries.Count > 0)
            gc._replCs = new AhoCorasickMatcher(gc._replCsEntries.Select(p => p.Source!));
        if (gc._replCiEntries.Count > 0)
            gc._replCi = new AhoCorasickMatcher(gc._replCiEntries.Select(p => p.Source!.ToLowerInvariant()));
        return gc;
    }

    public (string masked, List<(string Token, string Original)> map) MaskProtected(string src)
    {
        var matches = new List<(int start, int len, int idx, bool cs)>();
        if (_protCs is not null)
        {
            foreach (var m in _protCs.Find(src))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                matches.Add((m.Start, m.Length, m.PatternIndex, true));
            }
        }
        if (_protCi is not null)
        {
            var low = src.ToLowerInvariant();
            foreach (var m in _protCi.Find(low))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                matches.Add((m.Start, m.Length, m.PatternIndex, false));
            }
        }
        if (matches.Count == 0) return (src, new());

        // Global longest-first non-overlapping selection
        var ordered = matches
            .OrderByDescending(m => m.len)
            .ThenBy(m => m.start)
            .ToList();
        var chosen = new List<(int start, int len, int idx, bool cs)>();
        var occupied = new bool[src.Length > 0 ? src.Length : 0];
        foreach (var m in ordered)
        {
            bool ok = true;
            int end = m.start + m.len;
            if (m.start < 0 || end > src.Length) continue;
            for (int i = m.start; i < end; i++) { if (occupied[i]) { ok = false; break; } }
            if (!ok) continue;
            for (int i = m.start; i < end; i++) occupied[i] = true;
            chosen.Add(m);
        }
        chosen.Sort((a, b) => a.start.CompareTo(b.start));

        var map = new List<(string Token, string Original)>();
        var sb = new StringBuilder();
        int cur = 0; int tokenId = 0;
        foreach (var ch in chosen)
        {
            if (ch.start > cur) sb.Append(src, cur, ch.start - cur);
            var orig = src.Substring(ch.start, ch.len);
            var tk = $"⟪T{tokenId++}⟫";
            sb.Append(tk);
            map.Add((tk, orig));
            cur = ch.start + ch.len;
        }
        if (cur < src.Length) sb.Append(src, cur, src.Length - cur);
        return (sb.ToString(), map);
    }

    public string ApplyReplacement(string text)
    {
        if (_replCs is null && _replCi is null) return text;
        var s = text;
        var matches = new List<(int start, int len, int idx, bool cs)>();
        if (_replCs is not null)
        {
            foreach (var m in _replCs.Find(s))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(s, m.Start, m.Length)) continue;
                matches.Add((m.Start, m.Length, m.PatternIndex, true));
            }
        }
        if (_replCi is not null)
        {
            var low = s.ToLowerInvariant();
            foreach (var m in _replCi.Find(low))
            {
                if (m.Start < 0) continue;
                if (!GlossaryBoundary.CheckWordBoundary(s, m.Start, m.Length)) continue;
                matches.Add((m.Start, m.Length, m.PatternIndex, false));
            }
        }
        if (matches.Count == 0) return s;

        // Global longest-first with ENFORCE priority, non-overlapping selection
        var ordered = matches
            .Select(m =>
            {
                var type = m.cs ? _replCsEntries[m.idx].Type : _replCiEntries[m.idx].Type;
                int pri = type == GlossaryType.ENFORCE ? 2 : 1;
                return (m.start, m.len, m.idx, m.cs, pri);
            })
            .OrderByDescending(x => x.len)
            .ThenByDescending(x => x.pri)
            .ThenBy(x => x.start)
            .ToList();

        var chosen = new List<(int start, int len, int idx, bool cs)>();
        var occupied = new bool[s.Length > 0 ? s.Length : 0];
        foreach (var m in ordered)
        {
            bool ok = true;
            int end = m.start + m.len;
            if (m.start < 0 || end > s.Length) continue;
            for (int i = m.start; i < end; i++) { if (occupied[i]) { ok = false; break; } }
            if (!ok) continue;
            for (int i = m.start; i < end; i++) occupied[i] = true;
            chosen.Add((m.start, m.len, m.idx, m.cs));
        }
        chosen.Sort((a, b) => a.start.CompareTo(b.start));

        var sb = new StringBuilder();
        int cur = 0;
        foreach (var ch in chosen)
        {
            if (ch.start > cur) sb.Append(s, cur, ch.start - cur);
            var entry = ch.cs ? _replCsEntries[ch.idx] : _replCiEntries[ch.idx];
            sb.Append(entry.Target);
            cur = ch.start + ch.len;
        }
        if (cur < s.Length) sb.Append(s, cur, s.Length - cur);
        return sb.ToString();
    }

    public List<(string Source, string Target, string Type)> SummarizeForBatch(IEnumerable<string> sources, int limit)
    {
        var result = new List<(string, string, string)>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var src in sources)
        {
            if (result.Count >= limit) break;
            if (_replCs is not null)
            {
                foreach (var m in _replCs.Find(src))
                {
                    if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                    var e = _replCsEntries[m.PatternIndex];
                    if (seen.Add(e.Source!))
                    {
                        result.Add((e.Source!, e.Target ?? string.Empty, e.Type.ToString()));
                        if (result.Count >= limit) return result;
                    }
                }
            }
            if (_replCi is not null)
            {
                var low = src.ToLowerInvariant();
                foreach (var m in _replCi.Find(low))
                {
                    if (!GlossaryBoundary.CheckWordBoundary(src, m.Start, m.Length)) continue;
                    var e = _replCiEntries[m.PatternIndex];
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
