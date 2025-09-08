using Avalonia.Controls;
using Avalonia.Interactivity;
using XtrXmlTranslator.App.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Diagnostics;
using Avalonia;

namespace XtrXmlTranslator.App.Views.Dialogs;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    public void Initialize()
    {
        var s = SecretsFileStore.Load();
        ApiKeyBox.Text = s.GeminiApiKey ?? string.Empty;

        // 런타임 설정 초기값 로드
        try
        {
            var sp = AppServices.Provider;
            if (sp != null)
            {
                var rtObj = sp.GetService(typeof(IRuntimeSettings));
                if (rtObj is IRuntimeSettings rt)
                {
                    RtSkipTagHeavyBox.IsChecked = rt.SkipTagHeavyEnabled;
                    RtTagHeavyMinTextBox.Text = rt.TagHeavyMinText.ToString();
                }
            }
        }
        catch { /* ignore */ }

        LoadConfigStatus();
    }

    private void LoadConfigStatus()
    {
        // 구성 상태 표시
        try
        {
            var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            IConfiguration cfg = AppServices.Provider?.GetService<IConfiguration>()
                ?? new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: false)
                    .AddJsonFile(Path.Combine("Config", "appsettings.json"), optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables(prefix: "XTRANS_")
                    .Build();

            string m(string? v, string def = "") => string.IsNullOrWhiteSpace(v) ? def : v;
            string translatorMode = m(cfg["Translator:Mode"], "Default");
            string skipTagHeavy = m(cfg["Translator:SkipTagHeavy"], "false");
            string tagHeavyMin = m(cfg["Translator:TagHeavyMinText"], "0");
            string gemModel = m(cfg["Gemini:Model"], "gemini-2.5-flash");
            string gemTemp = m(cfg["Gemini:Temperature"], "0.2");
            string gemConc = m(cfg["Gemini:MaxConcurrency"], "4");
            string gemRpm = m(cfg["Gemini:RequestsPerMinute"], "10");
            string gemTimeout = m(cfg["Gemini:HttpTimeout"], "00:01:00");
            string key = new CombinedSecretsProvider().Get("GEMINI_API_KEY");
            bool keyMissing = string.IsNullOrWhiteSpace(key);
            string keyState = keyMissing ? "(없음)" : "(설정됨)";

            ConfigStatus.Text =
                $"Env: {envName}\n" +
                $"Translator.Mode: {translatorMode}\n" +
                $"Translator.SkipTagHeavy: {skipTagHeavy}\n" +
                $"Translator.TagHeavyMinText: {tagHeavyMin}\n" +
                $"Gemini.Model: {gemModel}\n" +
                $"Gemini.Temperature: {gemTemp}\n" +
                $"Gemini.MaxConcurrency: {gemConc}\n" +
                $"Gemini.RequestsPerMinute: {gemRpm}\n" +
                $"Gemini.HttpTimeout: {gemTimeout}\n" +
                $"GEMINI_API_KEY: {keyState}";

            // 키 상태 색상 표시: 없음이면 Danger 색상, 있으면 기본
            if (KeyStateText is not null)
            {
                KeyStateText.Text = keyState;
                if (keyMissing)
                {
                    // Danger 리소스가 있을 경우 사용
                    if (this.TryFindResource("Danger", out var danger) && danger is Avalonia.Media.IBrush br)
                        KeyStateText.Foreground = br;
                }
                else
                {
                    KeyStateText.ClearValue(TextBlock.ForegroundProperty);
                }
            }
        }
        catch (Exception ex)
        {
            ConfigStatus.Text = $"구성 읽기 오류: {ex.Message}";
        }
    }

    private async void OnCopyConfigClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard != null)
            {
                await top.Clipboard.SetTextAsync(ConfigStatus.Text ?? string.Empty);
            }
        }
        catch { /* ignore */ }
    }

    private void OnRefreshConfigClick(object? sender, RoutedEventArgs e) => LoadConfigStatus();

    private async void OnPasteKeyClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.Clipboard != null)
            {
                var text = await top.Clipboard.GetTextAsync();
                if (!string.IsNullOrEmpty(text)) ApiKeyBox.Text = text;
            }
        }
        catch { /* ignore */ }
    }

    private void OnOpenConfigFolderClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Services.AppConfigPaths.ConfigDir;
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch { /* ignore */ }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        SecretsFileStore.Save(new SecretsPayload { GeminiApiKey = ApiKeyBox.Text });

        // 런타임 설정 저장(세션 범위)
        try
        {
            var sp = AppServices.Provider;
            if (sp != null)
            {
                var rtObj = sp.GetService(typeof(IRuntimeSettings));
                if (rtObj is IRuntimeSettings rt)
                {
                    rt.SkipTagHeavyEnabled = RtSkipTagHeavyBox.IsChecked == true;
                    if (int.TryParse(RtTagHeavyMinTextBox.Text, out var v) && v >= 0) rt.TagHeavyMinText = v;
                    else rt.TagHeavyMinText = 0;
                }
            }
        }
        catch { /* ignore */ }

        Close(true);
    }

    private async void OnHealthCheckClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button b) b.IsEnabled = false;
            HealthStatus.Text = "테스트 중…";
            var (ok, msg) = await HealthCheckService.CheckAsync();
            HealthStatus.Text = ok ? "연결 성공" : $"실패: {msg}";
        }
        finally
        {
            if (sender is Button b2) b2.IsEnabled = true;
        }
    }
}
