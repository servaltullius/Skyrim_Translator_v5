using System.Linq;
using FluentAssertions;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class SrxAbbrevQuotesTests
{
    [Fact]
    public void En_Quotes_Break_After_Closing_Quote()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        var engine = SrxCompiler.Compile(srxDoc, "en-US");
        var text = "He said \"Hello.\" Then left.";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "He said \"Hello.\"",
                " Then left."
            }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void En_Parentheses_Abbrev_NoBreakInside_ThenBreakAfter()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        var engine = SrxCompiler.Compile(srxDoc, "en-US");
        var text = "It works (see p. 12). Next line.";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "It works (see p. 12).",
                " Next line."
            }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void En_Eg_Ie_NoBreak_Inside_Sentence()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        var engine = SrxCompiler.Compile(srxDoc, "en-US");
        var text = "We used e.g. examples and i.e. clarifications. Indeed.";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "We used e.g. examples and i.e. clarifications.",
                " Indeed."
            }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void Ja_Quotes_With_Punctuation()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.JaDefault);
        var engine = SrxCompiler.Compile(srxDoc, "ja-JP");
        var text = "彼は「やあ。」と言った。次へ。";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "彼は「やあ。」と言った。",
                "次へ。"
            }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void Zh_Quotes_With_Punctuation()
    {
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.ZhDefault);
        var engine = SrxCompiler.Compile(srxDoc, "zh-CN");
        var text = "他说“你好。”然后走了。";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(
            new[]
            {
                "他说“你好。”然后走了。"
            }, opt => opt.WithStrictOrdering());
    }
}

