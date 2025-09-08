namespace XtrXmlTranslator.Core.Translate;

public interface ITranslatorFactory
{
    ITranslator Create(GeminiOptions options, XtrXmlTranslator.Core.TM.ITmStore tm);
}

public sealed class GeminiTranslatorFactory : ITranslatorFactory
{
    public ITranslator Create(GeminiOptions options, XtrXmlTranslator.Core.TM.ITmStore tm)
        => new GeminiTranslator(options, tm);
}

