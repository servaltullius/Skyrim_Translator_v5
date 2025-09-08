using FluentAssertions;
using XtrXmlTranslator.Core.Validation;
using Xunit;

namespace XtrXmlTranslator.Tests;

public class ValidationPrintfTests
{
    [Fact]
    public void Detects_Type_And_Order_Differences()
    {
        var src = "Value: %d %s";
        var tgt = "Value: %s %d";
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderTypeMismatch);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderOrderMismatch);
    }

    [Fact]
    public void Detects_Detail_Differences_Flags_Width_Precision()
    {
        var src = "Num: %+08.2f";
        var tgt = "Num: %8.2f"; // flags differ (+0 missing)
        var issues = Validator.Validate(src, tgt);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderDetailMismatch);
    }
}

