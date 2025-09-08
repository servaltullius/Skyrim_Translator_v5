using FluentAssertions;
using XtrXmlTranslator.Core.Glossary;
using Xunit;

public class GlossaryBoundaryTests
{
    [Fact]
    public void DefaultBoundary_Allows_Foo_Match_In_Foo_Underscore_Bar()
    {
        var store = new GlossaryStore();
        store.Entries.Add(new GlossaryEntry { Source = "foo", Target = "FOO", Type = GlossaryType.ENFORCE });
        var input = "Visit foo_bar today";
        var output = GlossaryApply.ApplyPostReplace(input, store.Entries);
        output.Should().Be("Visit FOO_bar today");
    }

    [Fact]
    public void CustomWordChars_Underscore_Disallows_Foo_Match_In_Foo_Underscore_Bar()
    {
        var prev = GlossaryBoundary.Current;
        try
        {
            GlossaryBoundary.SetConfig(new GlossaryBoundaryConfig("_"));
            var store = new GlossaryStore();
            store.Entries.Add(new GlossaryEntry { Source = "foo", Target = "FOO", Type = GlossaryType.ENFORCE });
            var input = "Visit foo_bar today";
            var output = GlossaryApply.ApplyPostReplace(input, store.Entries);
            output.Should().Be("Visit foo_bar today");
        }
        finally
        {
            GlossaryBoundary.SetConfig(prev);
        }
    }
}

