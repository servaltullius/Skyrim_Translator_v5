using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class Http429ThenOkHandler : HttpMessageHandler
{
    int _count = 0;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        _count++;
        if (_count < 3)
        {
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{\"error\":\"rate_limited\"}")
            });
        }

        var sb = new StringBuilder();
        sb.AppendLine("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"테\"}]}}]}");
        sb.AppendLine("data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"스트\"}]}}]}");
        sb.AppendLine("data: [DONE]");
        var msg = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "text/event-stream")
        };
        return Task.FromResult(msg);
    }
}

public class ResilienceTests
{
    private static string? TryExtractDelta(string line)
    {
        if (!line.StartsWith("data: ")) return null;
        var payload = line.Substring(6).Trim();
        if (payload == "[DONE]") return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("candidates", out var c) || c.GetArrayLength() == 0) return null;
            var cand0 = c[0];
            if (!cand0.TryGetProperty("content", out var content)) return null;
            if (!content.TryGetProperty("parts", out var parts)) return null;
            var sb = new StringBuilder();
            foreach (var p in parts.EnumerateArray())
                if (p.TryGetProperty("text", out var t)) sb.Append(t.GetString());
            return sb.Length == 0 ? null : sb.ToString();
        }
        catch { return null; }
    }

    [Fact]
    public async Task Retries_429_Then_Succeeds_With_SSE_Deltas()
    {
        using var http = new HttpClient(new Http429ThenOkHandler());

        int attempt = 0;
        HttpResponseMessage? res = null;
        while (attempt < 5)
        {
            res = await http.SendAsync(new HttpRequestMessage(HttpMethod.Post, "http://example.com"), HttpCompletionOption.ResponseHeadersRead);
            if (res.StatusCode == (HttpStatusCode)429)
            {
                await Task.Delay(50);
                attempt++;
                continue;
            }
            res.EnsureSuccessStatusCode();
            break;
        }
        Assert.NotNull(res);
        Assert.Equal(HttpStatusCode.OK, res!.StatusCode);

        await using var stream = await res.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var outSb = new StringBuilder();
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            var d = TryExtractDelta(line);
            if (!string.IsNullOrEmpty(d)) outSb.Append(d);
        }
        Assert.Contains("테스트", outSb.ToString());
    }
}

