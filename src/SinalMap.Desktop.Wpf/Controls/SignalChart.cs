using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Controls;

/// <summary>Traça somente pontos observados; descontinuidades não são interpoladas.</summary>
public sealed class SignalChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points),
        typeof(IReadOnlyList<SignalPoint>), typeof(SignalChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<SignalPoint>? Points
    {
        get => (IReadOnlyList<SignalPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 130 || ActualHeight < 100) return;
        var plot = new Rect(52, 18, ActualWidth - 72, ActualHeight - 62);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(15, 27, 39)), null,
            new Rect(0, 0, ActualWidth, ActualHeight), 8, 8);
        var points = Points ?? [];
        var values = points.Where(p => p.RssiDbm.HasValue).Select(p => p.RssiDbm!.Value).ToArray();
        var min = Math.Min(-100d, values.Length == 0 ? -100 : Math.Floor(values.Min() / 10d) * 10);
        var max = Math.Max(-20d, values.Length == 0 ? -20 : Math.Ceiling(values.Max() / 10d) * 10);
        var end = Math.Max(60, points.Count == 0 ? 60 : Math.Ceiling(points.Max(p => p.Seconds) / 10) * 10);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(47, 65, 82)), 1);
        var stroke = new SolidColorBrush(Color.FromRgb(58, 180, 255));
        var line = new Pen(stroke, 2);
        for (var tick = 0; tick <= 8; tick++)
        {
            var value = min + (max - min) * tick / 8;
            var y = Y(value);
            dc.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(value.ToString("0", CultureInfo.InvariantCulture), 8, y - 7);
        }
        for (var i = 0; i <= 6; i++)
        {
            var seconds = end * i / 6;
            var x = X(seconds);
            dc.DrawLine(grid, new Point(x, plot.Top), new Point(x, plot.Bottom));
            Label(seconds.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")), x - 7, plot.Bottom + 7);
        }
        Label("dBm", 8, 0);
        Label("Tempo planejado (s)", Math.Max(52, ActualWidth - 165), ActualHeight - 20);
        Point? previous = null;
        foreach (var observation in points)
        {
            if (observation.RssiDbm is not { } rssi) { previous = null; continue; }
            var point = new Point(X(observation.Seconds), Y(rssi));
            if (!observation.BreakBefore && previous is { } from) dc.DrawLine(line, from, point);
            dc.DrawEllipse(stroke, null, point, 2.7, 2.7);
            previous = point;
        }
        if (values.Length == 0) Label("Aguardando leituras válidas de RSSI", plot.Left + 18, plot.Top + plot.Height / 2);

        double X(double seconds) => plot.Left + seconds / end * plot.Width;
        double Y(double value) => plot.Bottom - (value - min) / (max - min) * plot.Height;
        void Label(string text, double x, double y)
        {
            var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, new SolidColorBrush(Color.FromRgb(184, 205, 222)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formatted, new Point(x, y));
        }
    }
}
