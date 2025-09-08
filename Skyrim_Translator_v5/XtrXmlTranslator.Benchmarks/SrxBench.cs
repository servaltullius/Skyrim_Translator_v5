using BenchmarkDotNet.Attributes;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;

namespace XtrXmlTranslator.Benchmarks;

[MemoryDiagnoser]
public class SrxBench
{
    private string _text = "";
    private SrxEngine _engine = null!;

    [GlobalSetup]
    public void Setup()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        _engine = SrxCompiler.Compile(srxDoc, "en-US");
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 10000; i++) sb.Append("Sentence ").Append(i).Append(". ");
        _text = sb.ToString();
    }

    [Benchmark]
    public int Segment()
    {
        var toks = InlineTokenizer.Tokenize(_text);
        var segs = SrxSegmenter.SegmentTokens(toks, _engine);
        return segs.Count;
    }
}
