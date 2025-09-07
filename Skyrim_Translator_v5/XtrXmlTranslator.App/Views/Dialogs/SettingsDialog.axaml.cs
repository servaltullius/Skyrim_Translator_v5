using Avalonia.Controls;
using Avalonia.Interactivity;
using XtrXmlTranslator.App.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

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
        // 구성 상태 표시
        try
        {
            var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var cfg = new ConfigurationBuilder()
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
            string keyState = string.IsNullOrWhiteSpace(key) ? "(없음)" : "(설정됨)";

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
        }
        catch (Exception ex)
        {
            ConfigStatus.Text = $"구성 읽기 오류: {ex.Message}";
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        SecretsFileStore.Save(new SecretsPayload { GeminiApiKey = ApiKeyBox.Text });
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
