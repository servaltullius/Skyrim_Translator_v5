using System.Linq;
using FluentAssertions;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class SrxJaZhTests
{
    [Fact]
    public void Ja_Splits_On_Cjk_Punctuation()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.JaDefault);
        var engine = SrxCompiler.Compile(srxDoc, "ja-JP");
        var text = "これはテストです。次の文です！最後です？";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "これはテストです。",
                "次の文です！",
                "最後です？"
            }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void Zh_Splits_On_Cjk_Punctuation()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.ZhDefault);
        var engine = SrxCompiler.Compile(srxDoc, "zh-CN");
        var text = "这是测试。下一句；最后！";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "这是测试。",
                "下一句；",
                "最后！"
            }, opt => opt.WithStrictOrdering());
    }
}
