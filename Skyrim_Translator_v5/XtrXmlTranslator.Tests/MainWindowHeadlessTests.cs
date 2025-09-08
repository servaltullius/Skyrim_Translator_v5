using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Headless.XUnit;
using XtrXmlTranslator.App.ViewModels;
using Xunit;

public class MainWindowHeadlessTests
{
    [AvaloniaFact]
    public async Task StreamDelta_Appends_To_Translation_Cell()
    {
        var vm = new MainWindowViewModel();
        vm.Rows.Add(new TranslationRowVM("ctx#1", "Hello <b/> world"));

        await Dispatcher.UIThread.InvokeAsync(() => vm.Rows[0].TranslationText = "");
        await Dispatcher.UIThread.InvokeAsync(() => vm.Rows[0].TranslationText += "안녕");
        await Dispatcher.UIThread.InvokeAsync(() => vm.Rows[0].TranslationText += " 세상");

        Assert.Contains("안녕 세상", vm.Rows[0].TranslationText);
    }
}

