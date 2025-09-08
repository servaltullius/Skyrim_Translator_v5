using System;
using System.Net;
using System.Net.Http;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;

namespace XtrXmlTranslator.App.Services;

public static class ResilientHttp
{
    public static HttpClient Create(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            EnableMultipleHttp2Connections = true
        };
        var client = new HttpClient(handler)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
        return client;
    }
}

public sealed class ResilientGeminiFactory : ITranslatorFactory
{
    public ITranslator Create(GeminiOptions options, ITmStore tm)
    {
        // Use IHttpClientFactory + Resilience pipeline
        var http = HttpClientHost.Factory.CreateClient("GeminiApi");
        // 스트리밍 본문에 대한 타임아웃은 무한으로 설정(핸드셰이크 타임아웃은 GeminiApiClient에서 별도 관리)
        http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        return new GeminiTranslator(options, tm, clientFactory: () => new GeminiApiClient(options, http));
    }
}

