using System.Text.Json;
using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;
using SinalMap.Presentation;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class WifiScanMetadataTests
{
    [Fact]
    public async Task Scan_metadata_roundtrips_and_warns_about_RTT_without_changing_sample_values()
    {
        var original = await TestData.Session();
        var stamp = original.StartedAt;
        var session = original with { ActiveWifiScanning = true, WifiScans = [new(stamp, stamp, true, null)] };
        var json = JsonSerializer.Serialize(session, JsonSessionStore.CreateOptions());
        var restored = JsonSerializer.Deserialize<ProbeSession>(json, JsonSessionStore.CreateOptions())!;
        var summary = SessionAnalyzer.Analyze(restored);
        Assert.True(summary.ActiveWifiScanning);
        Assert.Equal(1, summary.WifiScansCompleted);
        Assert.Equal(original.Samples.Select(s => s.Wifi!.RssiDbm), restored.Samples.Select(s => s.Wifi!.RssiDbm));
        Assert.Contains("RTT", new SessionResultViewModel(restored).ScanNotice);
        var legacyJson = JsonSerializer.SerializeToNode(original, JsonSessionStore.CreateOptions())!;
        legacyJson.AsObject().Remove("activeWifiScanning");
        legacyJson.AsObject().Remove("wifiScans");
        var legacy = legacyJson.Deserialize<ProbeSession>(JsonSessionStore.CreateOptions())!;
        Assert.False(SessionAnalyzer.Analyze(legacy).ActiveWifiScanning);
    }

    [Fact]
    public async Task Different_scan_policies_prevent_a_comparison_that_ignores_radio_interference()
    {
        var first = await SessionComparerTests.CreateSession();
        var second = first with { SessionId = Guid.NewGuid(), ActiveWifiScanning = true, WifiScans = [] };
        var comparison = SessionComparer.Compare(first, second);
        Assert.Equal(ContextComparison.Different, comparison.Context.ActiveScanning);
        Assert.Null(comparison.MeanLatencyDifferenceMilliseconds);
        Assert.Null(comparison.MeanRssiDifferenceDb);
    }

    [Fact]
    public async Task Invalid_scan_timestamps_or_success_with_error_are_rejected()
    {
        var session = await TestData.Session();
        var stamp = session.StartedAt;
        var invalid = session with { ActiveWifiScanning = true, WifiScans = [new(stamp, stamp, true, new(IssueKind.NativeError, "scan", "synthetic"))] };
        Assert.Throws<InvalidDataException>(() => SessionAnalyzer.Analyze(invalid));
        invalid = session with { ActiveWifiScanning = true, WifiScans = [new(stamp, stamp with { MonotonicTicks = stamp.MonotonicTicks - 1 }, true, null)] };
        Assert.Throws<InvalidDataException>(() => SessionAnalyzer.Analyze(invalid));
    }
}
