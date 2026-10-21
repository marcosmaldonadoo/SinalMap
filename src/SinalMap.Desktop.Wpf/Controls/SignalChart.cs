using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Controls;

/// <summary>Traça somente pontos observados; descontinuidades não são interpoladas.</summary>
public sealed class SignalChart : FrameworkElement
{
    private Point? pointer;
    public SignalChart()
    {
        Cursor = Cursors.Cross;
    }

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points),
        typeof(IReadOnlyList<SignalPoint>), typeof(SignalChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<SignalPoint>? Points
    {
        get => (IReadOnlyList<SignalPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }
    public static readonly DependencyProperty TimeAxisLabelProperty = DependencyProperty.Register(nameof(TimeAxisLabel),
        typeof(string), typeof(SignalChart), new FrameworkPropertyMetadata("Tempo planejado (s)", FrameworkPropertyMetadataOptions.AffectsRender));
    public string TimeAxisLabel
    {
        get => (string)GetValue(TimeAxisLabelProperty);
        set => SetValue(TimeAxisLabelProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 130 || ActualHeight < 100) return;
        var plot = new Rect(52, 34, ActualWidth - 74, ActualHeight - 84);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(16, 28, 48)), null,
            new Rect(0, 0, ActualWidth, ActualHeight), 8, 8);
        var points = Points ?? [];
        var values = points.Where(p => p.RssiDbm.HasValue).Select(p => p.RssiDbm!.Value).ToArray();
        var min = Math.Min(-100d, values.Length == 0 ? -100 : Math.Floor(values.Min() / 10d) * 10);
        var max = Math.Max(-20d, values.Length == 0 ? -20 : Math.Ceiling(values.Max() / 10d) * 10);
        var end = Math.Max(60, points.Count == 0 ? 60 : Math.Ceiling(points.Max(p => p.Seconds) / 10) * 10);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(39, 57, 77)), 1);
        var stroke = new SolidColorBrush(Color.FromRgb(45, 212, 191));
        var line = new Pen(stroke, 2);
        var tickCount = (int)Math.Clamp(Math.Floor(plot.Height / 24), 2, 8);
        for (var tick = 0; tick <= tickCount; tick++)
        {
            var value = min + (max - min) * tick / tickCount;
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
        Label("RSSI (dBm)", 16, 9);
        Label(TimeAxisLabel, Math.Max(52, ActualWidth - 166), ActualHeight - 22);
        Point? previous = null;
        foreach (var observation in points)
        {
            if (observation.RssiDbm is not { } rssi) { previous = null; continue; }
            var point = new Point(X(observation.Seconds), Y(rssi));
            if (!observation.BreakBefore && previous is { } from && point.X >= from.X) dc.DrawLine(line, from, point);
            dc.DrawEllipse(stroke, null, point, 2, 2);
            previous = point;
        }
        if (values.Length == 0)
        {
            Label("Nenhuma leitura de sinal disponível", plot.Left + 18, plot.Top + plot.Height / 2 - 10);
            Label("Aguarde o adaptador ou abra uma coleta salva.", plot.Left + 18, plot.Top + plot.Height / 2 + 12);
        }
        if (points.LastOrDefault() is { RssiDbm: { } lastValue } last)
        {
            var lastPoint = new Point(X(last.Seconds), Y(lastValue));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(45, 45, 212, 191)), null, lastPoint, 7, 7);
            dc.DrawEllipse(stroke, null, lastPoint, 3.5, 3.5);
        }
        if (pointer is { } mouse && plot.Contains(mouse) && points.Count > 0)
        {
            var selected = points.MinBy(p => Math.Abs(X(p.Seconds) - mouse.X))!;
            var x = X(selected.Seconds);
            if (Math.Abs(x - mouse.X) <= 14)
            {
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(124, 157, 177)), 1) { DashStyle = DashStyles.Dash },
                    new Point(x, plot.Top), new Point(x, plot.Bottom));
                if (selected.RssiDbm is { } selectedValue)
                    dc.DrawEllipse(stroke, new Pen(Brushes.White, 1.5), new Point(x, Y(selectedValue)), 5, 5);
                var caption = selected.Seconds.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) + " s · " +
                    (selected.RssiDbm is { } value ? $"{value} dBm" : "Sem leitura");
                var text = new FormattedText(caption, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"), 12, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                var width = text.Width + 18;
                var labelX = Math.Clamp(x + 12, plot.Left + 4, Math.Max(plot.Left + 4, plot.Right - width));
                var labelY = Math.Clamp((selected.RssiDbm is { } rssi ? Y(rssi) : mouse.Y) - 38,
                    plot.Top + 4, Math.Max(plot.Top + 4, plot.Bottom - 29));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(38, 60, 80)),
                    new Pen(new SolidColorBrush(Color.FromRgb(92, 124, 146)), 1),
                    new Rect(labelX, labelY, width, 26), 5, 5);
                dc.DrawText(text, new Point(labelX + 9, labelY + 5));
            }
        }

        double X(double seconds) => plot.Left + seconds / end * plot.Width;
        double Y(double value) => plot.Bottom - (value - min) / (max - min) * plot.Height;
        void Label(string text, double x, double y)
        {
            var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, new SolidColorBrush(Color.FromRgb(158, 181, 202)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formatted, new Point(x, y));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        pointer = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pointer = null;
        InvalidateVisual();
    }
}
