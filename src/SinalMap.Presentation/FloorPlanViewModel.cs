using System.ComponentModel;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Presentation;

/// <summary>Coordena anotações manuais da planta sem alterar a aquisição ou o JSON de coleta.</summary>
public sealed class FloorPlanViewModel : ObservableObject
{
    private readonly IFloorPlanProjectStore store;
    private readonly IFloorPlanDialogs dialogs;
    private readonly IFloorPlanImageLoader images;
    private FloorPlanProject? project;
    private FloorPlanProject? savedProject;
    private FloorPlanPoint? selectedPoint;
    private PlanPlacement? placement;
    private string? projectPath;
    private bool busy;
    private bool dirty;
    private bool movingPoint;
    private FloorPlanPlacementMode placementMode;
    private FloorPlanProject? feedbackProject;
    private IReadOnlyList<PlanPointFeedback> pointFeedback = [];
    private SignalHistoryViewModel observedSignal;
    private bool showPointResults = true;
    private string error = "";
    private string status = "Importe uma foto ou imagem da planta para começar.";

    public FloorPlanViewModel(ProbeViewModel probe, IFloorPlanProjectStore store,
        IFloorPlanDialogs dialogs, IFloorPlanImageLoader images)
    {
        Probe = probe;
        this.store = store;
        this.dialogs = dialogs;
        this.images = images;
        ImportCommand = new(ImportAsync, ShowError, () => CanEdit);
        OpenCommand = new(OpenAsync, ShowError, () => CanEdit);
        SaveCommand = new(() => SaveAsync(), ShowError, () => CanEdit && HasProject);
        SaveAsCommand = new(() => SaveAsync(choosePath: true), ShowError, () => CanEdit && HasProject);
        MarkRouterCommand = new(() => { SetMode(FloorPlanPlacementMode.Router); return Task.CompletedTask; }, ShowError, () => CanEdit && HasProject);
        AddPointCommand = new(() => { movingPoint = false; SetMode(FloorPlanPlacementMode.Point); return Task.CompletedTask; }, ShowError, () => CanEdit && HasProject && Points.Count < FloorPlanValidation.MaximumPoints);
        MovePointCommand = new(() => { movingPoint = true; SetMode(FloorPlanPlacementMode.Point); return Task.CompletedTask; }, ShowError, () => CanMovePoint);
        MeasureCommand = new(MeasureAsync, ShowError, () => CanMeasure);
        RetrySessionSaveCommand = new(RetrySessionSaveAsync, ShowError, () => !busy && Probe.RetrySaveCommand.CanExecute(null));
        Probe.PropertyChanged += OnProbeChanged;
        Probe.SessionSaved += OnSessionSaved;
        observedSignal = Probe.Signal;
        observedSignal.PropertyChanged += OnSignalChanged;
    }

