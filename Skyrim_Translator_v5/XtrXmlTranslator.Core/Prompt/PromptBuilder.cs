using System.Text;

namespace XtrXmlTranslator.Core.Prompt;

public static class PromptBuilder
{
    public static string BuildSystemInstruction(
        PromptConfig cfg,
        IReadOnlyCollection<(string Source, string Target, string Type)>? glossarySummary = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(cfg.BaseVec.Trim());
        if (!string.IsNullOrWhiteSpace(cfg.UserCustom))
        {
            sb.AppendLine();
            sb.AppendLine("[사용자 지시]");
            sb.AppendLine(cfg.UserCustom!.Trim());
        }
        if (cfg.InjectGlossarySummary && glossarySummary is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("[용어 정책 요약 — 현재 배치에 등장하는 항목만]");
            foreach (var (src, tgt, type) in glossarySummary!)
            {
                if (type == "PROTECT")
                    sb.AppendLine($"- 번역 금지: \"{src}\" → 그대로 유지");
                else if (type == "ENFORCE")
                    sb.AppendLine($"- 강제 번역: \"{src}\" → \"{tgt}\"");
                else if (type == "PREFER")
                    sb.AppendLine($"- 권고 번역: \"{src}\" → \"{tgt}\" (가능하면 사용)");
            }
        }
        sb.AppendLine("[보존 규칙 — 반드시 준수]");
        sb.AppendLine("- ⟪T#⟫ 보호 토큰은 절대 수정/삭제/번역하지 말 것.");
        sb.AppendLine("- 모든 XML 유사 태그(<...>)는 개수/순서/속성까지 원문과 동일하게 보존. 추가/삭제/변형 금지.");
        sb.AppendLine("- 자리표시자(%d, %s, {0}, %{name} 등)는 원문과 동일한 개수/순서/형식으로 보존. 절대 변형 금지.");
        return sb.ToString();
    }
}
