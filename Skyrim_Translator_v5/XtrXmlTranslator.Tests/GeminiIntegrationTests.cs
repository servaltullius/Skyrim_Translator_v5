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

public class GeminiIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task StreamTranslate_Smoke_When_ApiKey_Present()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey) || !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            // No secret or not running on CI -> skip body
            return;
        }

        const string MinimalSrx = "<srx version=\"2.0\" xmlns=\"http://www.lisa.org/srx20\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><header segmentsubflows=\"yes\" cascade=\"yes\"><formathandle type=\"start\" include=\"no\"/><formathandle type=\"end\" include=\"yes\"/><formathandle type=\"isolated\" include=\"no\"/></header><body><languagerules><languagerule languagerulename=\"Default\"><rule break=\"yes\"><beforebreak>[.?!]</beforebreak><afterbreak>\\s[A-Z]</afterbreak></rule></languagerule></languagerules><maprules><languagemap languagepattern=\".*\" languagerulename=\"Default\"/></maprules></body></srx>";
        var doc = SrxLoader.LoadFromString(MinimalSrx);
        var srx = SrxCompiler.Compile(doc, "en-US");

        var input = "Hello <Alias=Player> %d!";
        var toks = InlineTokenizer.Tokenize(input);
        var segs = SrxSegmenter.SegmentTokens(toks, srx);

        var opt = new GeminiOptions { ApiKey = apiKey, Model = "gemini-2.5-flash", Temperature = 0.2, SystemInstruction = "단문 테스트; 태그/플레이스홀더 보존" };
        var tm = new InMemoryTm();
        var translator = new GeminiTranslator(opt, tm);

        string output = string.Empty;
        await translator.TranslateSegmentsAsync(
            segs,
            onDelta: (si, ri, delta) => { output += delta; },
            ct: default);

        Assert.False(string.IsNullOrWhiteSpace(output));
        // 자리표시자/태그 보존 확인(보수적)
        var issues = Validator.Validate(input, output);
        Assert.DoesNotContain(issues, i => i.Kind == IssueKind.PlaceholderMissing || i.Kind == IssueKind.PlaceholderExtra || i.Kind == IssueKind.TagCountMismatch);
    }
}

