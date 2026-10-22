using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Services;

public sealed class SignalVisualBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Application.Current.FindResource(value switch
        {
            SignalVisualBand.Strong => "SignalStrong",
            SignalVisualBand.Good => "SignalGood",
            SignalVisualBand.Moderate => "SignalModerate",
            SignalVisualBand.Weak => "SignalWeak",
            SignalVisualBand.VeryWeak => "SignalVeryWeak",
            _ => "Metadata"
        });
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}
