using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Srx;

namespace XtrXmlTranslator.Core.Translate;

// Simple mock translator that echoes the input text, suitable for tests/offline demo
public sealed class MockTranslator : ITranslator
{
    public async Task<IReadOnlyList<string>> TranslateSegmentsAsync(
        IReadOnlyList<TokenSegment> segments,
        Action<int, int, string>? onDelta = null,
        CancellationToken ct = default,
        Action<int, string>? onSegmentWarning = null,
        Action<int, string>? onSegmentError = null)
    {
        var results = new string[segments.Count];
        for (int i = 0; i < segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var text = string.Concat(segments[i].Tokens.Select(t => t.Value));
            onDelta?.Invoke(i, 0, text);
            results[i] = text;
            await Task.Yield();
        }
        return results;
    }
}
