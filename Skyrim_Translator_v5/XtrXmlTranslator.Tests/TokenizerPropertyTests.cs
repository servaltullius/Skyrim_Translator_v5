using System;
using System.Linq;
using System.Text;
using FluentAssertions;
using XtrXmlTranslator.Core.Tokenizing;
using Xunit;

public class TokenizerPropertyTests
{
    [Fact]
    public void Randomized_Tokenize_Reassemble_Is_Identity()
    {
        var rand = new Random(42);
        string[] tags = { "<b>", "</b>", "<i>", "</i>", "<br/>", "<Alias=Player>", "<Global=Gold>" };
        string[] braces = { "{0}", "{Name}", "%{username}" };
        string[] printf = { "%.0f", "%d", "%s" };

        for (int t = 0; t < 64; t++)
        {
            int parts = rand.Next(3, 30);
            var sb = new StringBuilder();
            for (int i = 0; i < parts; i++)
            {
                int pick = rand.Next(4);
                switch (pick)
                {
                    case 0:
                        sb.Append('a', rand.Next(1, 12));
                        break;
                    case 1:
                        sb.Append(tags[rand.Next(tags.Length)]);
                        break;
                    case 2:
                        sb.Append(braces[rand.Next(braces.Length)]);
                        break;
                    default:
                        sb.Append(printf[rand.Next(printf.Length)]);
                        break;
                }
                sb.Append(' ');
            }
            var s = sb.ToString();
            var toks = InlineTokenizer.Tokenize(s);
            var runs = new Queue<string>(InlineTokenizer.ExtractTranslatableRuns(toks));
            var outText = InlineTokenizer.Reassemble(toks, runs);
            InlineTokenizer.AssertPlaceholdersUnchanged(s, outText);
            outText.Should().Be(s);
        }
    }
}

