using System.Text.Json;
using System.Threading.Channels;
using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Presentation;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class SignalHistoryViewModelTests
{
    [Fact]
    public async Task Live_updates_are_ordered_and_an_older_completion_never_replaces_the_latest_reading()
    {
        var session = await SessionComparerTests.CreateSession(-60);
        var model = new SignalHistoryViewModel();
        model.Begin();
        model.Accept(session.Samples[1] with { Wifi = session.Samples[1].Wifi! with { RssiDbm = -55 } });
        model.Accept(session.Samples[0]);
        Assert.Equal("-55 dBm", model.Value);
        Assert.Equal(new[] { -60, -55 }, model.Points.Select(p => p.RssiDbm!.Value));
        Assert.False(model.Points[1].BreakBefore);
        Assert.Equal("Sinal atual", model.Heading);
        model.End();
        Assert.Equal("Última leitura registrada", model.Heading);
    }

    [Fact]
    public async Task Missing_RSSI_clears_the_current_value_and_interrupts_the_line_without_substituting_zero()
    {
        var session = await SessionComparerTests.CreateSession(-60);
        var model = new SignalHistoryViewModel();
        model.Begin();
        model.Accept(session.Samples[0]);
        model.Accept(session.Samples[1] with { Wifi = session.Samples[1].Wifi! with { RssiDbm = null } });
        Assert.Equal("Indisponível", model.Value);
        Assert.Null(model.Points[1].RssiDbm);
        model.Accept(session.Samples[2]);
        Assert.True(model.Points[2].BreakBefore);
        Assert.Equal("-60 dBm", model.Value);
    }

    [Theory]
    [InlineData("bssid")]
    [InlineData("ssid")]
    [InlineData("source")]
    [InlineData("missing-position")]
    public async Task Context_changes_or_missing_positions_prevent_a_continuous_line(string condition)
    {
        var session = await SessionComparerTests.CreateSession();
        var model = new SignalHistoryViewModel();
        model.Accept(session.Samples[0]);
        var next = session.Samples[condition == "missing-position" ? 2 : 1];
        next = next with { Wifi = condition switch
        {
            "bssid" => next.Wifi! with { Bssid = "02:00:00:00:00:02" },
            "ssid" => next.Wifi! with { SsidHex = "6F74686572" },
            "source" => next.Wifi! with { RssiSource = "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" },
            _ => next.Wifi
        }};
        model.Accept(next);
        Assert.True(model.Points[1].BreakBefore);
    }

    [Fact]
    public async Task Unrecognized_sources_are_not_displayed_as_measured_signal_and_free_text_remains_private()
    {
        var session = await SessionComparerTests.CreateSession();
        var sample = session.Samples[0] with
        {
            Wifi = session.Samples[0].Wifi! with { RssiSource = "PRIVATE-SECRET", Ssid = "PRIVATE-SECRET" },
            Issues = [new(IssueKind.NativeError, "PRIVATE-SECRET", "PRIVATE-SECRET", 27)]
        };
        var model = new SignalHistoryViewModel();
        model.Load(session with { Samples = [sample] });
        Assert.Equal("Indisponível", model.Value);
        Assert.Null(Assert.Single(model.Points).RssiDbm);
        Assert.Contains("27", model.LatestIssues);
        var output = JsonSerializer.Serialize(model);
        foreach (var value in new[] { "PRIVATE-SECRET", session.InterfaceId.ToString(), session.TargetAddress, "02:00:00:00:00:01" })
            Assert.DoesNotContain(value, output);
    }

    [Fact]
    public async Task Cancelled_future_positions_preserve_the_last_actual_reading_and_saved_history_can_be_loaded()
    {
        var session = await SessionComparerTests.CreateSession(-60);
        var model = new SignalHistoryViewModel();
        model.Load(session with { Samples = [session.Samples[0], session.Samples[1] with
        {
            Disposition = SlotDisposition.CancelledBeforeDispatch, Wifi = null, Icmp = null
        }], Cancelled = true });
        Assert.Equal("-60 dBm", model.Value);
        Assert.Null(model.Points[1].RssiDbm);
        Assert.Contains("Mínimo -60 dBm", model.Range);
    }

    [Fact]
    public async Task Full_or_closed_live_channel_does_not_block_or_change_the_collection()
    {
        var channel = Channel.CreateBounded<ProbeSample>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false, FullMode = BoundedChannelFullMode.Wait
        });
        var clock = new ManualClock();
        var session = await new ProbeRunner(new FakeWifi(clock), new FakeIcmp(clock), clock)
            .RunAsync(TestData.Options, CancellationToken.None, channel.Writer);
        Assert.Equal(60, session.Samples.Count);
        Assert.Equal(TimeSpan.FromSeconds(60), clock.ElapsedSince(0));
        Assert.True(channel.Reader.TryRead(out var published));
        Assert.Equal(session.Samples[0], published);
        channel.Writer.Complete();
        clock = new ManualClock();
        session = await new ProbeRunner(new FakeWifi(clock), new FakeIcmp(clock), clock)
            .RunAsync(TestData.Options, CancellationToken.None, channel.Writer);
        Assert.Equal(60, session.Samples.Count);
    }

    [Fact]
    public async Task Every_completed_sample_can_be_recovered_from_the_live_channel_without_altering_JSON_models()
    {
        var channel = Channel.CreateUnbounded<ProbeSample>(new() { AllowSynchronousContinuations = false });
        var clock = new ManualClock();
        var session = await new ProbeRunner(new FakeWifi(clock), new FakeIcmp(clock), clock)
            .RunAsync(TestData.Options, CancellationToken.None, channel.Writer);
        channel.Writer.Complete();
        var published = new List<ProbeSample>();
        await foreach (var sample in channel.Reader.ReadAllAsync()) published.Add(sample);
        Assert.Equal(session.Samples, published.OrderBy(s => s.Sequence));
    }
}
