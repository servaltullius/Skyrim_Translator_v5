namespace XtrXmlTranslator.Core.Prompt;

public sealed class PromptConfig
{
    public string BaseVec { get; set; } =
@"[vec 기본]
- 게임 로컬라이제이션 톤으로 자연스럽고 간결하게 번역.
- 줄바꿈/공백/문장부호/기호 유지.
- 숫자/단위/형식은 현지 표준.
- (중요) 플레이스홀더/변수/태그는 존재하지 않는다고 가정하고 문맥만 번역.";

    public string? UserCustom { get; set; }
    public bool InjectGlossarySummary { get; set; } = true;
}
