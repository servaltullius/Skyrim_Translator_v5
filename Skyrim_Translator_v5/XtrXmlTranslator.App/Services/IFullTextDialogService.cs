using System.Threading.Tasks;

namespace XtrXmlTranslator.App.Services;

public interface IFullTextDialogService
{
    Task<string?> OpenAsync(string context, string source, string translation, bool isTranslationEditable);
}

