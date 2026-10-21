using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace SinalMap.Desktop.Wpf.Controls;

/// <summary>Posiciona a leitura real em uma escala de dBm; ausência não recebe marcador.</summary>
public sealed class RssiRuler : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(int?),
        typeof(RssiRuler), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
            (owner, args) =>
            {
                if (UIElementAutomationPeer.FromElement((RssiRuler)owner) is { } peer)
                    peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, Describe((int?)args.OldValue), Describe((int?)args.NewValue));
            }));

    public int? Value { get => (int?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 140 || ActualHeight < 40) return;
        var min = Math.Min(-100d, Value is { } low ? Math.Floor(low / 20d) * 20 : -100);
        var max = Math.Max(-20d, Value is { } high ? Math.Ceiling(high / 20d) * 20 : -20);
        const double inset = 14;
        const double railY = 14;
        var line = new Pen(ThemeBrush("ControlBorder"), 1);
        dc.DrawLine(line, new Point(inset, railY), new Point(ActualWidth - inset, railY));
        for (var i = 0; i <= 4; i++)
        {
            var value = min + (max - min) * i / 4;
            var x = X(value);
            dc.DrawLine(line, new Point(x, railY), new Point(x, railY + 6));
            var text = new FormattedText(value.ToString("0", CultureInfo.InvariantCulture), CultureInfo.GetCultureInfo("pt-BR"),
                FlowDirection.LeftToRight, new Typeface((FontFamily)FindResource("MeasurementFont"), FontStyles.Normal,
                    FontWeights.Normal, FontStretches.Normal), 11, ThemeBrush("Metadata"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point(Math.Clamp(x - text.Width / 2, 0, Math.Max(0, ActualWidth - text.Width)), railY + 10));
        }
        if (Value is { } observed)
        {
            var x = X(observed);
            var marker = new StreamGeometry();
            using (var path = marker.Open())
            {
                path.BeginFigure(new Point(x - 4, 2), isFilled: true, isClosed: true);
                path.LineTo(new Point(x + 4, 2), true, false);
                path.LineTo(new Point(x, 8), true, false);
            }
            marker.Freeze();
            dc.DrawGeometry(ThemeBrush("Accent"), null, marker);
            dc.DrawLine(new Pen(ThemeBrush("Accent"), 2), new Point(x, 10), new Point(x, railY + 5));
        }
        double X(double value) => inset + (value - min) / (max - min) * (ActualWidth - 2 * inset);
        Brush ThemeBrush(string key) => (Brush)FindResource(key);
    }

    private static string Describe(int? value) => value is { } rssi ? $"Régua de RSSI: {rssi} dBm" : "Régua de RSSI: sem leitura";
    protected override AutomationPeer OnCreateAutomationPeer() => new RulerPeer(this);
    private sealed class RulerPeer(RssiRuler ruler) : FrameworkElementAutomationPeer(ruler)
    {
        protected override string GetNameCore() => Describe(ruler.Value);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;
        protected override string GetClassNameCore() => nameof(RssiRuler);
    }
}
