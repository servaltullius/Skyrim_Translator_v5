using System;
using System.Linq;
using XtrXmlTranslator.Core.Srx;
using XtrXmlTranslator.Core.Tokenizing;

static void Dump(string title, string text, SrxEngine engine)
{
    var toks = InlineTokenizer.Tokenize(text);
    var (flat, breaks) = SrxSegmenter.DebugFlattenAndBreaks(toks, engine);
    Console.WriteLine($"=== {title} ===");
    Console.WriteLine($"FLAT: {flat}");
    Console.WriteLine($"BREAKS: [{string.Join(",", breaks)}]");
    var segs = SrxSegmenter.SegmentTokens(toks, engine);
    Console.WriteLine("SEGS:");
    foreach (var s in segs.Select(s => s.ToString()))
        Console.WriteLine($"- '{s}'");
}

const string MinimalSrx = @"<srx version=""2.0"" xmlns=""http://www.lisa.org/srx20"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <header segmentsubflows=""yes"" cascade=""yes"">
    <formathandle type=""start"" include=""no""/>
    <formathandle type=""end"" include=""yes""/>
    <formathandle type=""isolated"" include=""no""/>
  </header>
  <body>
    <languagerules>
      <languagerule languagerulename=""Default"">
        <rule break=""yes""><beforebreak>[.?!]</beforebreak><afterbreak>\s[A-Z]</afterbreak></rule>
      </languagerule>
      <languagerule languagerulename=""English"">
        <rule break=""no""><beforebreak>\sMr\.</beforebreak><afterbreak>\s[A-Z]</afterbreak></rule>
        <rule break=""yes""><beforebreak>\sU\.K\.</beforebreak><afterbreak>\s[A-Z]</afterbreak></rule>
        <rule break=""no""><beforebreak>\sU\.K\.</beforebreak><afterbreak>\s[a-z]</afterbreak></rule>
        <rule break=""no""><beforebreak>\s[Ee]tc\.</beforebreak><afterbreak>\s[a-z]</afterbreak></rule>
      </languagerule>
    </languagerules>
    <maprules>
      <languagemap languagepattern=""^en(-[A-Za-z0-9]+)?$"" languagerulename=""English""/>
      <languagemap languagepattern="".*"" languagerulename=""Default""/>
    </maprules>
  </body>
</srx>";

var doc = SrxLoader.LoadFromString(MinimalSrx);
var engine = SrxCompiler.Compile(doc, "en-US");

Dump("Plain U.K.", "This is Mr. Abel from the U.K. He wrote etc. today. Ok?", engine);
Dump("Tag Boundary", "Hello." + " World! How are you?", engine);
