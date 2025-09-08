using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XtrXmlTranslator.App.ViewModels;

public enum RowStatus { Auto, Edited, Locked, Error }

public class TranslationRowVM : ObservableObject
{
    private CancellationTokenSource? _debounceCts;
    public int Index { get; set; } // 1-based row number for display
    public string Context { get; }
    public string SourceText { get; }

    private string _translationText = string.Empty;
    public string TranslationText
    {
        get => _translationText;
        set
        {
            if (SetProperty(ref _translationText, value))
            {
                DebouncedValidate();
            }
        }
    }

    private RowStatus _status;
    public RowStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    private bool _tagOk = true;
    public bool TagOk
    {
        get => _tagOk;
        set => SetProperty(ref _tagOk, value);
    }

    private string _warning = string.Empty;
    public string Warning
    {
        get => _warning;
        set => SetProperty(ref _warning, value);
    }

    public TranslationRowVM(string context, string source)
    {
        Context = context;
        SourceText = source;
        Status = RowStatus.Auto;
    }

    private async void DebouncedValidate()
    {
        _debounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _debounceCts = cts;
        try
        {
            await Task.Delay(300, cts.Token);
            var issues = XtrXmlTranslator.Core.Validation.Validator.Validate(SourceText, TranslationText ?? string.Empty);
            if (issues.Count == 0)
            {
                TagOk = true;
                Warning = string.Empty;
                if (Status == RowStatus.Auto) Status = RowStatus.Edited;
            }
            else
            {
                TagOk = false;
                Warning = string.Join(" | ", issues.Select(i => i.Message));
                Status = RowStatus.Error;
            }
        }
        catch (TaskCanceledException) { }
    }
}
