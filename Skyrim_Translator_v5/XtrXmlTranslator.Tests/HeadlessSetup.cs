using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(HeadlessSetup))]
public class HeadlessSetup
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .AfterSetup(_ =>
                  {
                      Application.Current!.Styles.Add(new FluentTheme());
                  });

    private class App : Application { }
}

