using System;
using System.Linq;
using System.Xml.Linq;

namespace XtrXmlTranslator.Core.Srx;

public static class SrxLoader
{
    private static readonly XNamespace NS = "http://www.lisa.org/srx20";

    public static SrxDocument LoadFromString(string xml)
    {
        var xdoc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var srx = xdoc.Descendants(NS + "srx").FirstOrDefault()
                  ?? throw new InvalidOperationException("SRX root not found.");

        var header = srx.Element(NS + "header")
                  ?? throw new InvalidOperationException("<header> missing");
        var cascadeAttr = (string?)header.Attribute("cascade") ?? "yes";
        var cascade = cascadeAttr.Equals("yes", StringComparison.OrdinalIgnoreCase);

        var body = srx.Element(NS + "body")
               ?? throw new InvalidOperationException("<body> missing");

        var doc = new SrxDocument { Cascade = cascade };

        var lrParent = body.Element(NS + "languagerules")
                      ?? throw new InvalidOperationException("<languagerules> missing");
        foreach (var lr in lrParent.Elements(NS + "languagerule"))
        {
            var set = new SrxLanguageRule { Name = (string?)lr.Attribute("languagerulename") ?? "" };
            foreach (var r in lr.Elements(NS + "rule"))
            {
                var brAttr = ((string?)r.Attribute("break")) ?? "yes";
                var isBreak = !brAttr.Equals("no", StringComparison.OrdinalIgnoreCase);
                var before = ((string?)r.Element(NS + "beforebreak"))?.Trim();
                var after = ((string?)r.Element(NS + "afterbreak"))?.Trim();
                set.Rules.Add(new SrxRule { Before = before, After = after, IsBreak = isBreak });
            }
            doc.LanguageRules.Add(set);
        }

        var maps = body.Element(NS + "maprules")
                 ?? throw new InvalidOperationException("<maprules> missing");
        foreach (var m in maps.Elements(NS + "languagemap"))
        {
            doc.LanguageMaps.Add(new SrxLanguageMap
            {
                LanguagePattern = (string?)m.Attribute("languagepattern") ?? ".*",
                LanguageRuleName = (string?)m.Attribute("languagerulename") ?? ""
            });
        }

        return doc;
    }
}

