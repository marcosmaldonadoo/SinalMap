using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Tests;

public sealed class SessionContextSummaryTests
{
    [Fact]
    public async Task Equivalent_identifiers_and_non_UTF8_SSID_bytes_do_not_imply_variation()
    {
        var session = await SessionComparerTests.CreateSession();
        var first = session.Samples[0] with { Wifi = session.Samples[0].Wifi! with
        {
            Ssid = null, SsidHex = "FFFE", Bssid = "02:AA:BB:CC:DD:01"
        }};
        var second = session.Samples[1] with { Wifi = first.Wifi! with
        {
            SsidHex = "fffe", Bssid = "02-AA-BB-CC-DD-01"
        }};
        var context = SessionAnalyzer.Analyze(session with { Samples = [first, second] }).Context;
        Assert.Equal(new SessionContextSummary(
            RecordedContextState.SingleValue, RecordedContextState.SingleValue, RecordedContextState.SingleValue,
            RecordedContextState.SingleValue, RecordedContextState.SingleValue, RecordedContextState.SingleValue,
            RecordedContextState.SingleValue), context);
        Assert.False(context.HasVariation);
        Assert.False(context.HasUnknown);
    }

    [Theory]
    [InlineData("interface")]
    [InlineData("ssid")]
    [InlineData("bssid")]
    [InlineData("channel")]
    [InlineData("frequency")]
    [InlineData("source")]
    public async Task Variation_is_detected_without_relying_on_the_BSSID_event_list(string field)
    {
        var session = await SessionComparerTests.CreateSession();
        var first = session.Samples[0];
        var wifi = first.Wifi!;
        var second = session.Samples[1] with { Wifi = field switch
        {
            "interface" => wifi with { InterfaceId = Guid.NewGuid() },
            "ssid" => wifi with { SsidHex = "6F74686572" },
            "bssid" => wifi with { Bssid = "02:00:00:00:00:02" },
            "channel" => wifi with { Channel = 11 },
            "frequency" => wifi with { FrequencyKHz = 2462000 },
            _ => wifi with { RssiSource = "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" }
        }};
        var summary = SessionAnalyzer.Analyze(session with { Samples = [first, second], BssidChanges = [] });
        var state = field switch
        {
            "interface" => summary.Context.Interface,
            "ssid" => summary.Context.Ssid,
            "bssid" => summary.Context.Bssid,
            "channel" => summary.Context.Channel,
            "frequency" => summary.Context.CachedFrequency,
            _ => summary.Context.RssiSource
        };
        Assert.Equal(RecordedContextState.Varied, state);
        Assert.True(summary.Context.HasVariation);
        Assert.Equal(0, summary.BssidChanges);
        Assert.Equal(-55, summary.RssiDbm.Mean);
    }

    [Theory]
    [InlineData("ssid")]
    [InlineData("bssid")]
    [InlineData("channel")]
    [InlineData("frequency")]
    [InlineData("source")]
    public async Task A_missing_or_unrecognized_value_prevents_a_single_value_claim(string field)
    {
        var session = await SessionComparerTests.CreateSession();
        var first = session.Samples[0];
        var wifi = first.Wifi!;
        var second = session.Samples[1] with { Wifi = field switch
        {
            "ssid" => wifi with { SsidHex = "invalid" },
            "bssid" => wifi with { Bssid = "00:00:00:00:00:00" },
            "channel" => wifi with { Channel = null },
            "frequency" => wifi with { FrequencySource = "unrecognized" },
            _ => wifi with { RssiSource = "unrecognized" }
        }};
        var context = SessionAnalyzer.Analyze(session with { Samples = [first, second] }).Context;
        var state = field switch
        {
            "ssid" => context.Ssid,
            "bssid" => context.Bssid,
            "channel" => context.Channel,
            "frequency" => context.CachedFrequency,
            _ => context.RssiSource
        };
        Assert.Equal(RecordedContextState.Unknown, state);
        Assert.True(context.HasUnknown);
        Assert.False(context.HasVariation);
    }

    [Fact]
    public async Task Observed_variation_remains_visible_in_the_presence_of_missing_identifiers()
    {
        var session = await SessionComparerTests.CreateSession();
        var samples = session.Samples.Take(3).ToArray();
        samples[1] = samples[1] with { Wifi = samples[1].Wifi! with { SsidHex = null } };
        samples[2] = samples[2] with { Wifi = samples[2].Wifi! with { SsidHex = "6F74686572" } };
        Assert.Equal(RecordedContextState.Varied, SessionAnalyzer.Analyze(session with { Samples = samples }).Context.Ssid);
    }

    [Fact]
    public async Task Disconnected_or_absent_observations_and_missing_RSSI_do_not_supply_context_values()
    {
        var session = await SessionComparerTests.CreateSession();
        var sample = session.Samples[0];
        var disconnected = sample with { Wifi = sample.Wifi! with
        {
            State = WifiConnectionState.Disconnected, RssiDbm = null, Channel = null, FrequencyKHz = null,
            SsidHex = "6F74686572", Bssid = "02:00:00:00:00:02"
        }};
        var context = SessionAnalyzer.Analyze(session with { Samples = [disconnected, session.Samples[1] with { Wifi = null }] }).Context;
        Assert.Equal(RecordedContextState.Unknown, context.Ssid);
        Assert.Equal(RecordedContextState.Unknown, context.Bssid);
        Assert.Equal(RecordedContextState.Unknown, context.RssiSource);
        Assert.False(context.HasVariation);

        var noRssi = session.Samples[1] with { Wifi = sample.Wifi! with { RssiDbm = null, RssiSource = "unrecognized" } };
        context = SessionAnalyzer.Analyze(session with { Samples = [sample, noRssi] }).Context;
        Assert.Equal(RecordedContextState.SingleValue, context.RssiSource);
        Assert.Equal(RecordedContextState.SingleValue, context.Ssid);
    }

    [Fact]
    public async Task Empty_session_with_invalid_header_has_no_known_context()
    {
        var session = (await SessionComparerTests.CreateSession()) with
        {
            Samples = [], InterfaceId = Guid.Empty, TargetAddress = "127.1"
        };
        var context = SessionAnalyzer.Analyze(session).Context;
        Assert.Equal(new SessionContextSummary(
            RecordedContextState.Unknown, RecordedContextState.Unknown, RecordedContextState.Unknown,
            RecordedContextState.Unknown, RecordedContextState.Unknown, RecordedContextState.Unknown,
            RecordedContextState.Unknown), context);
    }
}
