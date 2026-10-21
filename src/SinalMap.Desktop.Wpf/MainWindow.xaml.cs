using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf;

public partial class MainWindow : Window
{
    private bool closeInProgress;
    private bool allowClose;
    public MainWindow() => InitializeComponent();

    private void OnSignalHistoryChanged(object sender, DataTransferEventArgs e)
    {
        // Trocar o histórico volta à leitura principal; amostras novas preservam a inspeção em andamento.
        if (sender is ScrollViewer viewer && ReferenceEquals(e.TargetObject, viewer) && e.Property == TagProperty)
            viewer.ScrollToTop();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProbeViewModel vm)
        {
            await vm.RefreshAsync();
            vm.StartMonitoring();
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose || DataContext is not ProbeViewModel vm) return;
        e.Cancel = true;
        if (closeInProgress) return;
        closeInProgress = true;
        try
        {
            if (await vm.RequestCloseAsync())
            {
                allowClose = true;
                // Adiar para fora do evento Closing, inclusive quando não há operação pendente.
                _ = Dispatcher.BeginInvoke(new Action(Close));
            }
        }
        finally { closeInProgress = false; }
    }
}
