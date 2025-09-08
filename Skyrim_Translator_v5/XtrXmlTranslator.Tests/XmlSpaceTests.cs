using System.Text;
using FluentAssertions;
using XtrXmlTranslator.Core.Xml;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class XmlSpaceTests
{
    [Fact]
    public void PreserveWhitespace_With_xml_space_preserve_IsUnchanged()
    {
        var xml = """
        <root xml:space="preserve">
          <line>  앞 공백 보존</line>
          <line>끝공백 보존  </line>
          <line>개행\n보존</line>
        </root>
        """;
        var settings = XmlStreamReader.CreateSafeReaderSettings();
        using var sr = new System.IO.StringReader(xml);
        using var xr = System.Xml.XmlReader.Create(sr, settings);
        var sb = new StringBuilder();
        foreach (var (path, text) in XmlStreamReader.EnumerateLeafTexts(xr, _ => true))
        {
            sb.Append('[').Append(path).Append(']').Append(' ').Append(text).Append('\n');
        }
        var result = sb.ToString();
        result.Should().Contain("  앞 공백 보존");
        result.Should().Contain("끝공백 보존  ");
        result.Should().Contain("개행\\n보존");
    }

    [Fact]
    public void CRLF_Vs_LF_Preserved_In_TextTokens_Reassembly()
    {
        var text = "첫째 줄\r\n둘째 줄\n셋째 줄";
        var toks = InlineTokenizer.Tokenize(text);
        toks.Should().HaveCount(1);
        toks[0].Value.Should().Be(text);
    }
}
