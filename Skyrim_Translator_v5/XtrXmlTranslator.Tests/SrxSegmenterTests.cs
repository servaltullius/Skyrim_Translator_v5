using System.Collections.Generic;
using System.Linq;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
public class SrxSegmenterTests
{
    private readonly ITestOutputHelper _out;
    public SrxSegmenterTests(ITestOutputHelper output) => _out = output;

    private const string MinimalSrx = @"<srx version=""2.0"" xmlns=""http://www.lisa.org/srx20"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <header segmentsubflows=""yes"" cascade=""yes"">
    <formathandle type=""start"" include=""no""/>
    <formathandle type=""end"" include=""yes""/>
    <formathandle type=""isolated"" include=""no""/>
  </header>
  <body>
    <languagerules>
      <languagerule languagerulename=""Default"">
        <rule break=""yes""><beforebreak>[.?!]</beforebreak><afterbreak>\s[A-Z]</afterbreak></rule>
      </languagerule>
      <languagerule languagerulename=""English"">
        <rule break=""no""><beforebreak>\sMr\.</beforebreak><afterbreak>\s[A-Z]</afterbreak></rule>
        <rule break=""yes""><beforebreak>\sU\.K\.</beforebreak><afterbreak>\s[A-Z]</afterbreak></rule>
        <rule break=""no""><beforebreak>\sU\.K\.</beforebreak><afterbreak>\s[a-z]</afterbreak></rule>
        <rule break=""no""><beforebreak>\s[Ee]tc\.</beforebreak><afterbreak>\s[a-z]</afterbreak></rule>
      </languagerule>
    </languagerules>
    <maprules>
      <languagemap languagepattern=""^en(-[A-Za-z0-9]+)?$"" languagerulename=""English""/>
      <languagemap languagepattern="".*"" languagerulename=""Default""/>
    </maprules>
  </body>
</srx>";

    [Fact]
    public void Segmenter_Breaks_On_Punct_Not_On_Abbrev()
    {
        var doc = SrxLoader.LoadFromString(MinimalSrx);
        var engine = SrxCompiler.Compile(doc, "en-US");

        var text = "This is Mr. Abel from the U.K. He wrote etc. today. Ok?";
        var toks = InlineTokenizer.Tokenize(text);
        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        var dbg = SrxSegmenter.DebugFlattenAndBreaks(toks, engine);
        var debugMsg = $"flat='{dbg.flat}' breaks=[{string.Join(",", dbg.globalBreaks)}]";
        _out.WriteLine($"DBG1: {debugMsg}");

        segs.Select(s => s.ToString()).Should().BeEquivalentTo(new[]
        {
            "This is Mr. Abel from the U.K.",
            " He wrote etc. today.",
            " Ok?"
        }, opts => opts.WithStrictOrdering(), "Debug: {0}", debugMsg);
    }

    [Fact]
    public void Segmenter_Does_Not_Cross_Tags()
    {
        var doc = SrxLoader.LoadFromString(MinimalSrx);
        var engine = SrxCompiler.Compile(doc, "en-US");

        var toks = new List<InlineToken>
        {
            new(InlineTokenType.Text, "Hello."),
            new(InlineTokenType.XmlLikeTag, "<b/>"),
            new(InlineTokenType.Text, " World! How are you?")
        };

        var segs = SrxSegmenter.SegmentTokens(toks, engine);
        var dbg = SrxSegmenter.DebugFlattenAndBreaks(toks, engine);
        var debugMsg = $"flat='{dbg.flat}' breaks=[{string.Join(",", dbg.globalBreaks)}]";
        _out.WriteLine($"DBG2: {debugMsg}");
        segs.Select(s => s.ToString()).Should().BeEquivalentTo(new[]
        {
            "Hello.",
            "<b/> World!",
            " How are you?"
        }, opts => opts.WithStrictOrdering(), "Debug: {0}", debugMsg);
    }
}

