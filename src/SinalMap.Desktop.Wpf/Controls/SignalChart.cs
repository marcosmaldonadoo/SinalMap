using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Controls;

/// <summary>Traça somente pontos observados; descontinuidades não são interpoladas.</summary>
public sealed class SignalChart : FrameworkElement
{
    private Point? pointer;
    private int? keyboardIndex;
    private string selectionDescription = "";
    public SignalChart()
    {
        Cursor = Cursors.Cross;
        Focusable = true;
        KeyboardNavigation.SetIsTabStop(this, true);
    }

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(nameof(Points),
        typeof(IReadOnlyList<SignalPoint>), typeof(SignalChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
            (owner, _) => ((SignalChart)owner).OnPointsChanged()));
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
        dc.DrawRoundedRectangle(ThemeBrush("ScopeSurface"), null,
            new Rect(0, 0, ActualWidth, ActualHeight), 8, 8);
        var points = Points ?? [];
        var values = points.Where(p => p.RssiDbm.HasValue).Select(p => p.RssiDbm!.Value).ToArray();
        var min = Math.Min(-100d, values.Length == 0 ? -100 : Math.Floor(values.Min() / 10d) * 10);
        var max = Math.Max(-20d, values.Length == 0 ? -20 : Math.Ceiling(values.Max() / 10d) * 10);
        var end = Math.Max(60, points.Count == 0 ? 60 : Math.Ceiling(points.Max(p => p.Seconds) / 10) * 10);
        var grid = new Pen(ThemeBrush("ScopeGrid"), 1);
        var stroke = ThemeBrush("ScopeTrace");
        var line = new Pen(stroke, 2);
        var tickCount = (int)Math.Clamp(Math.Floor(plot.Height / 25), 4, 8);
        var step = 10d;
        while ((max - min) / step > tickCount) step *= 2;
        min = Math.Floor(min / step) * step;
        max = Math.Ceiling(max / step) * step;
        for (var value = min; value <= max; value += step)
        {
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
        var segment = new List<Point>();
        foreach (var observation in points)
        {
            if (observation.RssiDbm is not { } rssi) { FillSegment(); continue; }
            var point = new Point(X(observation.Seconds), Y(rssi));
            if (observation.BreakBefore || segment.Count > 0 && point.X < segment[^1].X) FillSegment();
            segment.Add(point);
        }
        FillSegment();
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
            dc.DrawEllipse(ThemeBrush("ScopeHalo"), null, lastPoint, 7, 7);
            dc.DrawEllipse(stroke, null, lastPoint, 3.5, 3.5);
        }
        SignalPoint? selected = keyboardIndex is { } index && index >= 0 && index < points.Count ? points[index] : null;
        if (selected is null && pointer is { } mouse && plot.Contains(mouse) && points.Count > 0)
        {
            var candidate = points.MinBy(p => Math.Abs(X(p.Seconds) - mouse.X))!;
            if (Math.Abs(X(candidate.Seconds) - mouse.X) <= 14) selected = candidate;
        }
        if (selected is not null)
        {
            var x = X(selected.Seconds);
            dc.DrawLine(new Pen(ThemeBrush("ScopeGuide"), 1) { DashStyle = DashStyles.Dash },
                new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (selected.RssiDbm is { } selectedValue)
                dc.DrawEllipse(stroke, new Pen(ThemeBrush("ScopeOnSelection"), 1.5), new Point(x, Y(selectedValue)), 5, 5);
            var caption = Describe(selected);
            var text = new FormattedText(caption, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                MeasurementTypeface(), 12, ThemeBrush("ScopeOnSelection"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var width = text.Width + 18;
            var labelX = Math.Clamp(x + 12, plot.Left + 4, Math.Max(plot.Left + 4, plot.Right - width));
            var labelY = Math.Clamp((selected.RssiDbm is { } rssi ? Y(rssi) : plot.Top + plot.Height / 2) - 38,
                plot.Top + 4, Math.Max(plot.Top + 4, plot.Bottom - 29));
            dc.DrawRoundedRectangle(ThemeBrush("ScopeSelection"),
                new Pen(ThemeBrush("ScopeSelectionBorder"), 1),
                new Rect(labelX, labelY, width, 26), 5, 5);
            dc.DrawText(text, new Point(labelX + 9, labelY + 5));
        }
        if (IsKeyboardFocused)
            dc.DrawRoundedRectangle(null, new Pen(stroke, 2), new Rect(1, 1, ActualWidth - 2, ActualHeight - 2), 8, 8);

        double X(double seconds) => plot.Left + seconds / end * plot.Width;
        double Y(double value) => plot.Bottom - (value - min) / (max - min) * plot.Height;
        void FillSegment()
        {
            if (segment.Count > 1)
            {
                var area = new StreamGeometry();
                using (var path = area.Open())
                {
                    path.BeginFigure(new Point(segment[0].X, plot.Bottom), isFilled: true, isClosed: true);
                    path.PolyLineTo(segment, isStroked: false, isSmoothJoin: false);
                    path.LineTo(new Point(segment[^1].X, plot.Bottom), isStroked: false, isSmoothJoin: false);
                }
                area.Freeze();
                dc.DrawGeometry(ThemeBrush("ScopeArea"), null, area);
            }
            segment.Clear();
        }
        void Label(string text, double x, double y)
        {
            var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
                MeasurementTypeface(), 11, ThemeBrush("ScopeText"), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formatted, new Point(x, y));
        }
    }

    private Brush ThemeBrush(string key) => (Brush)FindResource(key);
    private Typeface MeasurementTypeface() => new((FontFamily)FindResource("MeasurementFont"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        keyboardIndex = null;
        UpdateSelectionDescription();
        pointer = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pointer = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        keyboardIndex = null;
        UpdateSelectionDescription();
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (Points is { Count: > 0 } points) keyboardIndex = points.Count - 1;
        UpdateSelectionDescription();
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        keyboardIndex = null;
        UpdateSelectionDescription();
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Points is not { Count: > 0 } points) return;
        switch (e.Key)
        {
            case Key.Left: keyboardIndex = keyboardIndex is { } left ? Math.Max(0, left - 1) : points.Count - 1; break;
            case Key.Right: keyboardIndex = keyboardIndex is { } right ? Math.Min(points.Count - 1, right + 1) : 0; break;
            case Key.Home: keyboardIndex = 0; break;
            case Key.End: keyboardIndex = points.Count - 1; break;
            case Key.Escape: keyboardIndex = null; break;
            default: return;
        }
        e.Handled = true;
        UpdateSelectionDescription();
        InvalidateVisual();
    }

    private void OnPointsChanged()
    {
        if (keyboardIndex is { } index) keyboardIndex = Points is { Count: > 0 } points ? Math.Min(index, points.Count - 1) : null;
        UpdateSelectionDescription();
    }

    private static string Describe(SignalPoint point) => point.Seconds.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR")) +
        " s · " + (point.RssiDbm is { } value ? $"{value} dBm" : "Sem leitura");

    private void UpdateSelectionDescription()
    {
        var previous = selectionDescription;
        selectionDescription = keyboardIndex is { } index && Points is { } points && index >= 0 && index < points.Count ? Describe(points[index]) : "";
        if (previous != selectionDescription && UIElementAutomationPeer.FromElement(this) is { } peer)
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, previous, selectionDescription);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SignalChartPeer(this);

    private sealed class SignalChartPeer(SignalChart chart) : FrameworkElementAutomationPeer(chart)
    {
        protected override string GetClassNameCore() => nameof(SignalChart);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        protected override string GetItemStatusCore() => chart.selectionDescription;
    }
}
