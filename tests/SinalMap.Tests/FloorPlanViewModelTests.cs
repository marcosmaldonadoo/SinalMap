using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Presentation;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class FloorPlanViewModelTests
{
    [Fact]
    public async Task Point_feedback_updates_after_collection_and_is_restored_when_reopening_the_project()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var initial = h.Model.PointFeedback;
        Assert.Equal(SignalVisualBand.Unmeasured, Assert.Single(initial).Band);
        await h.Model.MeasureAsync();
        var measured = h.Model.PointFeedback;
        var feedback = Assert.Single(measured);
        Assert.NotSame(initial, measured);
        Assert.Equal(SignalVisualBand.Good, feedback.Band);
        Assert.Equal("−55,0 dBm", feedback.ValueLabel);
        Assert.True(feedback.HasMeasurement);
        Assert.Equal(feedback, h.Model.SelectedPointFeedback);
        Assert.Contains("1 de 1 com RSSI", h.Model.VisualSummary);
        h.Dialogs.ProjectPath = h.Dialogs.SavePath;
        await h.Model.OpenAsync();
        Assert.Equal(feedback, Assert.Single(h.Model.PointFeedback));
        Assert.Equal(feedback, h.Model.SelectedPointFeedback);
        Assert.False(h.Model.IsDirty);
        Assert.Single(h.Sessions.Saved);
    }

    [Fact]
    public async Task Feedback_projection_keeps_its_reference_during_non_observational_refreshes()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        await h.Model.AddPointCommand.ExecuteAsync();
        h.Model.Place(new(.4, .5));
        Assert.True(await h.Model.SaveAsync());
        var feedback = h.Model.PointFeedback;
        h.Probe.TargetText = "192.0.2.2";
        h.Model.SelectedPointId = h.Model.Points[0].Id;
        h.Model.CancelPlacement();
        Assert.Same(feedback, h.Model.PointFeedback);
        Assert.False(h.Model.IsDirty);
        Assert.Empty(h.Icmp.Invocations);
    }

    [Fact]
    public async Task Toggling_the_visual_layer_does_not_change_project_dirty_state_or_measurements()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h);
        Assert.True(h.Model.IsDirty);
        h.Model.ShowPointResults = false;
        Assert.True(h.Model.IsDirty);
        Assert.True(await h.Model.SaveAsync());
        var feedback = h.Model.PointFeedback;
        h.Model.ShowPointResults = true;
        h.Model.ShowPointResults = false;
        Assert.False(h.Model.IsDirty);
        Assert.Same(feedback, h.Model.PointFeedback);
        Assert.Empty(h.Model.SelectedMeasurements);
        Assert.Empty(h.Sessions.Saved);
    }

    [Fact]
    public async Task Selecting_a_point_by_id_survives_temporary_nulls_from_rebinding()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var first = h.Model.Points[0];
        await h.Model.AddPointCommand.ExecuteAsync();
        h.Model.Place(new(.5, .4));
        h.Model.SelectedPointId = first.Id;
        h.Model.SelectedPoint = null;
        h.Model.SelectedPointId = null;
        Assert.Equal(first.Id, h.Model.SelectedPointId);
        Assert.Equal(first, h.Model.SelectedPoint);
        Assert.Equal(first.Id, h.Model.SelectedPointFeedback!.PointId);
        Assert.True(h.Model.CanMeasure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Live_RSSI_belongs_only_to_the_point_being_collected_and_ends_with_saved_feedback(bool cancel)
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var first = h.Model.Points[0];
        await h.Model.AddPointCommand.ExecuteAsync();
        h.Model.Place(new(.4, .5));
        var other = h.Model.SelectedPoint!;
        h.Model.SelectedPointId = first.Id;
        var observed = Signal();
        var release = Signal();
        h.Wifi.OnRead = async (sequence, id, token) =>
        {
            if (sequence > 0) await release.Task.WaitAsync(token);
            return TestData.Wifi(new ManualClock(), id) with { RssiSource = "WlanQueryInterface:rssi", RssiDbm = -62 };
        };
        h.Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FloorPlanViewModel.LiveRssiDbm) && h.Model.LiveRssiDbm == -62)
                observed.TrySetResult();
        };
        Assert.Null(h.Model.CollectionPointId);
        Assert.Null(h.Model.LiveRssiDbm);
        var collection = h.Model.MeasureAsync();
        try
        {
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(first.Id, h.Model.CollectionPointId);
            Assert.Equal(-62, h.Model.LiveRssiDbm);
            h.Model.SelectedPointId = other.Id;
            Assert.Equal(first.Id, h.Model.SelectedPointId);
            Assert.Equal(SignalVisualBand.Unmeasured, h.Model.PointFeedback.Single(p => p.PointId == other.Id).Band);
            Assert.Empty(h.Sessions.Saved);
            if (cancel) h.Probe.Cancel();
        }
        finally
        {
            release.TrySetResult();
            await collection.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Null(h.Model.CollectionPointId);
        Assert.Null(h.Model.LiveRssiDbm);
        Assert.Equal(first.Id, h.Model.SelectedPointFeedback!.PointId);
        Assert.Equal(SignalVisualBand.Moderate, h.Model.SelectedPointFeedback.Band);
        Assert.Equal("−62,0 dBm", h.Model.SelectedPointFeedback.ValueLabel);
        Assert.Equal(cancel, h.Model.SelectedPointFeedback.IsPartial);
        Assert.Single(h.Sessions.Saved);
        Assert.False(h.Model.IsDirty);
    }

    [Fact]
    public async Task Sidebar_and_map_choose_the_same_last_link_when_completion_timestamps_are_equal()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h);
        Assert.True(await h.Model.SaveAsync());
        var project = h.Projects.Saved[^1];
        var point = project.Points[0];
        var first = new PlacedMeasurement(Guid.NewGuid(), point.Id, point.Name, point.Position, project.Router!,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "coleta-anterior-ficticia.json")), DateTimeOffset.UtcNow,
            false, 60, -45, -45, -45);
        var last = first with { SessionId = Guid.NewGuid(), RssiMinimumDbm = -77, RssiMeanDbm = -77, RssiMaximumDbm = -77 };
        await h.Projects.SaveAsync(project with { Measurements = [first, last] }, h.Dialogs.SavePath!, default);
        h.Dialogs.ProjectPath = h.Dialogs.SavePath;
        await h.Model.OpenAsync();
        Assert.Equal(last, h.Model.SelectedMeasurements[0]);
        Assert.Contains("-77", h.Model.LatestRssi);
        Assert.Equal("−77,0 dBm", h.Model.SelectedPointFeedback!.ValueLabel);
        Assert.Equal(SignalVisualBand.Weak, h.Model.SelectedPointFeedback.Band);
        Assert.Empty(h.Sessions.Saved);
        Assert.Empty(h.Icmp.Invocations);
    }

    [Fact]
    public async Task Moving_the_router_preserves_the_saved_placement_and_identifies_the_difference()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        await h.Model.MeasureAsync();
        var original = Assert.Single(h.Model.SelectedMeasurements);
        await h.Model.MarkRouterCommand.ExecuteAsync();
        h.Model.Place(new(.9, .9));
        Assert.NotEqual(original.RouterPosition, h.Model.Router);
        Assert.Equal(original, Assert.Single(h.Model.SelectedMeasurements));
        Assert.Contains("marcações atuais diferem", h.Model.LatestRssiHint);
        Assert.True(h.Model.IsDirty);
    }

    [Fact]
    public async Task Renaming_a_point_preserves_its_selection_when_the_list_publishes_a_temporary_null()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var id = h.Model.SelectedPointId;
        h.Model.SelectedPointName = "Caixa renomeado";
        h.Model.SelectedPoint = null;
        Assert.Equal(id, h.Model.SelectedPointId);
        Assert.Equal("Caixa renomeado", h.Model.SelectedPointName);
        Assert.Equal(h.Model.SelectedPoint, Assert.Single(h.Model.Points));
        Assert.True(h.Model.CanMeasure);
    }

    [Fact]
    public async Task Import_router_and_points_form_a_manual_placement_without_collecting()
    {
        var h = new Harness();
        await h.Model.ImportAsync();
        Assert.True(h.Model.HasProject);
        Assert.True(h.Model.IsDirty);
        Assert.Null(h.Model.Router);
        Assert.Equal(FloorPlanPlacementMode.Router, h.Model.PlacementMode);
        h.Model.Place(new(.2, .3));
        Assert.Equal(new PlanPosition(.2, .3), h.Model.Router);
        await h.Model.AddPointCommand.ExecuteAsync();
        h.Model.Place(new(.7, .8));
        Assert.Equal(new PlanPosition(.7, .8), Assert.Single(h.Model.Points).Position);
        h.Model.SelectedPointName = "Caixa de teste";
        Assert.Equal("Caixa de teste", h.Model.SelectedPoint!.Name);
        Assert.False(h.Model.CanMeasure);
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Sessions.Saved);
        Assert.Equal(0, h.Wifi.Calls);
        var point = h.Model.SelectedPoint;
        await h.Model.MovePointCommand.ExecuteAsync();
        h.Model.Place(new(2, .5));
        Assert.Equal(point, h.Model.SelectedPoint);
        h.Model.CancelPlacement();
        Assert.Equal(FloorPlanPlacementMode.Select, h.Model.PlacementMode);
    }

    [Fact]
    public async Task Cancelling_dialogs_preserves_the_current_project_and_does_not_start_acquisition()
    {
        var h = new Harness();
        h.Dialogs.ImagePath = null;
        await h.Model.ImportAsync();
        Assert.False(h.Model.HasProject);
        Assert.Equal(0, h.Images.ReadCalls);
        h.Dialogs.ImagePath = "planta.png";
        await h.Model.ImportAsync();
        h.Model.ProjectName = "Projeto preservado";
        h.Dialogs.UnsavedChoice = UnsavedProjectChoice.Cancel;
        await h.Model.ImportAsync();
        await h.Model.OpenAsync();
        Assert.Equal(1, h.Images.ReadCalls);
        Assert.Equal("Projeto preservado", h.Model.ProjectName);
        h.Dialogs.SavePath = null;
        Assert.False(await h.Model.SaveAsync());
        Assert.True(h.Model.IsDirty);
        Assert.Empty(h.Projects.Saved);
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Sessions.Saved);
    }

    [Fact]
    public async Task Failed_image_import_preserves_the_previous_image_and_markers()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h);
        var bytes = h.Model.ImageBytes;
        var router = h.Model.Router;
        var point = h.Model.SelectedPoint;
        h.Dialogs.UnsavedChoice = UnsavedProjectChoice.Discard;
        h.Images.Fail = true;
        await h.Model.ImportAsync();
        Assert.True(h.Model.HasError);
        Assert.Same(bytes, h.Model.ImageBytes);
        Assert.Equal(router, h.Model.Router);
        Assert.Equal(point, h.Model.SelectedPoint);
        Assert.True(h.Model.IsDirty);
        Assert.Empty(h.Sessions.Saved);
    }

    [Fact]
    public async Task Save_and_reopen_restore_the_plan_without_loading_or_creating_a_measurement()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h);
        h.Model.ProjectName = "Loja de teste salva";
        var router = h.Model.Router;
        var point = h.Model.SelectedPoint;
        Assert.True(await h.Model.SaveAsync());
        Assert.False(h.Model.IsDirty);
        h.Model.ProjectName = "Alteração em memória";
        h.Dialogs.UnsavedChoice = UnsavedProjectChoice.Discard;
        h.Dialogs.ProjectPath = h.Dialogs.SavePath;
        await h.Model.OpenAsync();
        Assert.Equal("Loja de teste salva", h.Model.ProjectName);
        Assert.Equal(router, h.Model.Router);
        Assert.Equal(point, h.Model.SelectedPoint);
        Assert.False(h.Model.IsDirty);
        Assert.Empty(h.Model.SelectedMeasurements);
        Assert.Equal(0, h.Wifi.Calls);
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Sessions.Saved);
    }

    [Fact]
    public async Task Measurement_saves_the_original_JSON_then_links_and_saves_the_project()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var point = h.Model.SelectedPoint!;
        var router = h.Model.Router!;
        await h.Model.MeasureAsync();
        var saved = Assert.Single(h.Sessions.Saved);
        var measurement = Assert.Single(h.Model.SelectedMeasurements);
        Assert.Equal(saved.Session.SessionId, measurement.SessionId);
        Assert.Equal(saved.Path, measurement.SessionPath);
        Assert.Equal(point.Id, measurement.PointId);
        Assert.Equal(point.Name, measurement.PointName);
        Assert.Equal(point.Position, measurement.PointPosition);
        Assert.Equal(router, measurement.RouterPosition);
        Assert.Equal(60, measurement.RssiCount);
        Assert.Equal(-55, measurement.RssiMinimumDbm);
        Assert.Equal(-55, measurement.RssiMeanDbm);
        Assert.Equal(-55, measurement.RssiMaximumDbm);
        Assert.False(measurement.Cancelled);
        Assert.Equal(2, h.Projects.SaveCalls);
        Assert.Empty(h.Projects.Saved[0].Measurements);
        Assert.Equal(measurement, Assert.Single(h.Projects.Saved[1].Measurements));
        Assert.False(h.Model.IsDirty);
        Assert.False(h.Model.HasError);
        Assert.False(h.Probe.HasPendingSave);
        Assert.Equal(60, h.Icmp.Invocations.Count);
        Assert.False(h.Model.CanMovePoint);
    }

    [Fact]
    public async Task Cancelling_a_collection_links_the_saved_partial_session_to_the_original_point()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var entered = Signal();
        h.Wifi.OnRead = async (_, _, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return null!;
        };
        var point = h.Model.SelectedPoint!;
        var collection = h.Model.MeasureAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        h.Probe.Cancel();
        await collection.WaitAsync(TimeSpan.FromSeconds(5));
        var saved = Assert.Single(h.Sessions.Saved);
        var measurement = Assert.Single(h.Model.SelectedMeasurements);
        Assert.True(saved.Session.Cancelled);
        Assert.True(measurement.Cancelled);
        Assert.Equal(point.Position, measurement.PointPosition);
        Assert.Equal(saved.Path, measurement.SessionPath);
        Assert.Equal(0, measurement.RssiCount);
        Assert.Null(measurement.RssiMeanDbm);
        Assert.False(h.Probe.HasPendingSave);
        Assert.False(h.Model.IsDirty);
    }

    [Fact]
    public async Task Project_save_failure_preserves_the_successful_JSON_and_retry_does_not_duplicate_it()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        h.Projects.FailAtSaveCall = 2;
        await h.Model.MeasureAsync();
        var saved = Assert.Single(h.Sessions.Saved);
        var measurement = Assert.Single(h.Model.SelectedMeasurements);
        Assert.Equal(saved.Path, measurement.SessionPath);
        Assert.True(h.Model.HasError);
        Assert.True(h.Model.IsDirty);
        Assert.False(h.Probe.HasPendingSave);
        Assert.Empty(Assert.Single(h.Projects.Saved).Measurements);
        h.Projects.FailAtSaveCall = null;
        Assert.True(await h.Model.SaveAsync());
        Assert.Single(h.Sessions.Saved);
        Assert.Equal(1, h.Sessions.SaveCalls);
        Assert.Equal(measurement, Assert.Single(h.Projects.Saved[^1].Measurements));
        Assert.False(h.Model.IsDirty);
        Assert.False(h.Model.HasError);
    }

    [Fact]
    public async Task JSON_save_failure_creates_no_link_and_successful_retry_creates_one_link_only()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var point = h.Model.SelectedPoint;
        var router = h.Model.Router;
        h.Sessions.Fail = true;
        await h.Model.MeasureAsync();
        Assert.True(h.Probe.HasPendingSave);
        Assert.Empty(h.Model.SelectedMeasurements);
        Assert.Empty(h.Sessions.Saved);
        Assert.False(h.Model.CanEdit);
        await h.Model.MarkRouterCommand.ExecuteAsync();
        h.Model.Place(new(.9, .9));
        h.Model.SelectedPointName = "Alteração bloqueada";
        Assert.Equal(router, h.Model.Router);
        Assert.Equal(point, h.Model.SelectedPoint);
        h.Sessions.Fail = false;
        await h.Model.RetrySessionSaveAsync();
        Assert.Single(h.Sessions.Saved);
        var measurement = Assert.Single(h.Model.SelectedMeasurements);
        Assert.Equal(point!.Name, measurement.PointName);
        Assert.Equal(point.Position, measurement.PointPosition);
        Assert.Equal(router, measurement.RouterPosition);
        Assert.False(h.Probe.HasPendingSave);
        Assert.False(h.Model.IsDirty);
        var calls = h.Sessions.SaveCalls;
        await h.Model.RetrySessionSaveAsync();
        Assert.Equal(calls, h.Sessions.SaveCalls);
        Assert.Single(h.Model.SelectedMeasurements);
    }

    [Fact]
    public async Task Editing_and_closing_are_blocked_while_collection_save_is_in_progress()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        var point = h.Model.SelectedPoint;
        var router = h.Model.Router;
        h.Sessions.Hold = Signal();
        var measurement = h.Model.MeasureAsync();
        await h.Sessions.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(h.Model.CanEdit);
        Assert.False(h.Model.CanMeasure);
        Assert.False(await h.Model.CanCloseAsync());
        h.Model.Place(new(.1, .9));
        h.Model.SelectedPointName = "Não aplicado";
        h.Model.ProjectName = "Não aplicado";
        h.Model.SelectedPoint = null;
        await h.Model.AddPointCommand.ExecuteAsync();
        Assert.Single(h.Model.Points);
        Assert.Equal(point, h.Model.SelectedPoint);
        Assert.Equal(router, h.Model.Router);
        h.Sessions.Hold.TrySetResult();
        await measurement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(point!.Position, Assert.Single(h.Model.SelectedMeasurements).PointPosition);
        Assert.True(h.Model.CanEdit);
    }

    [Fact]
    public async Task Clean_plan_requires_close_review_while_its_presave_is_pending_before_probe_starts()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        Assert.True(await h.Model.SaveAsync());
        Assert.False(h.Model.IsDirty);
        Assert.False(h.Model.RequiresCloseReview);
        h.Projects.HoldAtSaveCall = 2;
        h.Projects.Hold = Signal();
        var measurement = h.Model.MeasureAsync();
        await h.Projects.Held.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(h.Model.IsDirty);
        Assert.False(h.Probe.IsBusy);
        Assert.True(h.Model.RequiresCloseReview);
        Assert.False(await h.Model.CanCloseAsync());
        Assert.Empty(h.Sessions.Saved);
        h.Projects.Hold.TrySetResult();
        await measurement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(h.Model.SelectedMeasurements);
        Assert.False(h.Model.RequiresCloseReview);
        Assert.True(await h.Model.CanCloseAsync());
    }

    [Fact]
    public async Task Initial_project_save_failure_or_cancelled_save_dialog_prevents_a_collection()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        h.Dialogs.SavePath = null;
        await h.Model.MeasureAsync();
        Assert.Empty(h.Sessions.Saved);
        Assert.Empty(h.Icmp.Invocations);
        Assert.True(h.Model.IsDirty);
        h.Dialogs.SavePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "projeto-ficticio.sinalmap-planta.json"));
        h.Projects.FailAtSaveCall = 1;
        await h.Model.MeasureAsync();
        Assert.True(h.Model.HasError);
        Assert.Empty(h.Sessions.Saved);
        Assert.Empty(h.Icmp.Invocations);
        Assert.False(h.Probe.HasPendingSave);
    }

    [Fact]
    public async Task Cancelling_close_leaves_the_probe_and_plan_usable()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        h.Dialogs.UnsavedChoice = UnsavedProjectChoice.Cancel;
        Assert.False(await h.Model.CanCloseAsync());
        Assert.True(h.Model.CanEdit);
        Assert.True(h.Probe.CanReplaceSession);
        Assert.True(h.Probe.StartCommand.CanExecute(null));
        await h.Model.MeasureAsync();
        Assert.Single(h.Sessions.Saved);
        Assert.Single(h.Model.SelectedMeasurements);
    }

    [Fact]
    public async Task Opening_history_and_collecting_outside_the_plan_do_not_create_placement_links()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        await h.Probe.OpenAsync("coleta-ficticia.json");
        Assert.Empty(h.Model.SelectedMeasurements);
        await h.Probe.StartAsync();
        Assert.Single(h.Sessions.Saved);
        Assert.Empty(h.Model.SelectedMeasurements);
        Assert.True(h.Model.IsDirty);
    }

    [Fact]
    public async Task Unknown_RSSI_sources_are_not_projected_as_measured_values_on_the_plan()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        h.Wifi.OnRead = (_, id, _) => Task.FromResult(TestData.Wifi(new ManualClock(), id) with { RssiSource = "desconhecida" });
        await h.Model.MeasureAsync();
        var measurement = Assert.Single(h.Model.SelectedMeasurements);
        Assert.Equal(0, measurement.RssiCount);
        Assert.Null(measurement.RssiMinimumDbm);
        Assert.Null(measurement.RssiMeanDbm);
        Assert.Null(measurement.RssiMaximumDbm);
        Assert.Equal("—", h.Model.LatestRssi);
        Assert.Single(h.Sessions.Saved);
        Assert.False(h.Model.IsDirty);
    }

    [Fact]
    public async Task A_project_at_the_measurement_limit_cannot_start_another_collection()
    {
        var h = new Harness();
        await ConfigurePlanAsync(h, prepareProbe: true);
        Assert.True(await h.Model.SaveAsync());
        var initial = h.Projects.Saved[^1];
        var point = Assert.Single(initial.Points);
        var template = new PlacedMeasurement(Guid.NewGuid(), point.Id, point.Name, point.Position, initial.Router!,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "coleta-no-limite-ficticia.json")), DateTimeOffset.UtcNow,
            false, 0, null, null, null);
        var full = initial with
        {
            Measurements = Enumerable.Range(0, FloorPlanValidation.MaximumMeasurements)
                .Select(_ => template with { SessionId = Guid.NewGuid() }).ToArray()
        };
        await h.Projects.SaveAsync(full, h.Dialogs.SavePath!, default);
        h.Dialogs.ProjectPath = h.Dialogs.SavePath;
        await h.Model.OpenAsync();
        Assert.Equal(FloorPlanValidation.MaximumMeasurements, h.Model.SelectedMeasurements.Count);
        Assert.False(h.Model.CanMeasure);
        Assert.False(h.Model.MeasureCommand.CanExecute(null));
        await h.Model.MeasureAsync();
        Assert.Empty(h.Sessions.Saved);
        Assert.Empty(h.Icmp.Invocations);
    }

    private static async Task ConfigurePlanAsync(Harness h, bool prepareProbe = false)
    {
        await h.Model.ImportAsync();
        h.Model.Place(new(.2, .3));
        await h.Model.AddPointCommand.ExecuteAsync();
        h.Model.Place(new(.7, .8));
        if (prepareProbe) await h.Probe.RefreshAsync();
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Harness
    {
        public FakeWifi Wifi { get; }
        public FakeIcmp Icmp { get; }
        public SessionStore Sessions { get; } = new();
        public ProjectStore Projects { get; } = new();
        public Dialogs Dialogs { get; } = new();
        public Images Images { get; } = new();
        public ProbeViewModel Probe { get; }
        public FloorPlanViewModel Model { get; }
        public Harness()
        {
            var clock = new ManualClock();
            Wifi = new(clock);
            Wifi.OnRead = (_, id, _) => Task.FromResult(TestData.Wifi(clock, id) with { RssiSource = "WlanQueryInterface:rssi" });
            Icmp = new(clock);
            Probe = new(Wifi, Icmp, clock, Sessions, (_, _) => TestData.Session(), new ProbeDialogs(),
                Path.Combine(Path.GetTempPath(), "sinalmap-plan-simulated"));
            Probe.TargetText = "192.0.2.1";
            Model = new(Probe, Projects, Dialogs, Images);
        }
    }

    private sealed class Dialogs : IFloorPlanDialogs
    {
        public string? ImagePath { get; set; } = "planta.png";
        public string? ProjectPath { get; set; }
        public string? SavePath { get; set; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "loja-ficticia.sinalmap-planta.json"));
        public UnsavedProjectChoice UnsavedChoice { get; set; } = UnsavedProjectChoice.Cancel;
        public string? ChooseImage() => ImagePath;
        public string? ChooseProject() => ProjectPath;
        public string? ChooseSavePath(string? currentPath) => SavePath;
        public UnsavedProjectChoice ConfirmUnsavedChanges() => UnsavedChoice;
    }

    private sealed class Images : IFloorPlanImageLoader
    {
        public int ReadCalls { get; private set; }
        public bool Fail { get; set; }
        public Task<FloorPlanImage> ReadAsync(string path, CancellationToken cancellationToken)
        {
            ReadCalls++;
            if (Fail) throw new IOException("Falha simulada na importação.");
            return Task.FromResult(new FloorPlanImage("planta.png", Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j0ioAAAAASUVORK5CYII="), 1, 1));
        }
        public void Validate(FloorPlanImage image) { }
    }

    private sealed class ProjectStore : IFloorPlanProjectStore
    {
        private readonly Dictionary<string, FloorPlanProject> files = new(StringComparer.Ordinal);
        public List<FloorPlanProject> Saved { get; } = [];
        public int SaveCalls { get; private set; }
        public int? FailAtSaveCall { get; set; }
        public int? HoldAtSaveCall { get; set; }
        public TaskCompletionSource? Hold { get; set; }
        public TaskCompletionSource Held { get; } = Signal();
        public Task<FloorPlanProject> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult(files[path]);
        public async Task SaveAsync(FloorPlanProject project, string path, CancellationToken cancellationToken)
        {
            FloorPlanValidation.Validate(project);
            if (++SaveCalls == FailAtSaveCall) throw new IOException("Falha simulada no projeto.");
            if (SaveCalls == HoldAtSaveCall && Hold is not null)
            {
                Held.TrySetResult();
                await Hold.Task;
            }
            files[path] = project;
            Saved.Add(project);
        }
    }

    private sealed class SessionStore : ISessionStore
    {
        public List<(ProbeSession Session, string Path)> Saved { get; } = [];
        public int SaveCalls { get; private set; }
        public bool Fail { get; set; }
        public TaskCompletionSource Started { get; } = Signal();
        public TaskCompletionSource? Hold { get; set; }
        public async Task SaveAsync(ProbeSession session, string path, CancellationToken cancellationToken)
        {
            SaveCalls++;
            Started.TrySetResult();
            if (Hold is not null) await Hold.Task;
            if (Fail) throw new IOException("Falha simulada no JSON da coleta.");
            Saved.Add((session, path));
        }
    }

    private sealed class ProbeDialogs : IProbeDialogs
    {
        public string? ChooseOutputFolder(string currentFolder) => null;
        public string? ChooseSessionFile() => null;
        public bool ConfirmDiscard() => false;
    }
}
