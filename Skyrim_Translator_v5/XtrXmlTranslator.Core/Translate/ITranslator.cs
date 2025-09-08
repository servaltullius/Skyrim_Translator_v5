namespace XtrXmlTranslator.Core.Translate;

public interface ITranslator
{
    Task<IReadOnlyList<string>> TranslateSegmentsAsync(
        IReadOnlyList<XtrXmlTranslator.Core.Srx.TokenSegment> segments,
        Action<int, int, string>? onDelta = null,
        CancellationToken ct = default,
        Action<int, string>? onSegmentWarning = null,
        Action<int, string>? onSegmentError = null);
}
