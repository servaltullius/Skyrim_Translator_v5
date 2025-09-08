using System.Linq;
using XtrXmlTranslator.App.ViewModels;
using XtrXmlTranslator.Core.Validation;

namespace XtrXmlTranslator.App.Services;

public interface IValidationOrchestrator
{
    void AutoFixRow(TranslationRowVM row);
    void RevalidateRow(TranslationRowVM row);
}

public sealed class ValidationOrchestrator : IValidationOrchestrator
{
    public void AutoFixRow(TranslationRowVM row)
    {
        var fixed1 = AutoFix.ReorderCkAtomicsLikeSource(row.SourceText, row.TranslationText);
        var fixed2 = AutoFix.RestorePercentLiterals(row.SourceText, fixed1);
        row.TranslationText = fixed2;
    }

    public void RevalidateRow(TranslationRowVM row)
    {
        var issues = Validator.Validate(row.SourceText ?? string.Empty, row.TranslationText ?? string.Empty);
        if (issues.Count == 0)
        {
            row.TagOk = true;
            row.Warning = string.Empty;
            if (row.Status == RowStatus.Error) row.Status = RowStatus.Edited;
        }
        else
        {
            row.TagOk = false;
            row.Warning = string.Join(" | ", issues.Select(i => i.Message));
            row.Status = RowStatus.Error;
        }
    }
}
