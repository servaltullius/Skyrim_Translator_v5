using System.IO;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using XtrXmlTranslator.App.Services;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Prompt;

namespace XtrXmlTranslator.Tests.Services;

public class ConfigServiceTests
{
    [Fact]
    public void SavePrompt_Then_Load_Roundtrip()
    {
        var svc = new ConfigService();
        var p = new PromptConfig { BaseVec = "base-vec-test", UserCustom = "user-custom", InjectGlossarySummary = false };
        svc.SavePrompt(p);

        var (loaded, _) = svc.Load();
        loaded.BaseVec.Should().Be("base-vec-test");
        loaded.UserCustom.Should().Be("user-custom");
        loaded.InjectGlossarySummary.Should().BeFalse();
    }

    [Fact]
    public void SaveGlossary_Then_Load_Roundtrip()
    {
        var svc = new ConfigService();
        var g = new GlossaryStore();
        g.Entries.Add(new GlossaryEntry { Source = "Foo", Target = "Bar", Type = GlossaryType.ENFORCE, CaseSensitive = true, Notes = "n1" });
        g.Entries.Add(new GlossaryEntry { Source = "X", Target = "Y", Type = GlossaryType.PREFER });

        svc.SaveGlossary(g);
        var (_, loadedG) = svc.Load();
        loadedG.Entries.Count.Should().BeGreaterThanOrEqualTo(2);
        loadedG.Entries.Any(e => e.Source == "Foo" && e.Target == "Bar" && e.CaseSensitive).Should().BeTrue();
    }
}

