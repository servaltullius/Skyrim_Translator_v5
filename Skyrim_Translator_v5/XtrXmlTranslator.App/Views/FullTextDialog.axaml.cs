using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace XtrXmlTranslator.App.Views;

public partial class FullTextDialog : Window
{
    private Services.UxSettings _ux = new Services.UxSettings();

    public FullTextDialog()
    {
        InitializeComponent();
    }

    public void Initialize(string context, string source, string translation)
    {
        ContextText.Text = context;
        SourceBox.Text = source;
        TranslationBox.Text = translation;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        Close(TranslationBox.Text ?? string.Empty);
    }

    private void OnDialogOpened(object? sender, System.EventArgs e)
    {
        _ux = Services.UxSettingsService.Load();
        if (_ux.FullTextWidth > 0 && _ux.FullTextHeight > 0)
        {
            Width = _ux.FullTextWidth; Height = _ux.FullTextHeight;
        }
        if (_ux.FullTextFontSize > 0)
        {
            SourceBox.FontSize = _ux.FullTextFontSize;
            TranslationBox.FontSize = _ux.FullTextFontSize;
        }
    }

    private void OnDialogClosed(object? sender, System.EventArgs e)
    {
        _ux.FullTextWidth = Width;
        _ux.FullTextHeight = Height;
        _ux.FullTextFontSize = TranslationBox.FontSize;
        Services.UxSettingsService.Save(_ux);
    }

    private void OnDialogKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            if (e.Key == Avalonia.Input.Key.OemPlus || e.Key == Avalonia.Input.Key.Add)
            {
                TranslationBox.FontSize += 1; SourceBox.FontSize = TranslationBox.FontSize; e.Handled = true; return;
            }
            if (e.Key == Avalonia.Input.Key.OemMinus || e.Key == Avalonia.Input.Key.Subtract)
            {
                TranslationBox.FontSize = Math.Max(10, TranslationBox.FontSize - 1); SourceBox.FontSize = TranslationBox.FontSize; e.Handled = true; return;
            }
            if (e.Key == Avalonia.Input.Key.D0)
            {
                TranslationBox.FontSize = 14; SourceBox.FontSize = 14; e.Handled = true; return;
            }
        }
    }
}

