using System.Reflection;
using System.IO;

namespace XtrXmlTranslator.Core.Srx;

public static class SrxPresets
{
    private static readonly Lazy<string> _en = new(() => Read("XtrXmlTranslator.Core.Resources.Srx.en-default.srx"));
    private static readonly Lazy<string> _ko = new(() => Read("XtrXmlTranslator.Core.Resources.Srx.ko-default.srx"));
    private static readonly Lazy<string> _ja = new(() => Read("XtrXmlTranslator.Core.Resources.Srx.ja-default.srx"));
    private static readonly Lazy<string> _zh = new(() => Read("XtrXmlTranslator.Core.Resources.Srx.zh-default.srx"));

    public static string EnDefault => _en.Value;
    public static string KoDefault => _ko.Value;
    public static string JaDefault => _ja.Value;
    public static string ZhDefault => _zh.Value;

    public static string ForLanguage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return EnDefault;
        var lc = code.ToLowerInvariant();
        if (lc.StartsWith("ko")) return KoDefault;
        if (lc.StartsWith("ja")) return JaDefault;
        if (lc.StartsWith("zh")) return ZhDefault;
        return EnDefault;
    }

    private static string Read(string resourceName)
    {
        var asm = typeof(SrxPresets).Assembly;
        using var s = asm.GetManifestResourceStream(resourceName) ??
                      throw new FileNotFoundException($"SRX resource not found: {resourceName}");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
