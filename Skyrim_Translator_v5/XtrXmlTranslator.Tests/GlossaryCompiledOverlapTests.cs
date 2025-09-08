using FluentAssertions;
using XtrXmlTranslator.Core.Glossary;
using Xunit;

public class GlossaryCompiledOverlapTests
{
    [Fact]
    public void Protect_GlobalLongestFirst_Masks_Longest_Only()
    {
        var g = new GlossaryStore();
        g.Entries.Add(new GlossaryEntry { Source = "Imperial City", Type = GlossaryType.PROTECT, CaseSensitive = false });
        g.Entries.Add(new GlossaryEntry { Source = "City", Type = GlossaryType.PROTECT, CaseSensitive = false });
        var gc = GlossaryCompiled.Build(g);

        var input = "Imperial City is a City.";
        var (masked, map) = gc.MaskProtected(input);

        // 'Imperial City' should be masked as one token; the trailing 'City' should also be masked separately
        map.Should().ContainSingle(t => t.Original == "Imperial City");
        map.Should().ContainSingle(t => t.Original == "City");
        masked.Should().Contain("⟪T0⟫");
        masked.Should().Contain("⟪T1⟫");
    }

    [Fact]
    public void Replace_GlobalLongestFirst_With_Enforce_Priority()
    {
        var g = new GlossaryStore();
        g.Entries.Add(new GlossaryEntry { Source = "Imperial City", Target = "황실 도시", Type = GlossaryType.ENFORCE });
        g.Entries.Add(new GlossaryEntry { Source = "City", Target = "도시", Type = GlossaryType.PREFER });
        var gc = GlossaryCompiled.Build(g);

        var input = "Imperial City and another City";
        var output = gc.ApplyReplacement(input);

        output.Should().Be("황실 도시 and another 도시");
    }
}
