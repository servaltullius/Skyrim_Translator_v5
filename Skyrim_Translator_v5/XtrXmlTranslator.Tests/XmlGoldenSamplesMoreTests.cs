using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerifyXunit;
using XtrXmlTranslator.Core.Xml;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class XmlGoldenSamplesMoreTests
{
    [Fact]
    public async Task Xml_Golden_Preserve_Multiple_Spaces_Snapshot()
    {
        var xml = """
<root xml:space="preserve">
  <Entry id="1"><Text>  Hello &lt;b/&gt;   world  </Text></Entry>
</root>
""";
        using var r = System.Xml.XmlReader.Create(new StringReader(xml), XmlStreamReader.CreateSafeReaderSettings());
        var segs = XmlStreamReader.EnumerateTexts(r, name => name == "Text").ToList();

        var sb = new StringBuilder();
        foreach (var s in segs)
        {
            var toks = InlineTokenizer.Tokenize(s.Text);
            var runs = InlineTokenizer.ExtractTranslatableRuns(toks).Select(x => $"[{x}]");
            var outText = InlineTokenizer.Reassemble(toks, new System.Collections.Generic.Queue<string>(runs));

            sb.AppendLine("{");
            sb.AppendLine("  Path: " + s.Path + ",");
            sb.AppendLine("  Text: " + s.Text + ",");
            sb.AppendLine("  outText: " + outText);
            sb.AppendLine("}");
        }

        await Verifier.Verify(sb.ToString());
    }

    [Fact]
    public async Task Xml_Golden_Enumerate_LeafTexts_Snapshot()
    {
        var xml = """
<root xml:space="preserve">
  <Paragraph>
    <Run>Alpha &lt;Alias=Player&gt;</Run>
    <Run> %d and {0} </Run>
    <Span>
      <Inner><![CDATA[Beta <Alias=Dragon>]]></Inner>
    </Span>
  </Paragraph>
</root>
""";
        using var r = System.Xml.XmlReader.Create(new StringReader(xml), XmlStreamReader.CreateSafeReaderSettings());
        var segs = XmlStreamReader.EnumerateLeafTexts(r, _ => true).ToList();

        var sb = new StringBuilder();
        foreach (var s in segs)
        {
            var toks = InlineTokenizer.Tokenize(s.Text);
            var runs = InlineTokenizer.ExtractTranslatableRuns(toks).Select(x => $"[{x}]");
            var outText = InlineTokenizer.Reassemble(toks, new System.Collections.Generic.Queue<string>(runs));

            sb.AppendLine("{");
            sb.AppendLine("  Path: " + s.Path + ",");
            sb.AppendLine("  Text: " + s.Text + ",");
            sb.AppendLine("  outText: " + outText);
            sb.AppendLine("}");
        }

        await Verifier.Verify(sb.ToString());
    }
}

