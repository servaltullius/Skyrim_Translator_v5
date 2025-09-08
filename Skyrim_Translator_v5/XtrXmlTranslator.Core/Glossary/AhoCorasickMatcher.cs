using System.Collections.Generic;

namespace XtrXmlTranslator.Core.Glossary;

internal sealed class AhoCorasickMatcher
{
    private sealed class Node
    {
        public Dictionary<char, int> Next { get; } = new();
        public int Fail { get; set; } = 0;
        public List<int> Out { get; } = new(); // indices of patterns
    }

    private readonly List<Node> _nodes = new() { new Node() }; // root at 0
    private readonly string[] _patterns;

    public AhoCorasickMatcher(IEnumerable<string> patterns)
    {
        _patterns = patterns as string[] ?? new List<string>(patterns).ToArray();
        Build();
    }

    private void Build()
    {
        // insert patterns
        for (int idx = 0; idx < _patterns.Length; idx++)
        {
            var p = _patterns[idx];
            int s = 0;
            foreach (var ch in p)
            {
                if (!_nodes[s].Next.TryGetValue(ch, out var t))
                {
                    t = _nodes.Count;
                    _nodes[s].Next[ch] = t;
                    _nodes.Add(new Node());
                }
                s = t;
            }
            _nodes[s].Out.Add(idx);
        }
        // build fail links (BFS)
        var q = new Queue<int>();
        foreach (var kv in _nodes[0].Next)
            q.Enqueue(kv.Value);
        while (q.Count > 0)
        {
            int v = q.Dequeue();
            foreach (var (ch, u) in _nodes[v].Next)
            {
                q.Enqueue(u);
                int f = _nodes[v].Fail;
                while (f != 0 && !_nodes[f].Next.ContainsKey(ch)) f = _nodes[f].Fail;
                if (_nodes[f].Next.TryGetValue(ch, out var nf))
                    _nodes[u].Fail = nf;
                _nodes[u].Out.AddRange(_nodes[_nodes[u].Fail].Out);
            }
        }
    }

    public readonly struct Match
    {
        public readonly int Start;
        public readonly int Length;
        public readonly int PatternIndex;
        public Match(int s, int l, int p) { Start = s; Length = l; PatternIndex = p; }
    }

    public List<Match> Find(string text)
    {
        var matches = new List<Match>();
        int s = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            while (s != 0 && !_nodes[s].Next.ContainsKey(ch)) s = _nodes[s].Fail;
            if (_nodes[s].Next.TryGetValue(ch, out var t)) s = t;
            foreach (var p in _nodes[s].Out)
            {
                int len = _patterns[p].Length;
                matches.Add(new Match(i - len + 1, len, p));
            }
        }
        return matches;
    }
}
