using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using XtrXmlTranslator.App.ViewModels;

namespace XtrXmlTranslator.App.Services;

public interface IRowFilterService
{
    TranslationRowVM? Update(ObservableCollection<TranslationRowVM> rows,
        ObservableCollection<TranslationRowVM> viewRows,
        string? query,
        bool filterErrors,
        TranslationRowVM? currentSelection);
}

public sealed class RowFilterService : IRowFilterService
{
    public TranslationRowVM? Update(ObservableCollection<TranslationRowVM> rows,
        ObservableCollection<TranslationRowVM> viewRows,
        string? query,
        bool filterErrors,
        TranslationRowVM? currentSelection)
    {
        IEnumerable<TranslationRowVM> q = rows;
        if (filterErrors)
        {
            q = q.Where(r => !r.TagOk || r.Status == RowStatus.Error);
        }
        var s = (query ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(s))
        {
            q = q.Where(r =>
                (!string.IsNullOrEmpty(r.SourceText) && r.SourceText.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(r.TranslationText) && r.TranslationText.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(r.Context) && r.Context.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        var list = q.ToList();
        viewRows.Clear();
        foreach (var r in list) viewRows.Add(r);

        if (currentSelection is not null && viewRows.Contains(currentSelection))
            return currentSelection;

        if (filterErrors)
            return viewRows.FirstOrDefault(r => !r.TagOk) ?? viewRows.FirstOrDefault();

        return viewRows.FirstOrDefault();
    }
}
