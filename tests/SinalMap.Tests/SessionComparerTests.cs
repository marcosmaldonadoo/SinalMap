using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class SessionComparerTests
{
    [Fact]
    public async Task Compatible_records_use_B_minus_A_and_RSSI_difference_is_in_dB()
    {
        var comparison = SessionComparer.Compare(await CreateSession(-50, 0), await CreateSession(-70, 9));
        Assert.Equal(-20, comparison.MeanRssiDifferenceDb);
        Assert.Equal(9, comparison.MeanLatencyDifferenceMilliseconds);
        Assert.Equal(ContextComparison.Same, comparison.Context.Bssid);
        Assert.Equal(60, comparison.First.RssiDbm.Count);
    }

    [Theory]
    [InlineData("ssid")]
    [InlineData("interface")]
    [InlineData("missing-wifi")]
    public async Task Different_or_unknown_network_context_suppresses_both_differences(string condition)
    {
        var first = await CreateSession();
        var second = await CreateSession();
        if (condition == "ssid") second = MapWifi(second, w => w with { SsidHex = "6F74686572" });
        if (condition == "interface")
        {
            var other = Guid.NewGuid();
            second = MapWifi(second with { InterfaceId = other }, w => w with { InterfaceId = other });
        }
        if (condition == "missing-wifi") second = second with { Samples = second.Samples.Select(s => s with { Wifi = null }).ToArray() };
        var comparison = SessionComparer.Compare(first, second);
        Assert.Null(comparison.MeanRssiDifferenceDb);
        Assert.Null(comparison.MeanLatencyDifferenceMilliseconds);
    }

    [Fact]
    public async Task Different_ICMP_targets_suppress_latency_without_discarding_RSSI_comparison()
    {
        var comparison = SessionComparer.Compare(await CreateSession(-50), (await CreateSession(-60)) with { TargetAddress = "192.0.2.2" });
        Assert.Equal(ContextComparison.Different, comparison.Context.Target);
        Assert.Null(comparison.MeanLatencyDifferenceMilliseconds);
        Assert.Equal(-10, comparison.MeanRssiDifferenceDb);
    }

    [Theory]
    [InlineData("bssid")]
    [InlineData("channel")]
    [InlineData("frequency")]
    [InlineData("source")]
    [InlineData("unknown-source")]
    [InlineData("missing-channel")]
    [InlineData("unknown-frequency-source")]
    public async Task Radio_context_must_be_known_and_equal_for_an_RSSI_difference(string condition)
    {
        var first = await CreateSession();
        var second = MapWifi(await CreateSession(), w => condition switch
        {
            "bssid" => w with { Bssid = "02:00:00:00:00:02" },
            "channel" => w with { Channel = 11 },
            "frequency" => w with { FrequencyKHz = 2462000 },
            "source" => w with { RssiSource = "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" },
            "unknown-source" => w with { RssiSource = "untrusted" },
            "missing-channel" => w with { Channel = null },
            "unknown-frequency-source" => w with { FrequencySource = "untrusted" },
            _ => throw new InvalidOperationException()
        });
        var comparison = SessionComparer.Compare(first, second);
        Assert.Null(comparison.MeanRssiDifferenceDb);
        Assert.Equal(0, comparison.MeanLatencyDifferenceMilliseconds);
    }

    [Fact]
    public async Task Unknown_or_missing_values_are_not_equal_context_and_never_produce_zero_differences()
    {
        var first = MapWifi(await CreateSession(), w => w with { SsidHex = null, Bssid = null, RssiDbm = null });
        var second = MapWifi(await CreateSession(), w => w with { SsidHex = null, Bssid = null, RssiDbm = null });
        var comparison = SessionComparer.Compare(first, second);
        Assert.Equal(ContextComparison.Unknown, comparison.Context.Ssid);
        Assert.Equal(ContextComparison.Unknown, comparison.Context.Bssid);
        Assert.Null(comparison.MeanRssiDifferenceDb);
        Assert.Null(comparison.MeanLatencyDifferenceMilliseconds);
    }

    [Fact]
    public async Task Missing_metric_values_do_not_turn_into_a_zero_difference_even_with_matching_SSID()
    {
        var first = await CreateSession();
        var second = await CreateSession();
        second = second with { Samples = second.Samples.Select(s => s with
        {
            Wifi = s.Wifi! with { RssiDbm = null, RssiSource = null },
            Icmp = s.Icmp! with { Outcome = IcmpOutcome.TimedOut, RoundTripTimeMilliseconds = null }
        }).ToArray() };
        var comparison = SessionComparer.Compare(first, second);
        Assert.Equal(ContextComparison.Same, comparison.Context.Ssid);
        Assert.Equal(0, comparison.Second.LatencyMilliseconds.Count);
        Assert.Equal(0, comparison.Second.RssiDbm.Count);
        Assert.Null(comparison.MeanLatencyDifferenceMilliseconds);
        Assert.Null(comparison.MeanRssiDifferenceDb);
    }

    [Theory]
    [InlineData("bssid")]
    [InlineData("ssid")]
    [InlineData("source")]
    [InlineData("interface")]
    public async Task Variations_inside_one_session_are_distinct_from_missing_context(string field)
    {
        var first = await CreateSession();
        var second = await CreateSession();
        var samples = second.Samples.ToArray();
        samples[0] = samples[0] with { Wifi = field switch
        {
            "bssid" => samples[0].Wifi! with { Bssid = "02:00:00:00:00:02" },
            "ssid" => samples[0].Wifi! with { SsidHex = "6F74686572" },
            "source" => samples[0].Wifi! with { RssiSource = "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" },
            "interface" => samples[0].Wifi! with { InterfaceId = Guid.NewGuid() },
            _ => throw new InvalidOperationException()
        }};
        var comparison = SessionComparer.Compare(first, second with { Samples = samples });
        var context = field switch
        {
            "bssid" => comparison.Context.Bssid,
            "ssid" => comparison.Context.Ssid,
            "source" => comparison.Context.RssiSource,
            _ => comparison.Context.Interface
        };
        Assert.Equal(ContextComparison.VariedWithinSession, context);
        Assert.Null(comparison.MeanRssiDifferenceDb);
    }

    [Fact]
    public async Task Non_UTF8_SSID_bytes_and_equivalent_MAC_formats_can_match_without_inventing_text()
    {
        var first = MapWifi(await CreateSession(), w => w with { Ssid = null, SsidHex = "FFFE", Bssid = "02:AA:BB:CC:DD:01" });
        var second = MapWifi(await CreateSession(), w => w with { Ssid = null, SsidHex = "fffe", Bssid = "02-AA-BB-CC-DD-01" });
        var comparison = SessionComparer.Compare(first, second);
        Assert.Equal(ContextComparison.Same, comparison.Context.Ssid);
        Assert.Equal(ContextComparison.Same, comparison.Context.Bssid);
        Assert.Equal(0, comparison.MeanRssiDifferenceDb);
    }

    [Theory]
    [InlineData("hex")]
    [InlineData("bssid")]
    [InlineData("target")]
    public async Task Malformed_identifiers_are_not_treated_as_known_equal_values(string field)
    {
        var first = await CreateSession();
        first = field switch
        {
            "hex" => MapWifi(first, w => w with { SsidHex = "not-hex" }),
            "bssid" => MapWifi(first, w => w with { Bssid = "00:00:00:00:00:00" }),
            _ => first with { TargetAddress = "127.1" }
        };
        var comparison = SessionComparer.Compare(first, first with { SessionId = Guid.NewGuid() });
        Assert.Equal(ContextComparison.Unknown, field switch
        {
            "hex" => comparison.Context.Ssid,
            "bssid" => comparison.Context.Bssid,
            _ => comparison.Context.Target
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_same_session_or_missing_session_identity_is_not_a_second_collection(bool same)
    {
        var first = await CreateSession();
        var second = first with { SessionId = same ? first.SessionId : Guid.Empty };
        Assert.Throws<InvalidDataException>(() => SessionComparer.Compare(first, second));
    }

    internal static async Task<ProbeSession> CreateSession(int rssi = -55, long latency = 4)
    {
        var session = await TestData.Session();
        return session with { Samples = session.Samples.Select(s => s with
        {
            Wifi = s.Wifi! with
            {
                RssiDbm = rssi,
                RssiSource = "WlanQueryInterface:rssi",
                ChannelSource = "WlanQueryInterface:channel_number",
                FrequencySource = "WlanGetNetworkBssList:ulChCenterFrequency (cached)"
            },
            Icmp = s.Icmp! with { RoundTripTimeMilliseconds = latency }
        }).ToArray() };
    }

    internal static ProbeSession MapWifi(ProbeSession session, Func<WifiSnapshot, WifiSnapshot> change) =>
        session with { Samples = session.Samples.Select(s => s with { Wifi = change(s.Wifi!) }).ToArray() };
}
