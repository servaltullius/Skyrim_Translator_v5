namespace XtrXmlTranslator.Core.Translate;

internal static class Backoff
{
    private static readonly Random _rng = new();

    public static TimeSpan ExponentialJitter(int attempt,
        TimeSpan baseDelay, TimeSpan maxDelay)
    {
        var exp = Math.Min(maxDelay.TotalMilliseconds,
                           baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
        var jitter = _rng.NextDouble() * exp * 0.25; // 25% 지터
        return TimeSpan.FromMilliseconds(Math.Min(exp + jitter, maxDelay.TotalMilliseconds));
    }
}
