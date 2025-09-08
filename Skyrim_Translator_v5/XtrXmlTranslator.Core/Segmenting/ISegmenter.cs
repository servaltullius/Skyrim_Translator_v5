using System.Collections.Generic;
using XtrXmlTranslator.Core.Models;

namespace XtrXmlTranslator.Core.Segmenting
{
    /// <summary>
    /// 텍스트를 세그먼트로 분할하는 인터페이스(SRX 등 구현체 제공 예정).
    /// </summary>
    public interface ISegmenter
    {
        IEnumerable<Segment> Segment(string text);
    }
}