    public ProbeViewModel Probe { get; }
    public AsyncCommand ImportCommand { get; }
    public AsyncCommand OpenCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand SaveAsCommand { get; }
    public AsyncCommand MarkRouterCommand { get; }
    public AsyncCommand AddPointCommand { get; }
    public AsyncCommand MovePointCommand { get; }
    public AsyncCommand MeasureCommand { get; }
    public AsyncCommand RetrySessionSaveCommand { get; }
    public bool HasProject => project is not null;
    public bool IsDirty => dirty;
    public bool RequiresCloseReview => dirty || busy || placement is not null;
    public bool CanEdit => !busy && Probe.CanReplaceSession;
    public bool CanEditProject => CanEdit && HasProject;
    public bool CanMovePoint => CanEdit && SelectedPoint is not null && SelectedMeasurements.Count == 0;
    public bool CanMeasure => CanEdit && project?.Router is not null && project.Measurements.Count < FloorPlanValidation.MaximumMeasurements &&
        SelectedPoint is not null && Probe.StartCommand.CanExecute(null);
    public bool HasError => !string.IsNullOrEmpty(Error);
    public bool HasSelectedPoint => SelectedPoint is not null;
    public bool HasSelectedMeasurements => SelectedMeasurements.Count > 0;
    public string Error { get => error; private set { Set(ref error, value); Notify(nameof(HasError)); } }
    public string Status { get => status; private set => Set(ref status, value); }
    public string ProjectFileLabel => projectPath is null ? "Projeto ainda não salvo" : Path.GetFileName(projectPath);
    public string SaveState => IsDirty ? "Alterações não salvas" : HasProject ? "Projeto salvo" : "Somente no seu computador";
    public string ImageDescription => project is { } p ? $"{p.ImageFileName} · {p.ImagePixelWidth} × {p.ImagePixelHeight}" : "PNG ou JPEG · até 20 MB";
    public byte[]? ImageBytes => project?.ImageBytes;
    public PlanPosition? Router => project?.Router;
    public IReadOnlyList<FloorPlanPoint> Points => project?.Points ?? [];
    public IReadOnlyList<PlanPointFeedback> PointFeedback
    {
        get
        {
            if (!ReferenceEquals(feedbackProject, project))
            {
                feedbackProject = project;
                pointFeedback = FloorPlanSignalFeedback.Create(project);
            }
            return pointFeedback;
        }
    }
    public Guid? SelectedPointId { get => SelectedPoint?.Id; set { if (value is { } id) SelectPoint(id); } }
    public PlanPointFeedback? SelectedPointFeedback => PointFeedback.FirstOrDefault(p => p.PointId == SelectedPointId);
    public bool ShowPointResults { get => showPointResults; set => Set(ref showPointResults, value); }
    public Guid? CollectionPointId => Probe.Signal.IsCollecting && Probe.Operation is ProbeOperation.Collecting or ProbeOperation.Cancelling ? placement?.PointId : null;
    public int? LiveRssiDbm => CollectionPointId is not null ? Probe.Signal.CurrentRssiDbm : null;
    public string LegendCaption => CollectionPointId is not null ?
        "Ponto ativo: leitura ao vivo · demais pontos: média da última coleta salva" :
        "RSSI médio da última coleta salva · faixas em dBm";
    public string VisualSummary
    {
        get
        {
            var feedback = PointFeedback;
            var measured = feedback.Count(p => p.Band is not (SignalVisualBand.Unmeasured or SignalVisualBand.Unavailable));
            var pending = feedback.Count(p => !p.HasMeasurement);
            var unavailable = feedback.Count - measured - pending;
            return $"{measured} de {feedback.Count} com RSSI" +
                (pending > 0 ? $" · {pending} {(pending == 1 ? "não medido" : "não medidos")}" : "") +
                (unavailable > 0 ? $" · {unavailable} sem valor atual" : "");
        }
    }
    public FloorPlanPlacementMode PlacementMode => placementMode;
    public string RouterStatus => Router is null ? "Ainda não marcado" : "Posição marcada por você";
    public string PlacementHint => PlacementMode switch
    {
        FloorPlanPlacementMode.Router => "Clique na imagem onde fica o roteador. Enter confirma a posição pelo teclado.",
        FloorPlanPlacementMode.Point => movingPoint ? "Clique para ajustar este ponto sem coletas." : "Clique na imagem onde o notebook fará a medição.",
        _ => "Selecione um ponto na planta ou na lista. As marcações são manuais."
    };
    public string MeasurementHint => !HasProject ? "Importe a planta da loja." : Router is null ? "Marque onde fica o roteador." :
        SelectedPoint is null ? "Marque e selecione um ponto de medição." : Probe.HasPendingSave ? "Salve ou descarte a coleta pendente antes de continuar." :
        project!.Measurements.Count >= FloorPlanValidation.MaximumMeasurements ? "Este projeto atingiu o limite de coletas. Inicie outro projeto para continuar." :
        Probe.HasTargetError ? "Confira o destino IPv4 na janela principal." : !Probe.StartCommand.CanExecute(null) && !Probe.IsBusy ?
        "Confira a interface Wi-Fi e o destino IPv4 na janela principal." : $"Vá com o notebook até {SelectedPoint.Name} e permaneça ali durante os 60 segundos.";
    public string ProjectName
    {
        get => project?.Name ?? "";
        set
        {
            if (!CanEdit || project is null || project.Name == value) return;
            project = project with { Name = value };
            MarkDirty(); Notify();
        }
    }
    public FloorPlanPoint? SelectedPoint
    {
        get => selectedPoint;
        set
        {
            // A ListBox publica null ao substituir sua lista. A seleção por ID continua válida.
            if (!CanEdit || value is null || !Points.Any(p => p.Id == value.Id)) return;
            if (!Set(ref selectedPoint, value)) return;
            movingPoint = false;
            SetMode(FloorPlanPlacementMode.Select);
            Refresh();
        }
    }
    public string SelectedPointName
    {
        get => SelectedPoint?.Name ?? "";
        set
        {
            if (!CanEdit || project is null || selectedPoint is null || selectedPoint.Name == value) return;
            var changed = selectedPoint with { Name = value };
            project = project with { Points = Points.Select(p => p.Id == changed.Id ? changed : p).ToArray() };
            selectedPoint = changed;
            MarkDirty(); Notify(nameof(Points)); Notify(nameof(SelectedPoint)); Refresh();
        }
    }
    public IReadOnlyList<PlacedMeasurement> SelectedMeasurements => selectedPoint is null ? [] :
        project!.Measurements.Where(m => m.PointId == selectedPoint.Id).Reverse().OrderByDescending(m => m.CompletedAt).ToArray();
    public string SelectedMeasurementSummary => SelectedMeasurements.Count == 0 ? "Nenhuma coleta vinculada a este ponto." :
        $"{SelectedMeasurements.Count} coleta(s) vinculada(s). Posições anteriores ficam preservadas.";
    public string LatestRssi => SelectedMeasurements.FirstOrDefault() is { RssiCount: > 0, RssiMeanDbm: { } mean } ?
        $"{mean:0.0} dBm" : "—";
    public string LatestRssiHint => SelectedMeasurements.FirstOrDefault() is { } m ?
        $"Média de {m.RssiCount} leituras · {(m.Cancelled ? "coleta parcial" : "coleta concluída")}" +
        (m.RouterPosition != Router || m.PointPosition != SelectedPoint?.Position ? "\nAs marcações atuais diferem das usadas nesta coleta." : "") : "Média da última coleta neste ponto";

