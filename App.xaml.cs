using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using VlessVPN.Services;

namespace VlessVPN;

public partial class App : Application
{
    /// <summary>Единственный экземпляр — тот же, что использует MainViewModel, чтобы OnExit реально останавливал VPN и сбрасывал системный прокси.</summary>
    public static XrayService Xray { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Xray = new XrayService();
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    private static void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        // Не вызывать StopXray с UI-потока: WaitForExit + async могли вешать завершение и оставлять xray живым
        Task.Run(() => Xray.StopXray()).GetAwaiter().GetResult();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionEnding -= OnSessionEnding;
        try
        {
            Task.Run(() => Xray.StopXray()).GetAwaiter().GetResult();
        }
        finally
        {
            base.OnExit(e);
        }
    }
}






