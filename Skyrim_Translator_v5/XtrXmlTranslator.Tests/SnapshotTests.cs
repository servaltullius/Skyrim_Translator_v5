using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XtrXmlTranslator.Core.Xml;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;
using System.Text;
using VerifyXunit;

public class SnapshotTests
{
    [Fact]
    public void Xml_Entry_Text_Roundtrip_Baseline()
    {
        var xml = """
        <root xml:space="preserve">
          <Entry id="1"><Text>Hello &lt;Alias=Player&gt; %.0f</Text></Entry>
        </root>
        """;
        using var r = System.Xml.XmlReader.Create(new StringReader(xml), XmlStreamReader.CreateSafeReaderSettings());
        var segs = XmlStreamReader.EnumerateTexts(r, name => name == "Text").ToList();
        var toks = InlineTokenizer.Tokenize(segs[0].Text);
        var runs = InlineTokenizer.ExtractTranslatableRuns(toks).Select(x => $"[{x}]");
        var outText = InlineTokenizer.Reassemble(toks, new System.Collections.Generic.Queue<string>(runs));

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  Path: " + segs[0].Path + ",");
        sb.AppendLine("  Text: " + segs[0].Text + ",");
        sb.AppendLine("  outText: " + outText);
        sb.AppendLine("}");

        var expected = "{\r\n  Path: root/Entry/Text,\r\n  Text: Hello <Alias=Player> %.0f,\r\n  outText: [Hello ]<Alias=Player>[ ]%.0f\r\n}\r\n";
        Assert.Equal(expected, sb.ToString());
    }

    [Fact]
    public async Task Xml_Entry_Text_Roundtrip_Snapshot()
    {
        var xml = """
        <root xml:space="preserve">
          <Entry id="1"><Text>Hello &lt;Alias=Player&gt; %.0f</Text></Entry>
        </root>
        """;
        using var r = System.Xml.XmlReader.Create(new StringReader(xml), XmlStreamReader.CreateSafeReaderSettings());
        var segs = XmlStreamReader.EnumerateTexts(r, name => name == "Text").ToList();
        var toks = InlineTokenizer.Tokenize(segs[0].Text);
        var runs = InlineTokenizer.ExtractTranslatableRuns(toks).Select(x => $"[{x}]");
        var outText = InlineTokenizer.Reassemble(toks, new System.Collections.Generic.Queue<string>(runs));

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  Path: " + segs[0].Path + ",");
        sb.AppendLine("  Text: " + segs[0].Text + ",");
        sb.AppendLine("  outText: " + outText);
        sb.AppendLine("}");

        await Verifier.Verify(sb.ToString());
    }
}

