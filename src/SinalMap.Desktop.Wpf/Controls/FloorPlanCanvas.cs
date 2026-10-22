using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using SinalMap.Domain;
using SinalMap.Presentation;

namespace SinalMap.Desktop.Wpf.Controls;

public sealed class PlanPositionEventArgs(PlanPosition position) : EventArgs
{
    public PlanPosition Position { get; } = position;
}

public sealed class PlanPointSelectedEventArgs(Guid pointId) : EventArgs
{
    public Guid PointId { get; } = pointId;
}

/// <summary>Imagem e marcações manuais; não estima localização, distância ou cobertura do rádio.</summary>
public sealed class FloorPlanCanvas : FrameworkElement
{
    private const double ImageInset = 24;
    private const double HitRadius = 22;
    private PlanPosition placementCursor = new(.5, .5);
    private string automationStatus = "";
    private bool cursorVisible;
    // O hit test acompanha as etiquetas visíveis até a próxima renderização.
    private readonly List<(Guid PointId, Rect Bounds)> pointLabelBounds = [];

    public FloorPlanCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        KeyboardNavigation.SetIsTabStop(this, true);
        UpdateState();
    }

    public event EventHandler<PlanPositionEventArgs>? PositionChosen;
    public event EventHandler<PlanPointSelectedEventArgs>? PointSelected;

    public static readonly DependencyProperty ImageProperty = Register(nameof(Image), typeof(ImageSource), null);
    public static readonly DependencyProperty RouterProperty = Register(nameof(Router), typeof(PlanPosition), null);
    public static readonly DependencyProperty PointsProperty = Register(nameof(Points), typeof(IReadOnlyList<FloorPlanPoint>), null);
    public static readonly DependencyProperty PointFeedbackProperty = Register(nameof(PointFeedback), typeof(IReadOnlyList<PlanPointFeedback>), null);
    public static readonly DependencyProperty ShowPointResultsProperty = Register(nameof(ShowPointResults), typeof(bool), true);
    public static readonly DependencyProperty CollectionPointIdProperty = Register(nameof(CollectionPointId), typeof(Guid?), null);
    public static readonly DependencyProperty LiveRssiDbmProperty = Register(nameof(LiveRssiDbm), typeof(int?), null);
    public static readonly DependencyProperty SelectedPointIdProperty = Register(nameof(SelectedPointId), typeof(Guid?), null);
    public static readonly DependencyProperty PlacementModeProperty = Register(nameof(PlacementMode), typeof(FloorPlanPlacementMode), FloorPlanPlacementMode.Select);
    public static readonly DependencyProperty IsPlacementEnabledProperty = Register(nameof(IsPlacementEnabled), typeof(bool), true);

    public ImageSource? Image { get => (ImageSource?)GetValue(ImageProperty); set => SetValue(ImageProperty, value); }
    public PlanPosition? Router { get => (PlanPosition?)GetValue(RouterProperty); set => SetValue(RouterProperty, value); }
    public IReadOnlyList<FloorPlanPoint>? Points { get => (IReadOnlyList<FloorPlanPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public IReadOnlyList<PlanPointFeedback>? PointFeedback { get => (IReadOnlyList<PlanPointFeedback>?)GetValue(PointFeedbackProperty); set => SetValue(PointFeedbackProperty, value); }
    public bool ShowPointResults { get => (bool)GetValue(ShowPointResultsProperty); set => SetValue(ShowPointResultsProperty, value); }
    public Guid? CollectionPointId { get => (Guid?)GetValue(CollectionPointIdProperty); set => SetValue(CollectionPointIdProperty, value); }
    public int? LiveRssiDbm { get => (int?)GetValue(LiveRssiDbmProperty); set => SetValue(LiveRssiDbmProperty, value); }
    public Guid? SelectedPointId { get => (Guid?)GetValue(SelectedPointIdProperty); set => SetValue(SelectedPointIdProperty, value); }
    public FloorPlanPlacementMode PlacementMode { get => (FloorPlanPlacementMode)GetValue(PlacementModeProperty); set => SetValue(PlacementModeProperty, value); }
    public bool IsPlacementEnabled { get => (bool)GetValue(IsPlacementEnabledProperty); set => SetValue(IsPlacementEnabledProperty, value); }

    private static DependencyProperty Register(string name, Type type, object? initial) =>
        DependencyProperty.Register(name, type, typeof(FloorPlanCanvas),
            new FrameworkPropertyMetadata(initial, FrameworkPropertyMetadataOptions.AffectsRender,
                (owner, args) => ((FloorPlanCanvas)owner).OnPropertyChanged(args.Property)));

    private void OnPropertyChanged(DependencyProperty property)
    {
        if (property == PlacementModeProperty || property == SelectedPointIdProperty || property == ImageProperty)
        {
            placementCursor = PlacementMode == FloorPlanPlacementMode.Router && FloorPlanViewport.IsNormalized(Router) ? Router! :
                Points?.FirstOrDefault(point => point.Id == SelectedPointId)?.Position ?? new(.5, .5);
            cursorVisible = IsKeyboardFocused && PlacementMode != FloorPlanPlacementMode.Select;
        }
        UpdateState();
    }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : 640,
        double.IsFinite(availableSize.Height) ? Math.Max(0, availableSize.Height) : 480);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        pointLabelBounds.Clear();
        if (ActualWidth < 2 || ActualHeight < 2) return;
        dc.DrawRoundedRectangle(ThemeBrush("ControlSurface"), null, new Rect(0, 0, ActualWidth, ActualHeight), 8, 8);
        var viewport = CurrentViewport();
        if (Image is not null && viewport.IsValid)
        {
            var imageRect = new Rect(viewport.Left, viewport.Top, viewport.Width, viewport.Height);
            dc.DrawRectangle(ThemeBrush("Surface"), null, imageRect);
            dc.DrawImage(Image, imageRect);
            dc.DrawRectangle(null, new Pen(ThemeBrush("Line"), 1), imageRect);
            var points = Points ?? [];
            var markerBounds = new List<Rect>();
            var rendered = new List<(FloorPlanPoint Point, Point Center, PlanPointFeedback? Feedback)>();
            for (var index = 0; index < points.Count; index++)
            {
                var point = points[index];
                if (point is not null && viewport.ToDisplay(point.Position) is { } pixel)
                {
                    var center = new Point(pixel.X, pixel.Y);
                    var feedback = FeedbackFor(point.Id);
                    DrawPoint(dc, center, feedback?.Number ?? index + 1, point.Id == SelectedPointId || point.Id == CollectionPointId, feedback);
                    rendered.Add((point, center, feedback));
                    markerBounds.Add(new(center.X - HitRadius, center.Y - HitRadius, HitRadius * 2, HitRadius * 2));
                }
            }
            if (viewport.ToDisplay(Router) is { } router)
            {
                DrawRouter(dc, new(router.X, router.Y));
                markerBounds.Add(new(router.X - 20, router.Y - 20, 40, 40));
            }
            if (ShowPointResults)
                foreach (var item in rendered.OrderByDescending(item => item.Point.Id == CollectionPointId ? 2 : item.Point.Id == SelectedPointId ? 1 : 0))
                    if (item.Feedback is { } feedback)
                        DrawPointResult(dc, viewport, item.Center, feedback, markerBounds);
            if (cursorVisible && IsPlacementEnabled && PlacementMode != FloorPlanPlacementMode.Select &&
                viewport.ToDisplay(placementCursor) is { } cursor)
            {
                var center = new Point(cursor.X, cursor.Y);
                DrawCross(dc, new Pen(ThemeBrush("Surface"), 4), center);
                DrawCross(dc, new Pen(ThemeBrush("Ink"), 1.5), center);
            }
        }
        if (IsKeyboardFocused)
            dc.DrawRoundedRectangle(null, new Pen(ThemeBrush("ControlFocus"), 2),
                new Rect(1, 1, Math.Max(0, ActualWidth - 2), Math.Max(0, ActualHeight - 2)), 8, 8);
    }

    private void DrawPoint(DrawingContext dc, Point center, int number, bool selected, PlanPointFeedback? feedback)
    {
        if (feedback is null)
        {
            if (selected) dc.DrawEllipse(ThemeBrush("Surface"), new Pen(ThemeBrush("Accent"), 2), center, 18, 18);
            dc.DrawEllipse(ThemeBrush(selected ? "Accent" : "Surface"), new Pen(ThemeBrush("Accent"), 2), center, 12, 12);
            CenterText(dc, number.ToString(CultureInfo.InvariantCulture), center, ThemeBrush(selected ? "Surface" : "Ink"), 12);
            return;
        }
        var fill = FeedbackBrush(feedback);
        if (selected) dc.DrawEllipse(ThemeBrush("Surface"), new Pen(ThemeBrush("Accent"), 2), center, 22, 22);
        var neutral = IsNeutral(feedback);
        var outline = new Pen(ThemeBrush(neutral ? "Metadata" : "Surface"), 2);
        if (feedback.HasPlacementMismatch) outline.DashStyle = DashStyles.Dash;
        dc.DrawEllipse(fill, outline, center, 16, 16);
        CenterText(dc, number.ToString(CultureInfo.InvariantCulture), center, FeedbackTextBrush(feedback, fill), 13);
    }

    private void DrawPointResult(DrawingContext dc, FloorPlanViewport viewport, Point center,
        PlanPointFeedback feedback, IReadOnlyList<Rect> markers)
    {
        var availableWidth = Math.Min(160, viewport.Width - 8);
        if (availableWidth < 48 || viewport.Height < 32) return;
        var caption = feedback.PointId == CollectionPointId ? feedback.ValueLabel :
            feedback.HasPlacementMismatch ? feedback.ValueLabel.StartsWith("Anterior", StringComparison.Ordinal) ? feedback.ValueLabel : $"Anterior · {feedback.ValueLabel}" : feedback.Band switch
        {
            SignalVisualBand.Unmeasured => "Não medido",
            SignalVisualBand.Unavailable => "Sem RSSI",
            _ => feedback.ValueLabel + (feedback.IsPartial ? " · parcial" : "")
        };
        var fill = FeedbackBrush(feedback);
        var formatted = new FormattedText(caption, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
            new Typeface((FontFamily)FindResource("MeasurementFont"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12, FeedbackTextBrush(feedback, fill), VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = availableWidth - 16,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        var width = Math.Min(availableWidth, formatted.WidthIncludingTrailingWhitespace + 16);
        const double height = 26;
        var left = Math.Clamp(center.X - width / 2, viewport.Left + 4, viewport.Left + viewport.Width - width - 4);
        var top = Math.Clamp(center.Y - height / 2, viewport.Top + 4, Math.Max(viewport.Top + 4, viewport.Top + viewport.Height - height - 4));
        var candidates = new[]
        {
            new Rect(left, center.Y - 24 - height, width, height),
            new Rect(left, center.Y + 24, width, height),
            new Rect(center.X + 24, top, width, height),
            new Rect(center.X - 24 - width, top, width, height)
        };
        var bounds = new Rect(viewport.Left + 2, viewport.Top + 2, Math.Max(0, viewport.Width - 4), Math.Max(0, viewport.Height - 4));
        foreach (var label in candidates)
        {
            if (!bounds.Contains(label) || markers.Any(marker => marker.IntersectsWith(label)) ||
                pointLabelBounds.Any(existing => existing.Bounds.IntersectsWith(label))) continue;
            dc.DrawRoundedRectangle(fill, new Pen(ThemeBrush(IsNeutral(feedback) ? "ControlBorder" : "Surface"), 1.5), label, 6, 6);
            dc.PushClip(new RectangleGeometry(label));
            dc.DrawText(formatted, new Point(label.Left + 8, label.Top + (height - formatted.Height) / 2));
            dc.Pop();
            pointLabelBounds.Add((feedback.PointId, label));
            return;
        }
        // Em pontos muito próximos a foto e os marcadores têm prioridade; o resultado continua no hover e no painel.
    }

    private static bool IsNeutral(PlanPointFeedback feedback) => feedback.HasPlacementMismatch ||
        feedback.Band is SignalVisualBand.Unmeasured or SignalVisualBand.Unavailable;

    private Brush FeedbackBrush(PlanPointFeedback feedback) => ThemeBrush(feedback.HasPlacementMismatch ? "ControlSurface" : feedback.Band switch
    {
        SignalVisualBand.Strong => "SignalStrong",
        SignalVisualBand.Good => "SignalGood",
        SignalVisualBand.Moderate => "SignalModerate",
        SignalVisualBand.Weak => "SignalWeak",
        SignalVisualBand.VeryWeak => "SignalVeryWeak",
        SignalVisualBand.Unavailable => "ControlSurface",
        _ => "Surface"
    });

    private Brush FeedbackTextBrush(PlanPointFeedback feedback, Brush background)
    {
        var ink = ThemeBrush("Ink");
        if (IsNeutral(feedback) || feedback.Band is SignalVisualBand.Strong or SignalVisualBand.Good or SignalVisualBand.Moderate) return ink;
        var light = ThemeBrush("Surface");
        if (background is not SolidColorBrush fill || ink is not SolidColorBrush dark || light is not SolidColorBrush pale) return ink;
        return Contrast(fill.Color, pale.Color) > Contrast(fill.Color, dark.Color) ? light : ink;

        static double Contrast(Color first, Color second)
        {
            var a = Luminance(first);
            var b = Luminance(second);
            return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        }
        static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        static double Linear(byte component)
        {
            var value = component / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
    }

    private PlanPointFeedback? FeedbackFor(Guid pointId)
    {
        var saved = PointFeedback?.FirstOrDefault(feedback => feedback.PointId == pointId);
        if (pointId != CollectionPointId) return saved;
        var point = Points?.FirstOrDefault(item => item.Id == pointId);
        if (point is null) return saved;
        var band = FloorPlanSignalFeedback.BandFor(LiveRssiDbm, LiveRssiDbm.HasValue ? 1 : 0);
        var value = LiveRssiDbm is { } rssi ? $"Ao vivo · {rssi.ToString(CultureInfo.InvariantCulture).Replace('-', '−')} dBm" :
            "Coletando · Sem RSSI";
        var number = saved?.Number ?? (Points!.TakeWhile(item => item.Id != pointId).Count() + 1);
        return new(point.Id, point.Name, point.Position, number, band, value,
            "Leitura ao vivo durante a coleta deste ponto. O resultado final será vinculado após salvar o JSON; esta leitura não é uma média salva.",
            false, false, false);
    }

    private string PointToolTip(FloorPlanPoint point) => FeedbackFor(point.Id) is { } feedback
        ? $"{feedback.Name} · {feedback.ValueLabel}\n{feedback.Detail}" : point.Name;

    private void DrawRouter(DrawingContext dc, Point center)
    {
        dc.DrawRoundedRectangle(ThemeBrush("Ink"), new Pen(ThemeBrush("Surface"), 2),
            new Rect(center.X - 15, center.Y - 15, 30, 30), 6, 6);
        CenterText(dc, "R", center, ThemeBrush("Surface"), 14);
    }

    private void CenterText(DrawingContext dc, string text, Point center, Brush brush, double size)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("pt-BR"), FlowDirection.LeftToRight,
            new Typeface((FontFamily)FindResource("MeasurementFont"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static void DrawCross(DrawingContext dc, Pen pen, Point center)
    {
        dc.DrawLine(pen, new(center.X - 10, center.Y), new(center.X + 10, center.Y));
        dc.DrawLine(pen, new(center.X, center.Y - 10), new(center.X, center.Y + 10));
    }

    private Brush ThemeBrush(string key) => (Brush)FindResource(key);

    private FloorPlanViewport CurrentViewport()
    {
        if (Image is not { } image) return default;
        var fitted = FloorPlanViewport.Fit(ActualWidth - ImageInset * 2, ActualHeight - ImageInset * 2, image.Width, image.Height);
        return fitted.IsValid ? fitted with { Left = fitted.Left + ImageInset, Top = fitted.Top + ImageInset } : default;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var wasCursorVisible = cursorVisible;
        var mouse = e.GetPosition(this);
        var viewport = CurrentViewport();
        if (viewport.ToNormalized(mouse.X, mouse.Y) is { } position)
        {
            placementCursor = position;
            cursorVisible = IsPlacementEnabled && PlacementMode != FloorPlanPlacementMode.Select;
            var point = FindPoint(viewport, mouse);
            ToolTip = point is not null ? PointToolTip(point) : PlacementHint();
        }
        else
        {
            cursorVisible = false;
            ToolTip = null;
        }
        UpdateState();
        if (cursorVisible || wasCursorVisible) InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        cursorVisible = IsKeyboardFocused && IsPlacementEnabled && PlacementMode != FloorPlanPlacementMode.Select;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        // O foco pode atualizar o layout; resolva o alvo com a geometria que recebeu o clique.
        var mouse = e.GetPosition(this);
        var viewport = CurrentViewport();
        var selected = PlacementMode == FloorPlanPlacementMode.Select ? FindPoint(viewport, mouse) : null;
        var position = viewport.ToNormalized(mouse.X, mouse.Y);
        Focus();
        if (!IsPlacementEnabled || position is null) return;
        if (PlacementMode == FloorPlanPlacementMode.Select)
        {
            if (selected is not null) PointSelected?.Invoke(this, new(selected.Id));
        }
        else
        {
            placementCursor = position;
            PositionChosen?.Invoke(this, new(position));
        }
        UpdateState();
        InvalidateVisual();
        e.Handled = true;
    }

    private FloorPlanPoint? FindPoint(FloorPlanViewport viewport, Point mouse)
    {
        for (var index = pointLabelBounds.Count - 1; index >= 0; index--)
            if (pointLabelBounds[index].Bounds.Contains(mouse))
                return Points?.FirstOrDefault(point => point.Id == pointLabelBounds[index].PointId);
        FloorPlanPoint? nearest = null;
        var minimum = HitRadius * HitRadius;
        foreach (var point in Points ?? [])
        {
            if (point is null || viewport.ToDisplay(point.Position) is not { } pixel) continue;
            var dx = pixel.X - mouse.X;
            var dy = pixel.Y - mouse.Y;
            var distance = dx * dx + dy * dy;
            if (distance <= minimum) { minimum = distance; nearest = point; }
        }
        return nearest;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        cursorVisible = IsPlacementEnabled && PlacementMode != FloorPlanPlacementMode.Select;
        UpdateState();
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        cursorVisible = false;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsPlacementEnabled || !CurrentViewport().IsValid) return;
        if (PlacementMode == FloorPlanPlacementMode.Select)
        {
            var points = Points ?? [];
            if (points.Count == 0) return;
            var selected = points.Select((point, index) => (point, index)).FirstOrDefault(item => item.point.Id == SelectedPointId);
            var index = selected.point is null ? 0 : selected.index;
            switch (e.Key)
            {
                case Key.Left or Key.Up: index = Math.Max(0, index - 1); break;
                case Key.Right or Key.Down: index = Math.Min(points.Count - 1, index + 1); break;
                case Key.Home: index = 0; break;
                case Key.End: index = points.Count - 1; break;
                default: return;
            }
            PointSelected?.Invoke(this, new(points[index].Id));
        }
        else
        {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? .0025 : .01;
            switch (e.Key)
            {
                case Key.Left: placementCursor = placementCursor with { X = Math.Max(0, placementCursor.X - step) }; break;
                case Key.Right: placementCursor = placementCursor with { X = Math.Min(1, placementCursor.X + step) }; break;
                case Key.Up: placementCursor = placementCursor with { Y = Math.Max(0, placementCursor.Y - step) }; break;
                case Key.Down: placementCursor = placementCursor with { Y = Math.Min(1, placementCursor.Y + step) }; break;
                case Key.Home: placementCursor = new(.5, .5); break;
                case Key.Enter: PositionChosen?.Invoke(this, new(placementCursor)); break;
                default: return;
            }
            cursorVisible = true;
        }
        UpdateState();
        InvalidateVisual();
        e.Handled = true;
    }

    private string PlacementHint() => PlacementMode switch
    {
        FloorPlanPlacementMode.Router => "Clique para marcar o roteador. Pelo teclado: setas e Enter; Shift ajusta com mais precisão; Home centraliza.",
        FloorPlanPlacementMode.Point => "Clique para marcar o ponto de medição. Pelo teclado: setas e Enter; Shift ajusta com mais precisão; Home centraliza.",
        _ => "Clique em um ponto para selecioná-lo. Pelo teclado: setas, Home e End alternam os pontos."
    };

    private void UpdateState()
    {
        Cursor = IsPlacementEnabled && PlacementMode != FloorPlanPlacementMode.Select ? Cursors.Cross : Cursors.Arrow;
        var selected = Points?.FirstOrDefault(point => point.Id == SelectedPointId);
        var mode = PlacementMode switch { FloorPlanPlacementMode.Router => "Marcar roteador", FloorPlanPlacementMode.Point => "Marcar ponto de medição", _ => "Selecionar ponto" };
        var status = Image is null ? "Nenhuma planta carregada" : !IsPlacementEnabled ? "Marcações bloqueadas durante a operação" :
            PlacementMode == FloorPlanPlacementMode.Select ? selected is null ? "Nenhum ponto selecionado" :
                FeedbackFor(selected.Id) is { } feedback ? $"Ponto selecionado: {feedback.Name} · {feedback.ValueLabel}. {feedback.Detail}" : $"Ponto selecionado: {selected.Name}" :
            $"{mode} · posição relativa X {placementCursor.X:P0}, Y {placementCursor.Y:P0}";
        if (automationStatus != status)
        {
            var previous = automationStatus;
            automationStatus = status;
            if (UIElementAutomationPeer.FromElement(this) is { } peer)
                peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, previous, automationStatus);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FloorPlanCanvasPeer(this);

    private sealed class FloorPlanCanvasPeer(FloorPlanCanvas canvas) : FrameworkElementAutomationPeer(canvas)
    {
        protected override string GetClassNameCore() => nameof(FloorPlanCanvas);
        protected override string GetNameCore() => "Planta da loja e posições de medição";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        protected override string GetHelpTextCore() => canvas.PlacementHint();
        protected override string GetItemStatusCore() => canvas.automationStatus;
    }
}
