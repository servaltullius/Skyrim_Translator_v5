using System;
using System.Linq;
using XtrXmlTranslator.Core.Validation;

namespace XtrXmlTranslator.App.Services;

// Deprecated thin wrapper. Use XtrXmlTranslator.Core.Validation.Validator directly.
public static class PlaceholderValidator
{
    [Obsolete("Use XtrXmlTranslator.Core.Validation.Validator.Validate instead.")]
    public static (bool ok, string message) Validate(string source, string translation)
    {
        try
        {
            var issues = Validator.Validate(source, translation);
            if (issues.Count == 0) return (true, string.Empty);
            var msg = string.Join(" | ", issues.Select(i => i.Message));
            return (false, msg);
        }
        catch (Exception ex)
        {
            return (false, $"검증 오류: {ex.Message}");
        }
    }
}
