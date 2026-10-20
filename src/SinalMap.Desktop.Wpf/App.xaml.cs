using System.IO;
using System.Windows;
using SinalMap.Infrastructure.Windows;
using SinalMap.Infrastructure.Windows.Native;
using SinalMap.Presentation;
using SinalMap.Desktop.Wpf.Services;

namespace SinalMap.Desktop.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        var clock = new SystemProbeClock();
        var reader = new JsonSessionReader();
        var viewModel = new ProbeViewModel(new WindowsWifiMonitor(clock), new WindowsIcmpProbe(clock), clock,
            new JsonSessionStore(), reader.ReadAsync, new ProbeDialogs(window),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SinalMap", "results"),
            ex => ex is NativeWifiException native
                ? $"{native.Issue.Operation}: falha na API Wi-Fi (código {native.Issue.NativeCode?.ToString() ?? "n/a"}). Confira WlanSvc e o consentimento de localização no Windows; executar como administrador não substitui consentimento."
                : "A operação falhou. Confira a interface Wi-Fi e tente novamente.");
        window.DataContext = viewModel;
        MainWindow = window;
        window.Show();
    }
}
