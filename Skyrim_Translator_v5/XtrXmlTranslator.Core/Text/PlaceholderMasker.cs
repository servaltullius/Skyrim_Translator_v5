namespace XtrXmlTranslator.Core.Text
{
    /// <summary>
    /// %s, %d, {0}, {1}, 그리고 간단한 <tag> 패턴 등 비번역 토큰을 마스킹/복원하기 위한 스텁.
    /// 실제 구현은 추후 정교한 정규식 및 토큰 테이블 사용으로 대체.
    /// </summary>
    public interface IPlaceholderMasker
    {
        string Mask(string input);
        string Unmask(string masked);
    }

    public sealed class PlaceholderMasker : IPlaceholderMasker
    {
        public string Mask(string input)
        {
            // TODO: 실제 마스킹 로직 구현
            return input ?? string.Empty;
        }

        public string Unmask(string masked)
        {
            // TODO: 실제 복원 로직 구현
            return masked ?? string.Empty;
        }
    }
}
