using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class ProbeRunnerTests
{
    [Fact]
    public async Task Plans_sixty_slots_on_absolute_monotonic_seconds_and_finishes_the_window()
    {
        var clock = new ManualClock();
        var icmp = new FakeIcmp(clock);
        var result = await new ProbeRunner(new FakeWifi(clock), icmp, clock).RunAsync(TestData.Options, CancellationToken.None);
        Assert.Equal(60, result.Samples.Count);
        Assert.Equal(Enumerable.Range(0, 60), result.Samples.Select(s => s.Sequence));
        Assert.Equal(Enumerable.Range(0, 60).Select(i => TimeSpan.FromSeconds(i)), result.Samples.Select(s => s.PlannedOffset));
        Assert.Equal(Enumerable.Range(0, 60).Select(i => i * clock.Frequency), icmp.Invocations.Select(s => s.MonotonicTicks));
        Assert.Equal(60 * clock.Frequency, result.CompletedAt.MonotonicTicks - result.StartedAt.MonotonicTicks);
        Assert.All(result.Samples, s => Assert.Equal(0, s.StartLatenessMilliseconds));
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task Does_not_wait_for_previous_async_icmp_operations_to_schedule_other_slots()
    {
        var clock = new ManualClock();
        var completions = new List<TaskCompletionSource<IcmpMeasurement>>();
        var icmp = new FakeIcmp(clock)
        {
            OnSend = (_, _) =>
            {
                var completion = new TaskCompletionSource<IcmpMeasurement>(TaskCreationOptions.RunContinuationsAsynchronously);
                completions.Add(completion);
                return completion.Task;
            }
        };
        var running = new ProbeRunner(new FakeWifi(clock), icmp, clock).RunAsync(TestData.Options, CancellationToken.None);
        Assert.False(running.IsCompleted);
        Assert.Equal(60, icmp.Invocations.Count);
        foreach (var completion in completions) completion.SetResult(TestData.Icmp(clock));
        Assert.Equal(60, (await running).Samples.Count);
    }

    [Fact]
    public async Task Skips_missed_slots_without_catch_up_bursts()
    {
        var clock = new ManualClock();
        var wifi = new FakeWifi(clock)
        {
            OnRead = (i, id, _) =>
            {
                if (i == 0) clock.Advance(TimeSpan.FromMilliseconds(2500));
                return Task.FromResult(TestData.Wifi(clock, id));
            }
        };
        var icmp = new FakeIcmp(clock);
        var session = await new ProbeRunner(wifi, icmp, clock).RunAsync(TestData.Options, CancellationToken.None);
        Assert.Equal(SlotDisposition.SkippedLate, session.Samples[1].Disposition);
        Assert.Equal(SlotDisposition.SkippedLate, session.Samples[2].Disposition);
        Assert.Null(session.Samples[1].Icmp);
        Assert.Equal(58, icmp.Invocations.Count);
        Assert.Equal(3 * clock.Frequency, icmp.Invocations[1].MonotonicTicks);
        Assert.Equal(60 * clock.Frequency, session.CompletedAt.MonotonicTicks);
    }

    [Fact]
    public async Task Wall_clock_changes_do_not_change_the_schedule()
    {
        var clock = new ManualClock();
        var icmp = new FakeIcmp(clock)
        {
            OnSend = (i, _) =>
            {
                if (i == 10) clock.WallClockAdjustment = TimeSpan.FromHours(-1);
                return Task.FromResult(TestData.Icmp(clock));
            }
        };
        var session = await new ProbeRunner(new FakeWifi(clock), icmp, clock).RunAsync(TestData.Options, CancellationToken.None);
        Assert.Equal(60, icmp.Invocations.Count);
        Assert.Equal(11 * clock.Frequency, session.Samples[11].DispatchedAt!.MonotonicTicks);
        Assert.Equal(clock.Origin.AddSeconds(11), session.Samples[11].PlannedAtUtc);
        Assert.Equal(clock.Origin.AddHours(-1).AddSeconds(11), session.Samples[11].DispatchedAt!.Utc);
    }

    [Fact]
    public async Task Cancellation_preserves_completed_samples_and_labels_unsent_slots()
    {
        var clock = new ManualClock();
        using var cancellation = new CancellationTokenSource();
        var icmp = new FakeIcmp(clock)
        {
            OnSend = (i, _) =>
            {
                if (i == 7) cancellation.Cancel();
                return Task.FromResult(TestData.Icmp(clock));
            }
        };
        var session = await new ProbeRunner(new FakeWifi(clock), icmp, clock).RunAsync(TestData.Options, cancellation.Token);
        Assert.True(session.Cancelled);
        Assert.Equal(60, session.Samples.Count);
        Assert.Equal(8, icmp.Invocations.Count);
        Assert.All(session.Samples.Skip(8), s =>
        {
            Assert.Equal(SlotDisposition.CancelledBeforeDispatch, s.Disposition);
            Assert.Null(s.Icmp);
            Assert.Null(s.Wifi);
            Assert.Null(s.DispatchedAt);
        });
    }

    [Fact]
    public async Task Adapter_exceptions_are_evidence_instead_of_zero_latency_or_signal()
    {
        var clock = new ManualClock();
        var wifi = new FakeWifi(clock) { OnRead = (_, _, _) => throw new InvalidOperationException("simulated Wi-Fi failure") };
        var icmp = new FakeIcmp(clock) { OnSend = (_, _) => throw new InvalidOperationException("simulated ICMP failure") };
        var session = await new ProbeRunner(wifi, icmp, clock).RunAsync(TestData.Options, CancellationToken.None);
        Assert.All(session.Samples, s =>
        {
            Assert.Null(s.Wifi!.RssiDbm);
            Assert.Null(s.Icmp!.RoundTripTimeMilliseconds);
            Assert.Null(s.Icmp.SendAttempted); // Um adaptador que lança exceção não confirmou se tentou enviar.
            Assert.Equal(IssueKind.AdapterError, s.Icmp.Issue!.Kind);
        });
    }

    [Fact]
    public void Bssid_changes_distinguish_network_switches_and_observation_gaps()
    {
        var clock = new ManualClock();
        var samples = new[]
        {
            TestData.Sample(0, TestData.Wifi(clock)),
            TestData.Sample(1, TestData.Wifi(clock)),
            TestData.Sample(2, null),
            TestData.Sample(3, TestData.Wifi(clock, bssid: "02:00:00:00:00:02")),
            TestData.Sample(4, TestData.Wifi(clock, bssid: "02:00:00:00:00:03", ssidHex: "6F74686572"))
        };
        var changes = BssidChangeDetector.Detect(samples);
        Assert.Equal(2, changes.Count);
        Assert.True(changes[0].HasObservationGap);
        Assert.Equal(BssidChangeKind.SameSsidBssidChangeObserved, changes[0].Kind);
        Assert.False(changes[1].HasObservationGap);
        Assert.Equal(BssidChangeKind.DifferentSsidBssidChangeObserved, changes[1].Kind);
    }
}
