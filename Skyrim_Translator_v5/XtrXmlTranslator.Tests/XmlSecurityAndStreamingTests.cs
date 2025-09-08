using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using FluentAssertions;
using XtrXmlTranslator.Core.Xml;
using Xunit;

public class XmlSecurityAndStreamingTests
{
    [Fact]
    public void Xxe_Is_Prohibited_By_SafeSettings()
    {
        var xml = """
        <!DOCTYPE root [
          <!ENTITY xxe SYSTEM "file:///etc/passwd">
        ]>
        <root><Text>&xxe;</Text></root>
        """;
        var settings = XmlStreamReader.CreateSafeReaderSettings();
        using var r = XmlReader.Create(new StringReader(xml), settings);
        Action act = () => XmlStreamReader.EnumerateLeafTexts(r, _ => true).ToList();
        act.Should().Throw<XmlException>();
    }

    [Fact]
    public void XmlSpace_Preserve_Is_Respected()
    {
        var xml = """
        <root xml:space="preserve">
          <Text> Hello  world </Text>
        </root>
        """;
        using var r = XmlReader.Create(new StringReader(xml), XmlStreamReader.CreateSafeReaderSettings());
        var items = XmlStreamReader.EnumerateLeafTexts(r, name => name == "Text").ToList();
        items.Count.Should().Be(1);
        items[0].Text.Should().Be(" Hello  world ");
    }

    [Fact]
    public void LeafStreaming_Handles_Thousand_Items()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<root>");
        for (int i = 0; i < 1000; i++) sb.AppendLine($"  <Text>item-{i}</Text>");
        sb.AppendLine("</root>");
        using var r = XmlReader.Create(new StringReader(sb.ToString()), XmlStreamReader.CreateSafeReaderSettings());
        var items = XmlStreamReader.EnumerateLeafTexts(r, _ => true).ToList();
        items.Count.Should().Be(1000);
        items.Take(3).Select(x => x.Text).Should().BeEquivalentTo(new[] { "item-0", "item-1", "item-2" }, o => o.WithStrictOrdering());
    }
}
