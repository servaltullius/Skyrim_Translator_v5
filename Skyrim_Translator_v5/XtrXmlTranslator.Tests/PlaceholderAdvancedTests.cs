using FluentAssertions;
using XtrXmlTranslator.Core.Validation;
using Xunit;

namespace XtrXmlTranslator.Tests;

public class PlaceholderAdvancedTests
{
    [Fact]
    public void Printf_Positional_With_Detail_Mismatch_Detected()
    {
        var src = "%2$+08.2f %1$s end";
        var tgt = "%2$8.2f %1$s end"; // flags '+' and '0' missing on first placeholder
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderDetailMismatch);
        issues.Should().NotContain(i => i.Kind == IssueKind.PlaceholderTypeMismatch);
    }

    [Fact]
    public void Printf_Positional_Order_Type_Mismatch_Detected()
    {
        var src = "%1$s %2$d";
        var tgt = "%2$d %1$s"; // swapped positions leads to type sequence diff
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderOrderMismatch);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderTypeMismatch);
    }

    [Fact]
    public void Brace_With_Format_Order_Mismatch_Detected()
    {
        var src = "Score {0:N2} of {1:D3}";
        var tgt = "Score {1:D3} of {0:N2}";
        var issues = Validator.Validate(src, tgt);
        // Validator checks order/name equality for brace placeholders (format detail not enforced)
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderOrderMismatch);
    }

    [Fact]
    public void PercentBrace_Named_Order_Mismatch_Detected()
    {
        var src = "Hello %{user}, you have %{count}";
        var tgt = "Hello %{count}, you have %{user}";
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderOrderMismatch);
    }

    [Fact]
    public void Mixed_Tags_And_Placeholders_Missing_Is_Detected()
    {
        var src = "<b>%d</b> and {0} with %{name}";
        var tgt = "<b>%d</b> and {0}"; // %{name} missing
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderMissing);
        // Tag order must remain same
        issues.Should().NotContain(i => i.Kind == IssueKind.TagOrderMismatch);
    }
}

