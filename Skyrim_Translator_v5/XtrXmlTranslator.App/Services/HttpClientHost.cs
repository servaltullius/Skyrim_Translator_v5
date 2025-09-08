using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Resilience;
using Polly;
using Polly.Retry;

namespace XtrXmlTranslator.App.Services;

public static class HttpClientHost
{
    private static readonly Lazy<IServiceProvider> _provider = new(() => BuildServices());
    public static IHttpClientFactory Factory => _provider.Value.GetRequiredService<IHttpClientFactory>();

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("GeminiApi")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                EnableMultipleHttp2Connections = true
            })
            // Resilience pipeline: retry with exponential backoff + jitter. Timeout is handled in handshake by GeminiApiClient.
            .AddResilienceHandler("gemini-pipeline", builder =>
            {
                var maxAttempts = TryParseInt(Environment.GetEnvironmentVariable("XTRANS_RETRY_MAX"), 5);
                var baseMs = TryParseInt(Environment.GetEnvironmentVariable("XTRANS_RETRY_BASEMS"), 500);
                builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = maxAttempts,
                    Delay = TimeSpan.FromMilliseconds(baseMs),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                                        .Handle<HttpRequestException>()
                                        .HandleResult(r => (int)r.StatusCode == 429 || (int)r.StatusCode >= 500)
                });
                // NOTE: No Timeout strategy here to avoid interrupting long-running SSE streams.
            });
        return services.BuildServiceProvider();
    }

    private static int TryParseInt(string? s, int def) => int.TryParse(s, out var v) ? v : def;
}
