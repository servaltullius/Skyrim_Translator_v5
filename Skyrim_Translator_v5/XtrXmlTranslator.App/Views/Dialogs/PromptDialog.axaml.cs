using Avalonia.Controls;
using Avalonia.Interactivity;
using XtrXmlTranslator.Core.Prompt;

namespace XtrXmlTranslator.App.Views.Dialogs;

public partial class PromptDialog : Window
{
    private PromptConfig _cfg = new();

    public PromptDialog()
    {
        InitializeComponent();
    }

    public void Initialize(PromptConfig cfg)
    {
        _cfg = new PromptConfig
        {
            BaseVec = cfg.BaseVec,
            UserCustom = cfg.UserCustom,
            InjectGlossarySummary = cfg.InjectGlossarySummary
        };
        BaseVecBox.Text = _cfg.BaseVec;
        UserCustomBox.Text = _cfg.UserCustom ?? string.Empty;
        InjectGlossaryCheck.IsChecked = _cfg.InjectGlossarySummary;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _cfg.BaseVec = BaseVecBox.Text ?? _cfg.BaseVec;
        _cfg.UserCustom = string.IsNullOrWhiteSpace(UserCustomBox.Text) ? null : UserCustomBox.Text;
        _cfg.InjectGlossarySummary = InjectGlossaryCheck.IsChecked ?? true;
        Close(_cfg);
    }
}
