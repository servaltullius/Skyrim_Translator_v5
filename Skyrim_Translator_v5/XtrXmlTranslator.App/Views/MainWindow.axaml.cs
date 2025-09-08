using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using XtrXmlTranslator.App.ViewModels;
using XtrXmlTranslator.App.Views.Dialogs;
using XtrXmlTranslator.Core.Prompt;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Validation;

namespace XtrXmlTranslator.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenPromptClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var dlg = new PromptDialog();
            dlg.Initialize(vm.Prompt);
            var result = await dlg.ShowDialog<PromptConfig?>(this);
            if (result is not null)
            {
                vm.Prompt = result;
                vm.SavePromptToDisk();
                vm.StatusText = "프롬프트가 업데이트되었습니다.";
            }
        }
    }

    private async void OnOpenSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var dlg = new XtrXmlTranslator.App.Views.Dialogs.SettingsDialog();
            dlg.Initialize();
            var saved = await dlg.ShowDialog<bool>(this);
            if (saved)
            {
                vm.StatusText = "API 키가 저장되었습니다.";
            }
        }
    }

    private async void OnOpenGlossaryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var dlg = new GlossaryDialog();
            dlg.Initialize(vm.Glossary);
            var result = await dlg.ShowDialog<GlossaryStore?>(this);
            if (result is not null)
            {
                vm.Glossary = result;
                vm.SaveGlossaryToDisk();
                // Recompile glossary (direct call)
                vm.RebuildGlossaryCompiled();
                vm.StatusText = $"용어집 항목: {vm.Glossary.Entries.Count}";
            }
        }
    }

    private async void OnOpenXmlClick(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Open xTranslator XML",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("XML files") { Patterns = new [] { "*.xml" }, MimeTypes = new [] { "application/xml", "text/xml" } },
                new FilePickerFileType("All files") { Patterns = new [] { "*" } }
            }
        };

        var files = await this.StorageProvider.OpenFilePickerAsync(options);
        if (files is { Count: > 0 })
        {
            var file = files[0];
            await using var stream = await file.OpenReadAsync();
            if (DataContext is MainWindowViewModel vm)
            {
                vm.LoadFromXmlStream(stream);
            }
        }
    }

    private async void OnOpenSourceFullText(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is TranslationRowVM row && DataContext is MainWindowViewModel vm)
        {
            await vm.OpenSourceFullTextCommand.ExecuteAsync(row);
        }
    }

    private async void OnOpenTranslationFullText(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is TranslationRowVM row && DataContext is MainWindowViewModel vm)
        {
            await vm.OpenTranslationFullTextCommand.ExecuteAsync(row);
        }
    }

    private async void OnDataGridDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (e.Handled) return;
        if (DataContext is MainWindowViewModel vm && vm.SelectedRow is { } row)
        {
            await vm.OpenTranslationFullTextCommand.ExecuteAsync(row);
            e.Handled = true;
        }
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.F)
        {
            if (this.FindControl<TextBox>("SearchBox") is { } sb)
            {
                sb.Focus();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.Shift)
        {
            vm.PrevWarningCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            if (vm.SelectedRow is { } row)
            {
                await vm.OpenTranslationFullTextCommand.ExecuteAsync(row);
                e.Handled = true;
            }
        }
    }
}
