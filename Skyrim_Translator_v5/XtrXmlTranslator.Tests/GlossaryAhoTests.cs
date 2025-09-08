using FluentAssertions;
using XtrXmlTranslator.Core.Glossary;
using Xunit;

public class GlossaryAhoTests
{
    [Fact]
    public void Protect_Masks_With_Boundaries_And_Case()
    {
        var store = new GlossaryStore();
        store.Entries.Add(new GlossaryEntry { Source = "Skyrim", Type = GlossaryType.PROTECT, CaseSensitive = true });
        store.Entries.Add(new GlossaryEntry { Source = "septim", Type = GlossaryType.PROTECT, CaseSensitive = false });
        var input = "Welcome to Skyrim with 100 septim coins and Dragonborn.";
        var (masked, map) = GlossaryApply.MaskProtectedTerms(input, store.Entries);
        masked.Should().Contain("⟪T0⟫");
        masked.Should().Contain("⟪T1⟫");
        map.Should().Contain(t => t.Original == "Skyrim");
        map.Should().Contain(t => t.Original.ToLowerInvariant() == "septim");
        // boundary: 'Dragon' should not match inside 'Dragonborn' if added
        store.Entries.Add(new GlossaryEntry { Source = "Dragon", Type = GlossaryType.PROTECT, CaseSensitive = false });
        var (masked2, map2) = GlossaryApply.MaskProtectedTerms("dragon dragonborn dragon.", store.Entries);
        masked2.Should().Contain("⟪T"); // at least one
        // 'dragon' must not match inside 'dragonborn' -> 'dragonborn' remains unchanged
        masked2.Should().Contain("dragonborn");
    }

    [Fact]
    public void Post_Replace_LongestFirst_And_Enforce_Priority()
    {
        var list = new List<GlossaryEntry>
        {
            new GlossaryEntry { Source = "Dragon", Target = "드래곤", Type = GlossaryType.PREFER, CaseSensitive = false },
            new GlossaryEntry { Source = "Dragonborn", Target = "드래곤본", Type = GlossaryType.ENFORCE, CaseSensitive = false },
        };
        var output = GlossaryApply.ApplyPostReplace("Dragonborn vs Dragon", list);
        output.Should().Contain("드래곤본 vs 드래곤");
    }
}