    public void Place(PlanPosition position)
    {
        if (!CanEdit || project is null || !double.IsFinite(position.X) || !double.IsFinite(position.Y) ||
            position.X is < 0 or > 1 || position.Y is < 0 or > 1) return;
        if (PlacementMode == FloorPlanPlacementMode.Router)
            project = project with { Router = position };
        else if (PlacementMode == FloorPlanPlacementMode.Point)
        {
            if (movingPoint && selectedPoint is not null && SelectedMeasurements.Count == 0)
            {
                selectedPoint = selectedPoint with { Position = position };
                project = project with { Points = Points.Select(p => p.Id == selectedPoint.Id ? selectedPoint : p).ToArray() };
            }
            else
            {
                if (Points.Count >= FloorPlanValidation.MaximumPoints) return;
                selectedPoint = new(Guid.NewGuid(), $"Ponto {Points.Count + 1}", position);
                project = project with { Points = [.. Points, selectedPoint] };
            }
        }
        else return;
        movingPoint = false;
        SetMode(FloorPlanPlacementMode.Select);
        MarkDirty(); Refresh();
        Status = Router is null ? "Marque o roteador para continuar." : SelectedPoint is null ? "Agora marque um ponto de medição." : "Posições prontas. Confira o nome do ponto antes de medir.";
    }

    public void SelectPoint(Guid id) => SelectedPoint = Points.FirstOrDefault(p => p.Id == id);
    public void CancelPlacement() { if (CanEdit) SetMode(FloorPlanPlacementMode.Select); }

    public async Task ImportAsync()
    {
        if (!CanEdit || !await PrepareReplacementAsync()) return;
        var path = dialogs.ChooseImage();
        if (path is null) return;
        await RunAsync(async () =>
        {
            var image = await images.ReadAsync(path, CancellationToken.None);
            images.Validate(image);
            var candidate = new FloorPlanProject(1, Guid.NewGuid(), Path.GetFileNameWithoutExtension(image.FileName),
                image.FileName, image.Bytes, image.PixelWidth, image.PixelHeight, null, [], []);
            FloorPlanValidation.Validate(candidate);
            projectPath = null; savedProject = null;
            ReplaceProject(candidate, true);
            SetMode(FloorPlanPlacementMode.Router);
            Status = "Planta importada. Marque o roteador na imagem.";
        });
    }

