using FluentAssertions;
using XtrXmlTranslator.Core.Glossary;
using Xunit;

public class GlossaryApplyOverlapTests
{
    [Fact]
    public void Protect_GlobalLongestFirst_Fallback_Applies_Correctly()
    {
        var list = new List<GlossaryEntry>
        {
            new GlossaryEntry { Source = "Imperial City", Type = GlossaryType.PROTECT, CaseSensitive = false },
            new GlossaryEntry { Source = "City", Type = GlossaryType.PROTECT, CaseSensitive = false },
        };
        var input = "Imperial City and City";
        var (masked, map) = GlossaryApply.MaskProtectedTerms(input, list);
        masked.Should().Contain("⟪T0⟫");
        masked.Should().Contain("⟪T1⟫");
        map.Should().ContainSingle(t => t.Original == "Imperial City");
        map.Should().ContainSingle(t => t.Original == "City");
    }

    [Fact]
    public void Replace_GlobalLongestFirst_Fallback_With_Enforce_Priority()
    {
        var list = new List<GlossaryEntry>
        {
            new GlossaryEntry { Source = "Imperial City", Target = "황실 도시", Type = GlossaryType.ENFORCE, CaseSensitive = false },
            new GlossaryEntry { Source = "City", Target = "도시", Type = GlossaryType.PREFER, CaseSensitive = false },
        };
        var output = GlossaryApply.ApplyPostReplace("Imperial City and another City", list);
        output.Should().Be("황실 도시 and another 도시");
    }
}
