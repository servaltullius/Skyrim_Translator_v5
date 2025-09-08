using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.Validation;
using Xunit;

public class E2E_MockTranslator_Tests
{
    [Fact]
    public async Task Pipeline_Protect_Then_Enforce_With_Placeholders_Tags_Validation_OK()
    {
        // Source with tag and placeholders
        var src = "Hello <Alias=Player/> Dragonborn! Meet me at %s and bring {0}.";

        // Glossary: Protect and Enforce the same term
        var g = new GlossaryStore();
        g.Entries.Add(new GlossaryEntry { Source = "Dragonborn", Type = GlossaryType.PROTECT });
        g.Entries.Add(new GlossaryEntry { Source = "Dragonborn", Target = "용사", Type = GlossaryType.ENFORCE });
        var gc = GlossaryCompiled.Build(g);

        // Tokenize and mask protected terms on text tokens
        var tokens = InlineTokenizer.Tokenize(src);
        var map = new System.Collections.Generic.List<(string Token, string Original)>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type != InlineTokenType.Text) continue;
            var (masked, rowMap) = gc.MaskProtected(tokens[i].Value);
            if (rowMap.Count > 0)
            {
                tokens[i] = new InlineToken(InlineTokenType.Text, masked);
                map.AddRange(rowMap);
            }
        }

        // SRX segmentation
        var srxDoc = SrxLoader.LoadFromString(SrxPresets.EnDefault);
        var engine = SrxCompiler.Compile(srxDoc, "en-US");
        var segs = SrxSegmenter.SegmentTokens(tokens, engine);

        // Translate with MockTranslator (echo)
        ITranslator translator = new MockTranslator();
        var outputs = await translator.TranslateSegmentsAsync(segs);
        var combined = string.Concat(outputs);

        // Unmask then apply replacements
        var unmasked = GlossaryApply.UnmaskProtectedTerms(combined, map);
        var final = gc.ApplyReplacement(unmasked);

        // Validate placeholders/tags
        var issues = Validator.Validate(src, final);
        issues.Should().BeEmpty();
        final.Should().Contain("용사").And.NotContain("Dragonborn");
        final.Should().Contain("<Alias=Player/>");
    }

    [Fact]
    public async Task Pipeline_Ko_CJK_Segmentation_With_Tag_And_Replacement_OK()
    {
        var src = "안녕하세요。여기는 <b/> 스카이림 입니다。잘 부탁합니다。";
        var g = new GlossaryStore();
        g.Entries.Add(new GlossaryEntry { Source = "스카이림", Type = GlossaryType.PROTECT });
        g.Entries.Add(new GlossaryEntry { Source = "스카이림", Target = "Skyrim", Type = GlossaryType.ENFORCE });
        var gc = GlossaryCompiled.Build(g);

        var tokens = InlineTokenizer.Tokenize(src);
        var map = new System.Collections.Generic.List<(string Token, string Original)>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type != InlineTokenType.Text) continue;
            var (masked, rowMap) = gc.MaskProtected(tokens[i].Value);
            if (rowMap.Count > 0) { tokens[i] = new InlineToken(InlineTokenType.Text, masked); map.AddRange(rowMap); }
        }

        var srxDoc = SrxLoader.LoadFromString(SrxPresets.KoDefault);
        var engine = SrxCompiler.Compile(srxDoc, "ko-KR");
        var segs = SrxSegmenter.SegmentTokens(tokens, engine);

        ITranslator translator = new MockTranslator();
        var outputs = await translator.TranslateSegmentsAsync(segs);
        var combined = string.Concat(outputs);

        var unmasked = GlossaryApply.UnmaskProtectedTerms(combined, map);
        var final = gc.ApplyReplacement(unmasked);

        var issues = Validator.Validate(src, final);
        issues.Should().BeEmpty();
        final.Should().Contain("Skyrim");
        final.Should().Contain("<b/>");
    }
}
