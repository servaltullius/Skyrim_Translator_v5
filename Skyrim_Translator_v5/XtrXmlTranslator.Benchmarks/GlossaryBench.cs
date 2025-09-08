using BenchmarkDotNet.Attributes;
using XtrXmlTranslator.Core.Glossary;

namespace XtrXmlTranslator.Benchmarks;

[MemoryDiagnoser]
public class GlossaryBench
{
    private GlossaryStore _store = new();
    private GlossaryCompiled _compiled = null!;
    private string _text = "";

    [GlobalSetup]
    public void Setup()
    {
        // 5k terms
        for (int i = 0; i < 5000; i++)
        {
            _store.Entries.Add(new GlossaryEntry { Source = $"term{i}", Target = $"용어{i}", Type = i % 3 == 0 ? GlossaryType.ENFORCE : GlossaryType.PREFER, CaseSensitive = false });
        }
        _store.Entries.Add(new GlossaryEntry { Source = "Skyrim", Type = GlossaryType.PROTECT, CaseSensitive = true });
        _store.Entries.Add(new GlossaryEntry { Source = "septim", Type = GlossaryType.PROTECT, CaseSensitive = false });
        _compiled = GlossaryCompiled.Build(_store);

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 20000; i++)
        {
            sb.Append("This is term").Append(i % 5000).Append(' ');
        }
        sb.Append("Welcome to Skyrim with 100 septim.");
        _text = sb.ToString();
    }

    [Benchmark]
    public (string masked, int mcount) MaskProtected()
    {
        var (masked, map) = _compiled.MaskProtected(_text);
        return (masked, map.Count);
    }

    [Benchmark]
    public string ApplyReplace()
    {
        return _compiled.ApplyReplacement(_text);
    }
}
