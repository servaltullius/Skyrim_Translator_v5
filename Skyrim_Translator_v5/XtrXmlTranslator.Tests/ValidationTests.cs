using FluentAssertions;
using XtrXmlTranslator.Core.Validation;
using Xunit;

namespace XtrXmlTranslator.Tests;

public class ValidationTests
{
    [Fact]
    public void PercentAndCkTags_MustMatch()
    {
        var src = "Find 20 nirnroot for <Alias=Player> (%.0f/%.0f) %% Ready?";
        var ok = "Find 20 nirnroot for <Alias=Player> (%.0f/%.0f) %% Ready?";
        var bad = "Find 20 nirnroot for <Alias=Player> (%.0f) % Ready?";

        Validator.Validate(src, ok).Should().BeEmpty();
        var issues = Validator.Validate(src, bad);
        issues.Should().Contain(i => i.Kind == IssueKind.PlaceholderMissing);
        issues.Should().Contain(i => i.Kind == IssueKind.PercentLiteralBroken);
    }

    [Fact]
    public void ReorderCkAtomicsLikeSource_FixesReorderedTags()
    {
        var src = "A <Alias=Bob> ate <Global=Count> apples.";
        var tgt = "A <Global=Count> ate <Alias=Bob> apples.";
        var fixedTgt = AutoFix.ReorderCkAtomicsLikeSource(src, tgt);
        fixedTgt.Should().Be(src);
    }

    [Fact]
    public void TagNesting_MustRemainValid()
    {
        var src = "<b>Hello</b> world";
        var tgt = "<b>Hello</b> world";
        Validator.Validate(src, tgt).Should().BeEmpty();

        var broken = "<b>Hello</i> world";
        var issues = Validator.Validate(src, broken);
        issues.Should().Contain(i => i.Kind == IssueKind.TagNestingError);
    }
}
