using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using FluentAssertions;
using XtrXmlTranslator.Core.Tokenizing;
using XtrXmlTranslator.Core.Xml;
using Xunit;

public class InlineTokenizerTests
{
    [Fact]
    public void Tokenize_Splits_Tags_And_Placeholders()
    {
        var s = "Find 20 nirnroot for <Alias=Player> (%.0f/%.0f) and {0}!";
        var toks = InlineTokenizer.Tokenize(s);

        toks.Should().Contain(t => t.Type == InlineTokenType.XmlLikeTag && t.Value.StartsWith("<Alias="));
        toks.Should().Contain(t => t.Type == InlineTokenType.PercentPlaceholder && t.Value == "%.0f");
        toks.Should().Contain(t => t.Type == InlineTokenType.BracePlaceholder && t.Value == "{0}");
        string recomposed = InlineTokenizer.Reassemble(toks, new Queue<string>(InlineTokenizer.ExtractTranslatableRuns(toks)));
        recomposed.Should().Be(s);
    }

    [Fact]
    public void XmlReader_ResolvesEntities_And_PreservesSpace()
    {
        var xml = @"<root xml:space=""preserve"">
  <Entry id=""1"">
    <Text>Hello &lt;Alias=Player&gt;, you found %.0f gold!
Line2
</Text>
  </Entry>
</root>";

        using var r = XmlReader.Create(new StringReader(xml), XmlStreamReader.CreateSafeReaderSettings());
        var segs = XmlStreamReader.EnumerateTexts(r, name => name == "Text").ToList();

        segs.Should().HaveCount(1);
        segs[0].Text.Should().Contain("Hello <Alias=Player>, you found %.0f gold!");
        segs[0].Text.Should().Contain("Line2");
        var toks = InlineTokenizer.Tokenize(segs[0].Text);
        var translated = new Queue<string>(InlineTokenizer.ExtractTranslatableRuns(toks).Select(x => $"[{x}]"));
        var outText = InlineTokenizer.Reassemble(toks, translated);

        InlineTokenizer.AssertPlaceholdersUnchanged(segs[0].Text, outText);
    }
}

