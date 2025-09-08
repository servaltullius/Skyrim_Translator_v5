using System.Collections.ObjectModel;
using FluentAssertions;
using Xunit;
using XtrXmlTranslator.App.Services;
using XtrXmlTranslator.App.ViewModels;

namespace XtrXmlTranslator.Tests.Services;

public class RowFilterServiceTests
{
    private static TranslationRowVM Row(string ctx, string src, string? tgt = null, bool tagOk = true, RowStatus status = RowStatus.Auto)
    {
        var r = new TranslationRowVM(ctx, src) { Index = 1 };
        r.TranslationText = tgt ?? string.Empty;
        r.TagOk = tagOk;
        r.Status = status;
        return r;
    }

    [Fact]
    public void Update_NoFilter_NoQuery_MirrorsRowsAndKeepsSelection()
    {
        var svc = new RowFilterService();
        var rows = new ObservableCollection<TranslationRowVM>
        {
            Row("a","hello"), Row("b","world"), Row("c","skyrim")
        };
        var view = new ObservableCollection<TranslationRowVM>();
        var sel = rows[1];

        var nextSel = svc.Update(rows, view, query: null, filterErrors: false, currentSelection: sel);

        view.Should().HaveCount(3);
        nextSel.Should().Be(sel);
    }

    [Fact]
    public void Update_FilterErrors_FiltersAndSelectsFirstError()
    {
        var svc = new RowFilterService();
        var rows = new ObservableCollection<TranslationRowVM>
        {
            Row("a","ok", tagOk:true),
            Row("b","bad1", tagOk:false, status: RowStatus.Error),
            Row("c","bad2", tagOk:false)
        };
        var view = new ObservableCollection<TranslationRowVM>();

        var nextSel = svc.Update(rows, view, query: null, filterErrors: true, currentSelection: null);

        view.Should().HaveCount(2);
        nextSel.Should().Be(view[0]);
        nextSel!.TagOk.Should().BeFalse();
    }

    [Fact]
    public void Update_Query_CaseInsensitive_MatchesSourceTranslationOrContext()
    {
        var svc = new RowFilterService();
        var rows = new ObservableCollection<TranslationRowVM>
        {
            Row("PATH/Item","Hello SKYRIM"),
            Row("Other","Dragonborn", tgt:"hero"),
            Row("ctx","something else")
        };
        var view = new ObservableCollection<TranslationRowVM>();

        var nextSel = svc.Update(rows, view, query: "skyrim", filterErrors: false, currentSelection: null);

        view.Should().HaveCount(1);
        view[0].SourceText.Should().Contain("SKYRIM");
        nextSel.Should().Be(view[0]);
    }
}

