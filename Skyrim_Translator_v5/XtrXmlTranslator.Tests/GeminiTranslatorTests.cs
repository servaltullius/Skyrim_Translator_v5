using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using FluentAssertions;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;
using Xunit;

public class FakeSseHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var msg = new HttpResponseMessage(HttpStatusCode.OK);
        var sb = new StringBuilder();
        sb.AppendLine("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Hel\"}]}}]}");
        sb.AppendLine("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"lo\"}]}}]}");
        sb.AppendLine("data: [DONE]");
        msg.Content = new StringContent(sb.ToString(), Encoding.UTF8, "text/event-stream");
        return await Task.FromResult(msg);
    }
}

public class GeminiTranslatorTests
{
    [Fact]
    public async Task TranslateSegments_Wires_Tokens_And_Restores_Tags()
    {
        var seg = new XtrXmlTranslator.Core.Srx.TokenSegment();
        seg.Tokens.Add(new InlineToken(InlineTokenType.Text, "Hello"));
        seg.Tokens.Add(new InlineToken(InlineTokenType.XmlLikeTag, "<b/>"));
        seg.Tokens.Add(new InlineToken(InlineTokenType.Text, " world"));

        var opt = new GeminiOptions { ApiKey = "DUMMY" };
        var tm = new InMemoryTm();

        var translator = new GeminiTranslator(opt, tm, () => new GeminiApiClient(opt, new FakeSseHandler()));
        var results = await translator.TranslateSegmentsAsync(new[] { seg });
        results[0].Should().Contain("<b/>");
    }
}
