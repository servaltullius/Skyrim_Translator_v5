using System.Collections.Generic;
using XtrXmlTranslator.Core.Models;

namespace XtrXmlTranslator.Core.Segmenting
{
    /// <summary>
    /// SRX 규칙 기반 세그먼터(스텁). 현재는 전체 텍스트를 1개 세그먼트로 반환.
    /// </summary>
    public sealed class SrxSegmenter : ISegmenter
    {
        public IEnumerable<Segment> Segment(string text)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            yield return new Segment(text, 0);
        }
    }
}
