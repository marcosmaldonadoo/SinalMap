using SinalMap.Domain;
using SinalMap.Presentation;

namespace SinalMap.Tests;

public sealed class FloorPlanSignalFeedbackTests
{
    [Theory]
    [InlineData(-30, SignalVisualBand.Strong)]
    [InlineData(-50, SignalVisualBand.Strong)]
    [InlineData(-50.01, SignalVisualBand.Good)]
    [InlineData(-60, SignalVisualBand.Good)]
    [InlineData(-60.01, SignalVisualBand.Moderate)]
    [InlineData(-70, SignalVisualBand.Moderate)]
    [InlineData(-70.01, SignalVisualBand.Weak)]
    [InlineData(-80, SignalVisualBand.Weak)]
    [InlineData(-80.01, SignalVisualBand.VeryWeak)]
    [InlineData(-101, SignalVisualBand.VeryWeak)]
    public void Visual_bands_respect_fractional_boundaries_without_rounding_the_input(double value, SignalVisualBand expected) =>
        Assert.Equal(expected, FloorPlanSignalFeedback.BandFor(value, 60));

    [Theory]
    [InlineData(null, 60)]
    [InlineData(-60d, 0)]
    [InlineData(-60d, -1)]
    [InlineData(double.NaN, 60)]
    [InlineData(double.PositiveInfinity, 60)]
    [InlineData(double.NegativeInfinity, 60)]
    public void Missing_or_invalid_values_never_receive_a_measured_signal_band(double? value, int count) =>
        Assert.Equal(SignalVisualBand.Unavailable, FloorPlanSignalFeedback.BandFor(value, count));

    [Fact]
    public void No_project_has_no_feedback_and_unmeasured_points_keep_their_identity_and_number()
    {
        Assert.Empty(FloorPlanSignalFeedback.Create(null));
        var project = Project();
        var second = new FloorPlanPoint(Guid.NewGuid(), "Estoque fictício", new(.8, .5));
        project = project with { Points = [project.Points[0], second] };
        var feedback = FloorPlanSignalFeedback.Create(project);
        Assert.Equal(new[] { 1, 2 }, feedback.Select(p => p.Number));
        Assert.Equal(second.Id, feedback[1].PointId);
        Assert.Equal(second.Position, feedback[1].Position);
        Assert.Equal(second.Name, feedback[1].Name);
        Assert.All(feedback, point =>
        {
            Assert.Equal(SignalVisualBand.Unmeasured, point.Band);
            Assert.Equal("Não medido", point.ValueLabel);
            Assert.False(point.HasMeasurement);
            Assert.False(point.IsPartial);
            Assert.False(point.HasPlacementMismatch);
        });
    }

    [Fact]
    public void Saved_mean_is_formatted_in_Portuguese_with_its_sample_count_and_partial_status()
    {
        var project = Project();
        var measurement = Measurement(project, -63.26) with { Cancelled = true, RssiCount = 17 };
        var point = Assert.Single(FloorPlanSignalFeedback.Create(project with { Measurements = [measurement] }));
        Assert.Equal(SignalVisualBand.Moderate, point.Band);
        Assert.Equal("−63,3 dBm", point.ValueLabel);
        Assert.Contains("17 leituras válidas", point.Detail);
        Assert.Contains("coleta parcial", point.Detail);
        Assert.True(point.HasMeasurement);
        Assert.True(point.IsPartial);
        Assert.DoesNotContain(measurement.SessionPath, point.Detail);
    }

    [Fact]
    public void The_latest_completed_collection_wins_even_when_links_arrive_out_of_order()
    {
        var project = Project();
        var older = Measurement(project, -49) with { CompletedAt = At(1) };
        var latest = Measurement(project, -83) with { CompletedAt = At(3) };
        var middle = Measurement(project, -65) with { CompletedAt = At(2) };
        var point = Assert.Single(FloorPlanSignalFeedback.Create(project with { Measurements = [latest, older, middle] }));
        Assert.Equal(SignalVisualBand.VeryWeak, point.Band);
        Assert.Equal("−83,0 dBm", point.ValueLabel);
    }

    [Fact]
    public void Tied_completion_times_use_the_last_saved_link()
    {
        var project = Project();
        var first = Measurement(project, -49);
        var last = Measurement(project, -78);
        var point = Assert.Single(FloorPlanSignalFeedback.Create(project with { Measurements = [first, last] }));
        Assert.Equal("−78,0 dBm", point.ValueLabel);
    }

    [Fact]
    public void An_unavailable_latest_result_does_not_reuse_a_previous_valid_RSSI()
    {
        var project = Project();
        var old = Measurement(project, -45) with { CompletedAt = At(1) };
        var latest = Measurement(project, null) with { CompletedAt = At(2), RssiCount = 0 };
        var point = Assert.Single(FloorPlanSignalFeedback.Create(project with { Measurements = [old, latest] }));
        Assert.True(point.HasMeasurement);
        Assert.Equal(SignalVisualBand.Unavailable, point.Band);
        Assert.Equal("Sem RSSI", point.ValueLabel);
        Assert.Contains("Nenhuma leitura válida", point.Detail);
        Assert.DoesNotContain("Sem sinal", point.Detail);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Changed_router_or_point_markings_show_the_old_value_without_a_current_signal_band(bool moveRouter)
    {
        var project = Project();
        var original = Measurement(project, -55);
        project = project with { Measurements = [original] };
        project = moveRouter ? project with { Router = new(.9, .9) } :
            project with { Points = [project.Points[0] with { Position = new(.9, .9) }] };
        var point = Assert.Single(FloorPlanSignalFeedback.Create(project));
        Assert.True(point.HasPlacementMismatch);
        Assert.True(point.HasMeasurement);
        Assert.Equal(SignalVisualBand.Unavailable, point.Band);
        Assert.Equal("Anterior: −55,0 dBm", point.ValueLabel);
        Assert.Contains("Marcação alterada após a coleta", point.Detail);
        Assert.Equal(original, Assert.Single(project.Measurements));
    }

    private static DateTimeOffset At(int minute) => new(2026, 1, 1, 0, minute, 0, TimeSpan.Zero);
    private static FloorPlanProject Project() => new(1, Guid.NewGuid(), "Projeto fictício", "planta.png", [], 100, 100,
        new(.2, .3), [new(Guid.NewGuid(), "Caixa fictício", new(.6, .7))], []);
    private static PlacedMeasurement Measurement(FloorPlanProject project, double? mean) => new(Guid.NewGuid(),
        project.Points[0].Id, project.Points[0].Name, project.Points[0].Position, project.Router!,
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "coleta-ficticia.json")), At(1), false,
        mean.HasValue ? 60 : 0, mean, mean, mean);
}
