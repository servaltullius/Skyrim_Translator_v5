using Avalonia;
using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace XtrXmlTranslator.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ConfigureLogging();
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Unhandled exception");
            else
                Log.Fatal("Unhandled exception: {Info}", e.ExceptionObject);
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureLogging()
    {
        var levelStr = Environment.GetEnvironmentVariable("XTRANS_LOG_LEVEL");
        var dir = Environment.GetEnvironmentVariable("XTRANS_LOG_DIR") ?? "logs";
        Directory.CreateDirectory(dir);
        var level = levelStr?.ToUpperInvariant() switch
        {
            "VERBOSE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFORMATION" => LogEventLevel.Information,
            "WARNING" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(dir, "xtrans-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, shared: true)
            .CreateLogger();

        Log.Information("Logger initialized. Level={Level} Dir={Dir}", level, dir);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
