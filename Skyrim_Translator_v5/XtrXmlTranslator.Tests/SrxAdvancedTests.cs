using System.Linq;
using FluentAssertions;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class SrxAdvancedTests
{
    [Fact]
    public void En_Segmentation_Respects_Abbreviation_NoBreaks()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        var engine = SrxCompiler.Compile(srxDoc, "en-US");
        var text = "Hello Mr. Smith. We sell widgets, etc. are common. Indeed.";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);

        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "Hello Mr. Smith.",
                " We sell widgets, etc. are common.",
                " Indeed."
            }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Ko_Segmentation_Splits_On_Cjk_Punctuation()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.KoDefault);
        var engine = SrxCompiler.Compile(srxDoc, "ko-KR");
        var text = "안녕하세요。여기는 스카이림입니다。잘 부탁합니다。";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);

        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "안녕하세요。",
                "여기는 스카이림입니다。",
                "잘 부탁합니다。"
            }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Tag_Boundary_Is_Not_Split_Within_Tag()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.KoDefault);
        var engine = SrxCompiler.Compile(srxDoc, "ko-KR");
        var text = "테스트<b/>문장입니다. 다음 문장";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);

        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "테스트<b/>문장입니다.",
                " 다음 문장"
            }, options => options.WithStrictOrdering());
    }
}
