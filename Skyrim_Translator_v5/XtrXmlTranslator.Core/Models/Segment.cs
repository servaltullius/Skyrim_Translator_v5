using System;

namespace XtrXmlTranslator.Core.Models
{
    /// <summary>
    /// 번역 단위가 되는 텍스트 세그먼트의 최소 표현.
    /// </summary>
    public sealed record Segment(string Text, int Index);
}