    public async Task OpenAsync()
    {
        if (!CanEdit || !await PrepareReplacementAsync()) return;
        var path = dialogs.ChooseProject();
        if (path is null) return;
        await RunAsync(async () =>
        {
            var candidate = await store.ReadAsync(path, CancellationToken.None);
            FloorPlanValidation.Validate(candidate);
            images.Validate(new(candidate.ImageFileName, candidate.ImageBytes, candidate.ImagePixelWidth, candidate.ImagePixelHeight));
            projectPath = Path.GetFullPath(path); savedProject = candidate;
            ReplaceProject(candidate, false);
            Status = "Projeto aberto. A foto e as marcações foram restauradas; nenhuma coleta foi iniciada.";
        });
    }

    public async Task<bool> SaveAsync(bool choosePath = false)
    {
        if (!CanEdit || project is null) return false;
        var saved = false;
        await RunAsync(async () => saved = await SaveCoreAsync(choosePath));
        return saved;
    }

    public async Task MeasureAsync()
    {
        if (!CanMeasure || project?.Router is not { } router || selectedPoint is not { } point) return;
        await RunAsync(async () =>
        {
            // A cópia é anterior a qualquer espera por disco, varredura ou aquisição.
            var captured = new PlanPlacement(project.Id, point.Id, point.Name, point.Position, router);
            if (!await SaveCoreAsync(false)) return;
            placement = captured;
            await Probe.StartAsync();
            if (!Probe.HasPendingSave)
            {
                placement = null;
                if (IsDirty) await SaveCoreAsync(false);
            }
        });
    }

    public async Task RetrySessionSaveAsync()
    {
        if (busy || !Probe.RetrySaveCommand.CanExecute(null)) return;
        await RunAsync(async () =>
        {
            await Probe.RetrySaveAsync();
            if (!Probe.HasPendingSave && IsDirty) await SaveCoreAsync(false);
        });
    }

    public async Task<bool> CanCloseAsync()
    {
        if (busy || Probe.IsBusy || Probe.HasPendingSave)
        {
            Status = "Finalize a operação ou use Parar e salvar parcial. Resolva o salvamento pendente antes de fechar a planta.";
            return false;
        }
        if (!dirty) return true;
        switch (dialogs.ConfirmUnsavedChanges())
        {
            case UnsavedProjectChoice.Save: return await SaveAsync();
            case UnsavedProjectChoice.Discard:
                ReplaceProject(savedProject, false);
                return true;
            default: return false;
        }
    }

    private async Task<bool> PrepareReplacementAsync()
    {
        if (!dirty) return true;
        return dialogs.ConfirmUnsavedChanges() switch
        {
            UnsavedProjectChoice.Save => await SaveAsync(),
            UnsavedProjectChoice.Discard => true,
            _ => false
        };
    }

    private async Task<bool> SaveCoreAsync(bool choosePath)
    {
        if (project is null) return false;
        FloorPlanValidation.Validate(project);
        var path = choosePath || projectPath is null ? dialogs.ChooseSavePath(projectPath) : projectPath;
        if (path is null) return false;
        await store.SaveAsync(project, path, CancellationToken.None);
        projectPath = Path.GetFullPath(path); savedProject = project;
        dirty = false;
        Status = "Projeto salvo com a foto, as marcações e os vínculos das coletas.";
        Refresh();
        return true;
    }

