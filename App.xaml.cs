using System.Windows;
using VlessVPN.Services;

namespace VlessVPN;

public partial class App : Application
{
    private XrayService? _xrayService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _xrayService = new XrayService();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _xrayService?.StopXray();
        base.OnExit(e);
    }
}






