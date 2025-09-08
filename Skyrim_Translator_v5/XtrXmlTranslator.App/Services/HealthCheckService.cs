using System;
using System.Threading;
using System.Threading.Tasks;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;

namespace XtrXmlTranslator.App.Services;

public static class HealthCheckService
{
    public static async Task<(bool ok, string message)> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var secrets = new CombinedSecretsProvider();
            var key = secrets.Get("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) return (false, "GEMINI_API_KEY 미설정");

            var opt = new GeminiOptions
            {
                ApiKey = key,
                Model = "gemini-2.5-flash",
                Temperature = 0.1,
                HttpHandshakeTimeout = TimeSpan.FromSeconds(8)
            };
            var factory = new ResilientGeminiFactory();
            var translator = factory.Create(opt, new InMemoryTm());

            var seg = new TokenSegment();
            seg.Tokens.Add(new InlineToken(InlineTokenType.Text, "health check"));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            var res = await translator.TranslateSegmentsAsync(new[] { seg }, ct: cts.Token);
            return (true, "SSE OK");
        }
        catch (OperationCanceledException)
        {
            return (false, "Timeout");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
