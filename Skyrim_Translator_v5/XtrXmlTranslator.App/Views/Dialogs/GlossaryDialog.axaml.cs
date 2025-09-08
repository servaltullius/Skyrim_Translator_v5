using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using XtrXmlTranslator.Core.Glossary;

namespace XtrXmlTranslator.App.Views.Dialogs;

public partial class GlossaryDialog : Window
{
    public ObservableCollection<GlossaryEntry> Items { get; } = new();
    public GlossaryType[] Types { get; } = new[] { GlossaryType.ENFORCE, GlossaryType.PREFER, GlossaryType.PROTECT };

    public GlossaryDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void Initialize(GlossaryStore store)
    {
        Items.Clear();
        foreach (var e in store.Entries)
            Items.Add(new GlossaryEntry
            {
                Source = e.Source,
                Target = e.Target,
                CaseSensitive = e.CaseSensitive,
                Type = e.Type,
                Notes = e.Notes
            });
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        Items.Add(new GlossaryEntry { Source = "", Target = "", Type = GlossaryType.ENFORCE, CaseSensitive = DefaultCaseCheck.IsChecked ?? false });
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is GlossaryEntry sel)
            Items.Remove(sel);
    }

    private async void OnImportClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "용어집 가져오기",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>{
                new("CSV/TSV") { Patterns = new[]{"*.csv","*.tsv"}, MimeTypes = new[]{"text/csv","text/tab-separated-values","text/plain"} }
            }
        });
        if (files is { Count: > 0 })
        {
            var p = files[0].Path.LocalPath;
            var g = GlossaryIo.Load(p);
            Initialize(g);
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "용어집 내보내기",
            ShowOverwritePrompt = true,
            DefaultExtension = "csv",
            FileTypeChoices = new List<FilePickerFileType> { new("CSV") { Patterns = new[] { "*.csv" } }, new("TSV") { Patterns = new[] { "*.tsv" } } }
        });
        if (file is not null)
        {
            var store = ToStore();
            GlossaryIo.Save(store, file.Path.LocalPath);
        }
    }

    private GlossaryStore ToStore()
    {
        var s = new GlossaryStore();
        foreach (var e in Items)
        {
            s.Entries.Add(new GlossaryEntry
            {
                Source = e.Source,
                Target = e.Target,
                Type = e.Type,
                CaseSensitive = e.CaseSensitive,
                Notes = e.Notes
            });
        }
        return s;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(ToStore());
    }
}
