using System.Threading.Tasks;
using XtrXmlTranslator.App.Views;

namespace XtrXmlTranslator.App.Services;

public sealed class FullTextDialogService : IFullTextDialogService
{
    private readonly Avalonia.Controls.Window _owner;
    public FullTextDialogService(Avalonia.Controls.Window owner) => _owner = owner;

    public async Task<string?> OpenAsync(string context, string source, string translation, bool isTranslationEditable)
    {
        var dlg = new FullTextDialog();
        dlg.Initialize(context, source, translation);
        var result = await dlg.ShowDialog<string?>(_owner);
        return result;
    }
}

