using System;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.Core.TM;

namespace XtrXmlTranslator.App.Services;

public sealed class TranslatorFactorySelector : ITranslatorFactory
{
    private readonly IAppConfig _config;
    private readonly ITranslatorFactory _defaultFactory;

    public TranslatorFactorySelector(IAppConfig config, ITranslatorFactory defaultFactory)
    {
        _config = config; _defaultFactory = defaultFactory;
    }

    public ITranslator Create(GeminiOptions options, ITmStore tm)
    {
        var mode = _config.Get("XTRANS_TRANSLATOR");
        if (!string.IsNullOrWhiteSpace(mode) && mode.Trim().Equals("MOCK", StringComparison.OrdinalIgnoreCase))
            return new MockTranslator();
        return _defaultFactory.Create(options, tm);
    }
}
