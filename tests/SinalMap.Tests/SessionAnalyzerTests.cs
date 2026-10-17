using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class SessionAnalyzerTests
{
    [Fact]
    public async Task Statistics_preserve_zero_and_missing_values_without_counting_failures_as_latency()
    {
        var session = await TestData.Session();
        var samples = session.Samples.Take(4).ToArray();
        samples[0] = samples[0] with { Wifi = samples[0].Wifi! with { RssiDbm = -50, RssiSource = "WlanQueryInterface:rssi" } };
        samples[1] = samples[1] with
        {
            Icmp = samples[1].Icmp! with { RoundTripTimeMilliseconds = 8 },
            Wifi = samples[1].Wifi! with { RssiDbm = -80, RssiSource = "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" }
        };
        samples[2] = samples[2] with
        {
            Icmp = samples[2].Icmp! with { Outcome = IcmpOutcome.TimedOut, RoundTripTimeMilliseconds = null },
            Wifi = samples[2].Wifi! with { RssiDbm = null, Channel = null, FrequencyKHz = null }
        };
        samples[3] = samples[3] with { Icmp = null, Wifi = null, Disposition = SlotDisposition.CancelledBeforeDispatch };
        var summary = SessionAnalyzer.Analyze(session with { Samples = samples, Cancelled = true });
        Assert.Equal(new NumericSummary(2, 0, 4, 8), summary.LatencyMilliseconds);
        Assert.Equal(new NumericSummary(2, -80, -65, -50), summary.RssiDbm);
        Assert.Equal(3, summary.IcmpAttempts);
        Assert.Equal(1, summary.PrimaryRssi);
        Assert.Equal(1, summary.AlternativeRssi);
        Assert.Equal(0, summary.OtherRssiSource);
        Assert.Equal(2, summary.Channels);
        Assert.Equal(2, summary.Frequencies);
        Assert.Equal(0, summary.CachedFrequencies); // A fonte da simulação não é apresentada como cache real.
        Assert.True(summary.Cancelled);
        Assert.Equal(1, summary.CancelledBeforeDispatch);
        Assert.Equal(60, summary.DurationSeconds);
        Assert.Contains(summary.IcmpOutcomes, x => x.Outcome == IcmpOutcome.TimedOut && x.Count == 1);
    }

    [Fact]
    public async Task No_observations_produce_null_statistics_and_zero_counts()
    {
        var summary = SessionAnalyzer.Analyze((await TestData.Session()) with { Samples = [] });
        Assert.Equal(new NumericSummary(0, null, null, null), summary.LatencyMilliseconds);
        Assert.Equal(new NumericSummary(0, null, null, null), summary.RssiDbm);
        Assert.Equal(0, summary.Positions);
        Assert.Empty(summary.IcmpOutcomes);
    }

    [Fact]
    public async Task Repeated_issues_in_one_position_are_not_counted_as_multiple_affected_positions()
    {
        var session = await TestData.Session();
        var issue = new ProbeIssue(IssueKind.NativeError, "operation", "free text", 27);
        var first = session.Samples[0] with
        {
            Issues = [issue, issue],
            Wifi = session.Samples[0].Wifi! with { Issues = [issue] },
            Icmp = session.Samples[0].Icmp! with
            {
                Issue = issue,
                Route = new(session.StartedAt, 1, 1, true, issue)
            }
        };
        var second = session.Samples[1] with { Issues = [issue] };
        var summary = SessionAnalyzer.Analyze(session with { Samples = [first, second] });
        Assert.Equal(new IssueCount(IssueKind.NativeError, 27, 2), Assert.Single(summary.Issues));
    }

    [Fact]
    public async Task Unknown_send_attempts_and_rejected_routes_remain_distinct()
    {
        var session = await TestData.Session();
        var first = session.Samples[0] with
        {
            Icmp = session.Samples[0].Icmp! with { Outcome = IcmpOutcome.LocalError, SendAttempted = null, RoundTripTimeMilliseconds = null }
        };
        var second = session.Samples[1] with
        {
            Icmp = session.Samples[1].Icmp! with { Outcome = IcmpOutcome.RouteRejected, SendAttempted = false, RoundTripTimeMilliseconds = null }
        };
        var summary = SessionAnalyzer.Analyze(session with { Samples = [first, second] });
        Assert.Equal(0, summary.IcmpAttempts);
        Assert.Equal(1, summary.UnknownIcmpAttempts);
        Assert.Null(summary.LatencyMilliseconds.Mean);
        Assert.Equal(2, summary.IcmpOutcomes.Count);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("clock")]
    [InlineData("backwards")]
    [InlineData("duplicate")]
    [InlineData("negative-rtt")]
    [InlineData("missing-rtt")]
    [InlineData("unknown-outcome")]
    public async Task Incompatible_or_inconsistent_files_are_not_summarized_as_valid_measurements(string fault)
    {
        var session = await TestData.Session();
        var first = session.Samples[0];
        session = fault switch
        {
            "schema" => session with { SchemaVersion = 2 },
            "clock" => session with { MonotonicFrequency = 0 },
            "backwards" => session with { CompletedAt = session.StartedAt with { MonotonicTicks = session.StartedAt.MonotonicTicks - 1 } },
            "duplicate" => session with { Samples = [first, first] },
            "negative-rtt" => session with { Samples = [first with { Icmp = first.Icmp! with { RoundTripTimeMilliseconds = -1 } }] },
            "missing-rtt" => session with { Samples = [first with { Icmp = first.Icmp! with { RoundTripTimeMilliseconds = null } }] },
            "unknown-outcome" => session with { Samples = [first with { Icmp = first.Icmp! with { Outcome = (IcmpOutcome)999 } }] },
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<InvalidDataException>(() => SessionAnalyzer.Analyze(session));
    }
}
