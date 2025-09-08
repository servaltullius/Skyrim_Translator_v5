using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerifyXunit;
using XtrXmlTranslator.Core.Xml;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class XmlGoldenSamplesTests
{
    [Fact]
    public async Task Xml_Golden_Entities_And_Cdata_Roundtrip_Snapshot()
    {
        var xml = """
<root xml:space="preserve">
  <Entry id="1"><Text>Hello &lt;Alias=Player&gt; %1$d and {0:N2} &amp; %{name}</Text></Entry>
  <Entry id="2"><Text><![CDATA[Hola <Alias=Dragon> %.2f and {1:D3} & %{who}]]></Text></Entry>
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
}

