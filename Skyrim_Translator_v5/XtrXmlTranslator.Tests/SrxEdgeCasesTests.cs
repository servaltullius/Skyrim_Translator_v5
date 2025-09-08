using System.Linq;
using FluentAssertions;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class SrxEdgeCasesTests
{
    [Fact]
    public void En_Abbreviation_USA_Breaks_After_Final_Period()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        var engine = SrxCompiler.Compile(srxDoc, "en-US");
        var text = "Hello U.S.A. Today we go.";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(new[] { "Hello U.S.A.", " Today we go." }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void Tag_Adjacent_Period_Breaks_Outside_Tag()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        var engine = SrxCompiler.Compile(srxDoc, "en-US");
        var text = "Text<b/>. Next";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(new[] { "Text<b/>.", " Next" }, opt => opt.WithStrictOrdering());
    }
}
