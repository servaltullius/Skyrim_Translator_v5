using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using XtrXmlTranslator.App.ViewModels;
using XtrXmlTranslator.App.Views;
using Serilog;
using System;
using XtrXmlTranslator.Core.Glossary;
using XtrXmlTranslator.Core.Translate;
using XtrXmlTranslator.App.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace XtrXmlTranslator.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            Log.Information("App starting: OS={OS}, Framework={FX}", Environment.OSVersion, System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

            // Configure glossary word boundary from env (optional)
            var wc = Environment.GetEnvironmentVariable("XTRANS_GLOSSARY_WORDCHARS");
            if (!string.IsNullOrEmpty(wc))
            {
                GlossaryBoundary.SetConfig(new GlossaryBoundaryConfig(wc));
                Log.Information("Glossary word-chars configured: {Chars}", wc);
            }

            // Build configuration root
            var envName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: true)
                .AddJsonFile(Path.Combine("Config", "appsettings.json"), optional: true, reloadOnChange: true)
                .AddEnvironmentVariables(prefix: "XTRANS_");
            var configuration = configBuilder.Build();

            // DI container
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IAppConfig>(sp => new Services.ConfigRoot(configuration));
            services.AddSingleton<Services.ResilientGeminiFactory>();
            services.AddSingleton<ITranslatorFactory>(sp =>
            {
                var appCfg = sp.GetRequiredService<IAppConfig>();
                var defaultFactory = sp.GetRequiredService<Services.ResilientGeminiFactory>();
                return new Services.TranslatorFactorySelector(appCfg, defaultFactory);
            });
            services.AddSingleton<ISecretsProvider, Services.CombinedSecretsProvider>();
            services.AddSingleton<IRowFilterService, Services.RowFilterService>();
            services.AddSingleton<IValidationOrchestrator, Services.ValidationOrchestrator>();
            services.AddSingleton<Services.TranslationSessionService>();
            services.AddSingleton<IConfigService, Services.ConfigService>();

            var provider = services.BuildServiceProvider();

            // UI objects
            var window = new MainWindow();
            var fullText = new Services.FullTextDialogService(window);

            // Compose ViewModel from DI + UI-bound services
            var secrets = provider.GetRequiredService<ISecretsProvider>();
            var selector = provider.GetRequiredService<ITranslatorFactory>();
            var rowFilter = provider.GetRequiredService<IRowFilterService>();
            var validator = provider.GetRequiredService<IValidationOrchestrator>();
            var session = provider.GetRequiredService<Services.TranslationSessionService>();
            var config = provider.GetRequiredService<IConfigService>();

            window.DataContext = new MainWindowViewModel(secrets, selector, fullText, rowFilter, validator, session, config);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}