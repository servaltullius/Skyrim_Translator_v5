using System.Threading.Tasks;

namespace XtrXmlTranslator.App.Services;

// Fallback service used only when App wiring is not available (design-time/testing)
public sealed class DummyFullTextDialogService : IFullTextDialogService
{
    public Task<string?> OpenAsync(string context, string source, string translation, bool isTranslationEditable)
        => Task.FromResult<string?>(null);
}

