using System;
using System.Linq;
using System.Threading.Tasks;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;
using XtrXmlTranslator.Core.Validation;
using Xunit;

namespace XtrXmlTranslator.Tests;

public class GeminiIntegrationScenariosTests
{
    private static bool ShouldRun()
        => string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY"));

    private static SrxEngine Engine()
    {
        const string MinimalSrx = "<srx version=\"2.0\" xmlns=\"http://www.lisa.org/srx20\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><header segmentsubflows=\"yes\" cascade=\"yes\"><formathandle type=\"start\" include=\"no\"/><formathandle type=\"end\" include=\"yes\"/><formathandle type=\"isolated\" include=\"no\"/></header><body><languagerules><languagerule languagerulename=\"Default\"><rule break=\"yes\"><beforebreak>[.?!]</beforebreak><afterbreak>\\s[A-Z]</afterbreak></rule></languagerule></languagerules><maprules><languagemap languagepattern=\".*\" languagerulename=\"Default\"/></maprules></body></srx>";
        var doc = SrxLoader.LoadFromString(MinimalSrx);
        return SrxCompiler.Compile(doc, "en-US");
    }

    private static GeminiTranslator MakeTranslator()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")!;
        var opt = new GeminiOptions { ApiKey = apiKey, Model = "gemini-2.5-flash", Temperature = 0.2, SystemInstruction = "태그와 자리표시자(%d,%s,{0},%{name},<...>,%%)는 절대 수정/삭제/추가하지 말 것." };
        var tm = new InMemoryTm();
        return new GeminiTranslator(opt, tm);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Tags_And_Placeholders_Preserved_Smoke()
    {
        if (!ShouldRun()) return;
        var engine = Engine();
        var input = "The <Alias=Player> has %d gold and %{name}. Hello {0}!";
        var segs = SrxSegmenter.SegmentTokens(InlineTokenizer.Tokenize(input), engine);
        var tr = MakeTranslator();
        string output = string.Empty;
        await tr.TranslateSegmentsAsync(segs, onDelta: (_, _, d) => output += d, ct: default);
        Assert.False(string.IsNullOrWhiteSpace(output));
        var issues = Validator.Validate(input, output);
        Assert.DoesNotContain(issues, i => i.Kind is IssueKind.PlaceholderMissing or IssueKind.PlaceholderExtra or IssueKind.TagCountMismatch or IssueKind.PercentLiteralBroken);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DoublePercent_Remains_Intact()
    {
        if (!ShouldRun()) return;
        var engine = Engine();
        var input = "Progress: 100%% done.";
        var segs = SrxSegmenter.SegmentTokens(InlineTokenizer.Tokenize(input), engine);
        var tr = MakeTranslator();
        string output = string.Empty;
        await tr.TranslateSegmentsAsync(segs, onDelta: (_, _, d) => output += d, ct: default);
        var issues = Validator.Validate(input, output);
        Assert.DoesNotContain(issues, i => i.Kind == IssueKind.PercentLiteralBroken);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MultiSentence_With_Tags_Segments_Ok()
    {
        if (!ShouldRun()) return;
        var engine = Engine();
        var input = "Hello, <Alias=Player>. Welcome! You have %d items.";
        var segs = SrxSegmenter.SegmentTokens(InlineTokenizer.Tokenize(input), engine);
        Assert.True(segs.Count >= 2);
        var tr = MakeTranslator();
        string output = string.Empty;
        await tr.TranslateSegmentsAsync(segs, onDelta: (_, _, d) => output += d, ct: default);
        Assert.False(string.IsNullOrWhiteSpace(output));
        var issues = Validator.Validate(input, output);
        Assert.DoesNotContain(issues, i => i.Kind is IssueKind.PlaceholderMissing or IssueKind.PlaceholderExtra or IssueKind.TagCountMismatch);
    }
}
