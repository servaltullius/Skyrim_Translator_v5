using FluentAssertions;
using XtrXmlTranslator.Core.Translate;
using Xunit;

public class GeminiSseParserTests
{
    [Fact]
    public void Parse_Sse_Deltas()
    {
        var l1 = "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"안\"}]}}]}";
        var l2 = "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"녕하세요\"}]}}]}";
        SseParser.TryExtractTextDelta(l1).Should().Be("안");
        SseParser.TryExtractTextDelta(l2).Should().Be("녕하세요");
    }
}