    private void OnSessionSaved(ProbeSession session, string path)
    {
        if (placement is not { } captured || project?.Id != captured.ProjectId) return;
        try
        {
            if (project.Measurements.Any(m => m.SessionId == session.SessionId)) return;
            _ = SessionAnalyzer.Analyze(session);
            var history = new SignalHistoryViewModel();
            history.Load(session);
            var values = history.Points.Where(p => p.RssiDbm.HasValue).Select(p => (double)p.RssiDbm!.Value).ToArray();
            var measurement = new PlacedMeasurement(session.SessionId, captured.PointId, captured.PointName,
                captured.PointPosition, captured.RouterPosition, Path.GetFullPath(path), session.CompletedAt.Utc, session.Cancelled,
                values.Length, values.Length == 0 ? null : values.Min(), values.Length == 0 ? null : values.Average(), values.Length == 0 ? null : values.Max());
            project = project with { Measurements = [.. project.Measurements, measurement] };
            placement = null;
            MarkDirty(); Refresh();
            Status = "Coleta salva e vinculada ao ponto. Salvando o projeto…";
        }
        catch (Exception ex)
        {
            // O JSON já está salvo. Uma falha de anotação nunca transforma isso em falha da aquisição.
            ShowError(ex);
        }
    }

    private void OnProbeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(observedSignal, Probe.Signal))
        {
            observedSignal.PropertyChanged -= OnSignalChanged;
            observedSignal = Probe.Signal;
            observedSignal.PropertyChanged += OnSignalChanged;
        }
        if (!Probe.HasPendingSave && Probe.Result is null && !Probe.IsBusy) placement = null;
        Refresh();
    }

    private void OnSignalChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SignalHistoryViewModel.CurrentRssiDbm) or nameof(SignalHistoryViewModel.IsCollecting))
        {
            Notify(nameof(CollectionPointId)); Notify(nameof(LiveRssiDbm)); Notify(nameof(LegendCaption));
        }
    }

    private void ReplaceProject(FloorPlanProject? value, bool changed)
    {
        project = value; selectedPoint = value?.Points.FirstOrDefault(); dirty = changed;
        placement = null; movingPoint = false;
        SetMode(FloorPlanPlacementMode.Select);
        Notify(nameof(ImageBytes)); Notify(nameof(ProjectName));
        Refresh();
    }

    private void SetMode(FloorPlanPlacementMode value)
    {
        placementMode = value;
        Notify(nameof(PlacementMode)); Notify(nameof(PlacementHint));
    }

    private void MarkDirty() { dirty = true; Notify(nameof(IsDirty)); Notify(nameof(SaveState)); }

    private async Task RunAsync(Func<Task> action)
    {
        busy = true; Error = ""; Refresh();
        try { await action(); }
        catch (Exception ex) { ShowError(ex); }
        finally { busy = false; Refresh(); }
    }

    private void ShowError(Exception ex)
    {
        Error = ex switch
        {
            InvalidDataException or ArgumentException => ex.Message,
            UnauthorizedAccessException => "Acesso negado. Escolha um arquivo ou pasta local com permissão.",
            IOException => "Não foi possível ler ou salvar o projeto. Os dados em memória foram preservados; tente Salvar como… em outra pasta.",
            _ => "Não foi possível concluir a operação da planta. Os dados anteriores foram preservados."
        };
    }

    private void Refresh()
    {
        foreach (var property in new[] { nameof(HasProject), nameof(IsDirty), nameof(RequiresCloseReview), nameof(CanEdit), nameof(CanEditProject), nameof(CanMovePoint), nameof(CanMeasure),
            nameof(SaveState), nameof(ProjectFileLabel), nameof(ImageDescription), nameof(Router), nameof(RouterStatus), nameof(Points), nameof(PointFeedback),
            nameof(SelectedPoint), nameof(SelectedPointId), nameof(SelectedPointName), nameof(HasSelectedPoint), nameof(SelectedMeasurements),
            nameof(SelectedMeasurementSummary), nameof(HasSelectedMeasurements), nameof(MeasurementHint), nameof(LatestRssi), nameof(LatestRssiHint),
            nameof(SelectedPointFeedback), nameof(VisualSummary), nameof(CollectionPointId), nameof(LiveRssiDbm), nameof(LegendCaption) }) Notify(property);
        foreach (var command in new[] { ImportCommand, OpenCommand, SaveCommand, SaveAsCommand, MarkRouterCommand,
            AddPointCommand, MovePointCommand, MeasureCommand, RetrySessionSaveCommand }) command?.NotifyCanExecuteChanged();
    }

    private sealed record PlanPlacement(Guid ProjectId, Guid PointId, string PointName, PlanPosition PointPosition, PlanPosition RouterPosition);
}
