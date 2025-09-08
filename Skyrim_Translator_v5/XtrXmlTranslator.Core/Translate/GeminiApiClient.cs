using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Polly;
using Polly.Retry;
using System.Net;
using System.Threading.Tasks;

namespace XtrXmlTranslator.Core.Translate;

public sealed class GeminiApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _opt;
    private readonly ResiliencePipeline<HttpResponseMessage> _handshakePipeline;
    private readonly bool _ownsHttpClient;

    public GeminiApiClient(GeminiOptions opt, HttpMessageHandler? handler = null)
    {
        _opt = opt;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = _opt.HttpTimeout;
        _handshakePipeline = BuildHandshakePipeline();
        _ownsHttpClient = true;
    }

    public GeminiApiClient(GeminiOptions opt, HttpClient http)
    {
        _opt = opt;
        _http = http;
        _ownsHttpClient = false; // 외부 주입된 HttpClient의 수명은 호출자가 관리
        // Respect externally provided client timeout; override only if default
        if (_http.Timeout == default)
            _http.Timeout = _opt.HttpTimeout;
        _handshakePipeline = BuildHandshakePipeline();
    }

    private string BuildEndpoint()
    {
        return _opt.Provider switch
        {
            GeminiProvider.AiGoogle =>
                $"https://generativelanguage.googleapis.com/v1beta/models/{_opt.Model}:streamGenerateContent?alt=sse&key={_opt.ApiKey}",
            GeminiProvider.VertexAI =>
                $"https://{_opt.Location}-aiplatform.googleapis.com/v1/projects/{_opt.ProjectId}/locations/{_opt.Location}/publishers/google/models/{_opt.Model}:streamGenerateContent?alt=sse",
            _ => throw new NotSupportedException()
        };
    }

    private HttpRequestMessage BuildRequest(string text)
    {
        var body = new
        {
            contents = new[] {
                new {
                    role = "user",
                    parts = new object[] { new { text } }
                }
            },
            systemInstruction = string.IsNullOrWhiteSpace(_opt.SystemInstruction)
                ? null
                : new { parts = new object[] { new { text = _opt.SystemInstruction } } },
            generationConfig = new
            {
                temperature = _opt.Temperature
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        if (_opt.Provider == GeminiProvider.VertexAI)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.AccessToken);
        }
        return req;
    }

    public async IAsyncEnumerable<string> StreamTranslateAsync(
        string text, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var req = BuildRequest(text);
        using (var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            if (_opt.HttpHandshakeTimeout > TimeSpan.Zero && _opt.HttpHandshakeTimeout != System.Threading.Timeout.InfiniteTimeSpan)
                sendCts.CancelAfter(_opt.HttpHandshakeTimeout);

            var res = await _handshakePipeline.ExecuteAsync(
                static async (state, token) =>
                {
                    var (http, request) = state;
                    return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                },
                (_http, req),
                sendCts.Token);

            res.EnsureSuccessStatusCode();

            var mediaType = res.Content.Headers?.ContentType?.MediaType;
            if (string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                await using var stream = await res.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                while ((line = await reader.ReadLineAsync()) is not null && !ct.IsCancellationRequested)
                {
                    var delta = SseParser.TryExtractTextDelta(line);
                    if (delta is { Length: > 0 })
                        yield return delta;
                }
                yield break;
            }
            else
            {
                // JSON 폴백: candidates[0].content.parts[].text를 합쳐 반환. 에러 형식이면 예외 승격.
                var json = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    string msg = err.TryGetProperty("message", out var em) ? (em.GetString() ?? "Unknown error") : err.ToString();
                    throw new HttpRequestException($"Gemini JSON error: {msg}");
                }

                string combined = string.Empty;
                if (doc.RootElement.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 0)
                {
                    var cand = cands[0];
                    if (cand.TryGetProperty("content", out var content))
                    {
                        if (content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var part in parts.EnumerateArray())
                            {
                                if (part.TryGetProperty("text", out var t))
                                {
                                    var s = t.GetString();
                                    if (!string.IsNullOrEmpty(s)) combined += s;
                                }
                            }
                        }
                        else if (content.ValueKind == JsonValueKind.String)
                        {
                            combined = content.GetString() ?? string.Empty;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(combined))
                {
                    // 비-SSE이므로 한 번에 전체 텍스트를 전달
                    yield return combined;
                }
                yield break;
            }
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
    private ResiliencePipeline<HttpResponseMessage> BuildHandshakePipeline()
    {
        var options = new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = _opt.RetryMaxAttempts,
            Delay = _opt.RetryBaseDelay,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500),
            OnRetry = args =>
            {
                int? code = null;
                if (args.Outcome.Result is HttpResponseMessage res)
                    code = (int)res.StatusCode;
                _opt.OnHandshakeRetry?.Invoke(args.AttemptNumber, args.RetryDelay, code);
                return ValueTask.CompletedTask;
            }
        };
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(options)
            .Build();
    }
}
