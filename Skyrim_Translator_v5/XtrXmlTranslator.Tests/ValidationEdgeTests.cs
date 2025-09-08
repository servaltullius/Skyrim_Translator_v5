using FluentAssertions;
using XtrXmlTranslator.Core.Validation;
using Xunit;

public class ValidationEdgeTests
{
    [Fact]
    public void Printf_Type_Mismatch_Is_Detected()
    {
        var src = "%d items";
        var tgt = "%s items";
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderTypeMismatch);
    }

    [Fact]
    public void Printf_Detail_Diff_Is_Detected()
    {
        var src = "%1$04.2f";
        var tgt = "%04.2f";
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderDetailMismatch);
    }

    [Fact]
    public void Percent_Literal_Mismatch_Is_Detected()
    {
        var src = "100%% success";
        var tgt = "100% success";
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PercentLiteralBroken);
    }

    [Fact]
    public void Brace_Order_Mismatch_Is_Detected()
    {
        var src = "{user} logged in {count}";
        var tgt = "{count} logged in {user}";
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderOrderMismatch);
    }

    [Fact]
    public void Missing_Placeholder_Is_Detected()
    {
        var src = "Hello {name}";
        var tgt = "Hello";
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderMissing);
    }
}
