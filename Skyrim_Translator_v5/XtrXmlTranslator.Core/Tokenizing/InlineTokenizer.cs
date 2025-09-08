using System.Text;
using System.Text.RegularExpressions;

namespace XtrXmlTranslator.Core.Tokenizing;

public static partial class InlineTokenizer
{
    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XmlLikeTag();

    [GeneratedRegex(@"\%\{[A-Za-z0-9_.-]+\}", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PercentBrace();

    [GeneratedRegex(@"\{[A-Za-z0-9_.-]+\}", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Brace();

    // CK Message 포맷: Flags/Width/Precision/Type (%.0f 등)
    [GeneratedRegex(@"%(?:\d+\$)?[+#0\- ]*(?:\d+)?(?:\.\d+)?[cdfgiosuxXeEfp%]", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Percent();

    private enum Kind { None, Xml, Percent, Brace, PercentBrace }

    public static List<InlineToken> Tokenize(string text)
    {
        var tokens = new List<InlineToken>();
        if (string.IsNullOrEmpty(text))
        {
            tokens.Add(new InlineToken(InlineTokenType.Text, text ?? string.Empty));
            return tokens;
        }

        int i = 0;
        while (i < text.Length)
        {
            var (k, m) = FindNext(text, i);
            if (k == Kind.None || m is null)
            {
                tokens.Add(new InlineToken(InlineTokenType.Text, text.Substring(i)));
                break;
            }

            if (m.Index > i)
                tokens.Add(new InlineToken(InlineTokenType.Text, text.Substring(i, m.Index - i)));

            var val = m.Value;
            tokens.Add(k switch
            {
                Kind.Xml => new InlineToken(InlineTokenType.XmlLikeTag, val),
                Kind.Percent => new InlineToken(InlineTokenType.PercentPlaceholder, val),
                Kind.Brace => new InlineToken(InlineTokenType.BracePlaceholder, val),
                Kind.PercentBrace => new InlineToken(InlineTokenType.PercentBracePlaceholder, val),
                _ => new InlineToken(InlineTokenType.Text, val)
            });

            i = m.Index + m.Length;
        }

        return tokens;
    }

    public static IEnumerable<string> ExtractTranslatableRuns(IReadOnlyList<InlineToken> tokens)
        => tokens.Where(t => t.Type == InlineTokenType.Text).Select(t => t.Value);

    public static string Reassemble(IReadOnlyList<InlineToken> tokens, Queue<string> translatedTextRuns)
    {
        var sb = new StringBuilder(tokens.Count * 8);
        foreach (var t in tokens)
        {
            if (t.Type == InlineTokenType.Text)
            {
                if (translatedTextRuns.Count == 0)
                    throw new InvalidOperationException("No translated segment available for a text run.");
                sb.Append(translatedTextRuns.Dequeue());
            }
            else
            {
                sb.Append(t.Value);
            }
        }
        if (translatedTextRuns.Count != 0)
            throw new InvalidOperationException("Extra translated segments remain.");
        return sb.ToString();
    }

    public static void AssertPlaceholdersUnchanged(string before, string after)
    {
        int Count(Regex rx, string s) => rx.Matches(s).Count;

        if (Count(Percent(), before) != Count(Percent(), after))
            throw new InvalidOperationException("Percent placeholders count mismatch.");

        if (Count(Brace(), before) != Count(Brace(), after))
            throw new InvalidOperationException("Brace placeholders count mismatch.");

        if (Count(PercentBrace(), before) != Count(PercentBrace(), after))
            throw new InvalidOperationException("Percent-brace placeholders count mismatch.");

        if (Count(XmlLikeTag(), before) != Count(XmlLikeTag(), after))
            throw new InvalidOperationException("Xml-like tags count mismatch.");
    }

    private static (Kind, Match?) FindNext(string text, int start)
    {
        var mXml = XmlLikeTag().Match(text, start);
        var mPer = Percent().Match(text, start);
        var mBra = Brace().Match(text, start);
        var mPerBra = PercentBrace().Match(text, start);

        var list = new (Kind kind, Match? m)[] {
            (Kind.Xml, mXml.Success ? mXml : null),
            (Kind.Percent, mPer.Success ? mPer : null),
            (Kind.Brace, mBra.Success ? mBra : null),
            (Kind.PercentBrace, mPerBra.Success ? mPerBra : null)
        }.Where(x => x.m is not null).ToList();

        if (list.Count == 0) return (Kind.None, null);
        var best = list.MinBy(x => x.m!.Index);
        return (best.kind, best.m);
    }
}

