using System.Windows;
using System.Windows.Controls;
using VlessVPN.ViewModels;

namespace VlessVPN.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Auto-scroll log
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.LogOutput))
                {
                    LogScrollViewer?.ScrollToEnd();
                }
            };
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}






