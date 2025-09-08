using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using FluentAssertions;
using XtrXmlTranslator.Core.Translate;
using Xunit;

internal sealed class FakeJsonOkHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var json = "{" +
                   "\"candidates\":[{" +
                   "\"content\":{\"parts\":[{\"text\":\"Hel\"},{\"text\":\"lo\"}]}}]" +
                   "}";
        var res = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(res);
    }
}

internal sealed class FakeJsonErrorHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var json = "{\"error\":{\"message\":\"Quota exceeded\"}}";
        var res = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(res);
    }
}

public class GeminiJsonFallbackTests
{
    [Fact]
    public async Task JsonFallback_ReturnsCombinedTextOnce()
    {
        var opt = new GeminiOptions { ApiKey = "DUMMY" };
        using var client = new GeminiApiClient(opt, new FakeJsonOkHandler());

        var chunks = new List<string>();
        await foreach (var d in client.StreamTranslateAsync("Hello"))
            chunks.Add(d);

        chunks.Should().HaveCount(1);
        string.Concat(chunks).Should().Be("Hello");
    }

    [Fact]
    public async Task JsonFallback_Error_ThrowsHttpRequestException()
    {
        var opt = new GeminiOptions { ApiKey = "DUMMY" };
        using var client = new GeminiApiClient(opt, new FakeJsonErrorHandler());

        var act = async () =>
        {
            await foreach (var _ in client.StreamTranslateAsync("X")) { }
        };
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Gemini JSON error*Quota exceeded*");
    }
}

