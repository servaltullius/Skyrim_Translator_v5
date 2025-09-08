using System.Collections.Concurrent;
using System.Text;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.TM;

namespace XtrXmlTranslator.Core.Translate;

public sealed class GeminiTranslator : ITranslator
{
    private readonly GeminiOptions _opt;
    private readonly ITmStore _tm;
    private readonly Func<GeminiApiClient> _clientFactory;

    public GeminiTranslator(GeminiOptions opt, ITmStore tm, Func<GeminiApiClient>? clientFactory = null)
    {
        _opt = opt; _tm = tm; _clientFactory = clientFactory ?? (() => new GeminiApiClient(_opt));
    }

    public async Task<IReadOnlyList<string>> TranslateSegmentsAsync(
        IReadOnlyList<XtrXmlTranslator.Core.Srx.TokenSegment> segments,
        Action<int, int, string>? onDelta = null,
        CancellationToken ct = default,
        Action<int, string>? onSegmentWarning = null,
        Action<int, string>? onSegmentError = null)
    {
        using var gate = new SemaphoreSlim(_opt.MaxConcurrency);
        var lastCalls = new ConcurrentQueue<DateTimeOffset>();
        TimeSpan rpmWindow = TimeSpan.FromMinutes(1);

        var results = new string[segments.Count];

        var tasks = segments.Select((seg, segIdx) => Task.Run(async () =>
        {
            try
            {
                using var client = _clientFactory();
                var runs = seg.Tokens.Where(t => t.Type == InlineTokenType.Text).ToList();
                var translatedRuns = new string[runs.Count];

                for (int runIdx = 0; runIdx < runs.Count; runIdx++)
                {
                    ct.ThrowIfCancellationRequested();
                    var src = runs[runIdx].Value ?? string.Empty;
                    if (string.IsNullOrEmpty(src)) { translatedRuns[runIdx] = src; continue; }

                    var tmKey = $"{_opt.Model}::{_opt.Temperature}::{src}";
                    if (_tm.TryGet(tmKey, out var cached))
                    {
                        translatedRuns[runIdx] = cached;
                        onDelta?.Invoke(segIdx, runIdx, cached);
                        continue;
                    }

                    await gate.WaitAsync(ct);
                    try
                    {
                        await ThrottleByRpmAsync(lastCalls, _opt.RequestsPerMinute, rpmWindow, ct, _opt.OnPaceWait);

                        var attempt = 0;
                        while (true)
                        {
                            try
                            {
                                var sb = new StringBuilder();
                                await foreach (var delta in client.StreamTranslateAsync(src, ct))
                                {
                                    sb.Append(delta);
                                    onDelta?.Invoke(segIdx, runIdx, delta);
                                }
                                var done = sb.ToString();
                                _tm.Put(tmKey, done);
                                translatedRuns[runIdx] = done;
                                break;
                            }
                            catch (HttpRequestException) when (attempt < 5)
                            {
                                var delay = Backoff.ExponentialJitter(attempt++,
                                    TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10));
                                await Task.Delay(delay, ct);
                            }
                        }
                        lastCalls.Enqueue(DateTimeOffset.UtcNow);
                    }
                    finally { gate.Release(); }
                }

                var q = new Queue<string>(translatedRuns);
                results[segIdx] = InlineTokenizer.Reassemble(seg.Tokens, q);
                try
                {
                    InlineTokenizer.AssertPlaceholdersUnchanged(
                        string.Concat(seg.Tokens.Select(t => t.Value)), results[segIdx]);
                }
                catch (Exception ex)
                {
                    onSegmentWarning?.Invoke(segIdx, ex.Message);
                }
            }
            catch (Exception ex)
            {
                onSegmentError?.Invoke(segIdx, ex.Message);
                // Fallback to original text for this segment
                results[segIdx] = string.Concat(seg.Tokens.Select(t => t.Value));
            }
        }, ct)).ToArray();

        await Task.WhenAll(tasks);
        return results;
    }

    private static async Task ThrottleByRpmAsync(ConcurrentQueue<DateTimeOffset> q, int rpm, TimeSpan window, CancellationToken ct, Action<TimeSpan>? onWait)
    {
        var now = DateTimeOffset.UtcNow;
        while (q.TryPeek(out var t) && (now - t) > window) q.TryDequeue(out _);
        if (q.Count >= rpm)
        {
            if (q.TryPeek(out var first))
            {
                var wait = window - (now - first);
                if (wait > TimeSpan.Zero)
                {
                    onWait?.Invoke(wait);
                    await Task.Delay(wait, ct);
                }
            }
        }
    }
}
