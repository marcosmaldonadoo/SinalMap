using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SinalMap.Desktop.Wpf.Controls;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf;

public partial class FloorPlanWindow : Window
{
    private bool allowClose;
    private bool checkingClose;
    public FloorPlanWindow() => InitializeComponent();

    private void OnPositionChosen(object? sender, PlanPositionEventArgs e)
    {
        if (DataContext is FloorPlanViewModel vm) vm.Place(e.Position);
    }
    private void OnPointSelected(object? sender, PlanPointSelectedEventArgs e)
    {
        if (DataContext is FloorPlanViewModel vm) vm.SelectPoint(e.PointId);
    }
    private void OnCanvasKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is FloorPlanViewModel vm) { vm.CancelPlacement(); e.Handled = true; }
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose || DataContext is not FloorPlanViewModel vm) return;
        e.Cancel = true;
        if (checkingClose) return;
        checkingClose = true;
        try
        {
            if (await vm.CanCloseAsync()) { allowClose = true; _ = Dispatcher.BeginInvoke(new Action(Close)); }
        }
        finally { checkingClose = false; }
    }
}
