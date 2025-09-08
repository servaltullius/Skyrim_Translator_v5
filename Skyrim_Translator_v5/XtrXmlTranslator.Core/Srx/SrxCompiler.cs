using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XtrXmlTranslator.Core.Srx;

public sealed class CompiledRule
{
    public CompiledRule(Regex combined, bool isBreak, int order, Regex? beforeRegex, Regex? afterRegex)
    {
        Combined = combined; IsBreak = isBreak; Order = order; BeforeRegex = beforeRegex; AfterRegex = afterRegex;
    }
    public Regex Combined { get; }
    public bool IsBreak { get; }
    public int Order { get; }
    public Regex? BeforeRegex { get; }
    public Regex? AfterRegex { get; }
}

public sealed class SrxEngine
{
    public List<CompiledRule> Rules { get; }
    public SrxEngine(List<CompiledRule> rules) => Rules = rules;
}

public static class SrxCompiler
{
    private static string NormalizeIcuToDotNet(string? p)
    {
        if (string.IsNullOrEmpty(p)) return "";
        var s = p!;
        s = Regex.Replace(s, @"\\x\{([0-9A-Fa-f]{4})\}", m => "\\u" + m.Groups[1].Value.ToUpperInvariant());
        s = Regex.Replace(s, @"\\x([0-9A-Fa-f]{4})\b", m => "\\u" + m.Groups[1].Value.ToUpperInvariant());
        s = Regex.Replace(s, @"\\R", @"\r?\n");
        return s;
    }

    public static SrxEngine Compile(SrxDocument doc, string languageTag, TimeSpan? timeout = null)
    {
        var timeoutValue = timeout ?? TimeSpan.FromMilliseconds(200);
        var ruleByName = doc.LanguageRules.ToDictionary(x => x.Name);

        var ordered = new List<SrxRule>();
        foreach (var map in doc.LanguageMaps)
        {
            var re = new Regex(map.LanguagePattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (!re.IsMatch(languageTag)) continue;

            if (!ruleByName.TryGetValue(map.LanguageRuleName, out var set))
                throw new InvalidOperationException($"Unknown languagerulename: {map.LanguageRuleName}");
            ordered.AddRange(set.Rules);
            if (!doc.Cascade) break;
        }

        var compiled = new List<CompiledRule>();
        int order = 0;
        foreach (var r in ordered)
        {
            var before = NormalizeIcuToDotNet(r.Before);
            var after = NormalizeIcuToDotNet(r.After);

            string pattern = (before, after) switch
            {
                ("" or null, "" or null) => "",
                (not "", not "") => $"(?<={before})(?={after})",
                (not "", "") => $"(?<={before})",
                ("", not "") => $"(?={after})"
            };
            if (string.IsNullOrEmpty(pattern)) { order++; continue; }

            var rx = new Regex(pattern,
                RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled,
                timeoutValue);

            Regex? beforeRx = null;
            Regex? afterRx = null;
            if (!string.IsNullOrEmpty(before))
                beforeRx = new Regex($"{before}$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled, timeoutValue);
            if (!string.IsNullOrEmpty(after))
                afterRx = new Regex($"^{after}", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled, timeoutValue);

            compiled.Add(new CompiledRule(rx, r.IsBreak, order, beforeRx, afterRx));
            order++;
        }
        return new SrxEngine(compiled);
    }
}

