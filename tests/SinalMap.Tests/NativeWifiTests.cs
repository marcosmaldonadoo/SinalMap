using System.Runtime.InteropServices;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;
using SinalMap.Infrastructure.Windows.Native;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class NativeWifiTests
{
    [Fact]
    public void Abi_layout_matches_Windows_SDK_fixed_width_fields_and_alignment()
    {
        Assert.Equal(36, Marshal.SizeOf<Dot11Ssid>());
        Assert.Equal(6, Marshal.SizeOf<Dot11MacAddress>());
        Assert.Equal(532, Marshal.SizeOf<WlanInterfaceInfo>());
        Assert.Equal(68, Marshal.SizeOf<WlanAssociationAttributes>());
        Assert.Equal(604, Marshal.SizeOf<WlanConnectionAttributes>());
        Assert.Equal(360, Marshal.SizeOf<WlanBssEntry>());
        Assert.Equal(64, Marshal.OffsetOf<WlanBssEntry>(nameof(WlanBssEntry.InRegDomain)).ToInt32());
        Assert.Equal(80, Marshal.OffsetOf<WlanBssEntry>(nameof(WlanBssEntry.HostTimestamp)).ToInt32());
        Assert.Equal(92, Marshal.OffsetOf<WlanBssEntry>(nameof(WlanBssEntry.CenterFrequencyKHz)).ToInt32());
        Assert.Equal(520, Marshal.OffsetOf<WlanConnectionAttributes>(nameof(WlanConnectionAttributes.Association)).ToInt32());
        Assert.Equal(16, Marshal.SizeOf<WindowsRouteInspector.SockaddrIn>());
    }

    [Fact]
    public async Task Uses_current_RSSI_and_matches_exact_BSSID_in_the_cached_BSS_list()
    {
        var api = new FakeWlanApi
        {
            BssEntries = [FakeWlanApi.Bss("020000000099", 5180000), FakeWlanApi.Bss()]
        };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(WifiConnectionState.Connected, result.State);
        Assert.Equal("test", result.Ssid);
        Assert.Equal("74657374", result.SsidHex);
        Assert.Equal("02:00:00:00:00:01", result.Bssid);
        Assert.Equal(-55, result.RssiDbm);
        Assert.Equal(6u, result.Channel);
        Assert.Equal(2437000u, result.FrequencyKHz);
        Assert.Equal(134116992000000000ul, result.BssHostTimestampFileTime);
        Assert.Contains("cached", result.FrequencySource!);
        Assert.Empty(result.Issues);
        Assert.Equal(api.AllocationCount, api.FreeCount);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Unsupported_metrics_are_null_and_never_estimated_from_quality_or_frequency()
    {
        var api = new FakeWlanApi();
        api.QueryErrors[WlanOpcode.Rssi] = 50;
        api.QueryErrors[WlanOpcode.ChannelNumber] = 50;
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Null(result.RssiDbm); // SignalQuality=90 must not be converted into a measured RSSI.
        Assert.Null(result.Channel); // 2437000 kHz must not be silently converted to channel 6.
        Assert.Equal(2437000u, result.FrequencyKHz);
        Assert.Equal(2, result.Issues.Count(i => i.Kind == IssueKind.NotSupported));
        Assert.Equal(api.AllocationCount, api.FreeCount);
    }

    [Fact]
    public async Task Missing_connected_BSS_does_not_borrow_a_neighbor_frequency()
    {
        var api = new FakeWlanApi { BssEntries = [FakeWlanApi.Bss("020000000099", 5180000)] };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Null(result.FrequencyKHz);
        Assert.Null(result.BssHostTimestampFileTime);
        Assert.Contains(result.Issues, i => i.Kind == IssueKind.BssEntryMissing);
    }

    [Fact]
    public async Task Channel_zero_and_BSS_error_do_not_hide_the_current_RSSI()
    {
        var api = new FakeWlanApi { Channel = 0, BssCode = 5 };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(-55, result.RssiDbm);
        Assert.Null(result.Channel);
        Assert.Null(result.FrequencyKHz);
        Assert.Contains(result.Issues, i => i.Kind == IssueKind.PermissionDenied && i.NativeCode == 5);
        Assert.Equal(api.AllocationCount, api.FreeCount);
    }

    [Fact]
    public async Task Empty_BSS_list_is_valid_and_has_no_frequency()
    {
        var api = new FakeWlanApi { BssEntries = [] };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Null(result.FrequencyKHz);
        Assert.Contains(result.Issues, i => i.Kind == IssueKind.BssEntryMissing);
        Assert.Equal(api.AllocationCount, api.FreeCount);
    }

    [Fact]
    public async Task FHSS_frequency_is_not_treated_as_valid()
    {
        var bss = FakeWlanApi.Bss();
        bss.PhyType = 1;
        var api = new FakeWlanApi { BssEntries = [bss] };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Null(result.FrequencyKHz);
        Assert.Contains(result.Issues, i => i.Kind == IssueKind.NotSupported);
    }

    [Fact]
    public void Disposing_a_native_client_twice_closes_the_handle_once()
    {
        var api = new FakeWlanApi();
        var client = new WlanClient(api);
        client.Dispose();
        client.Dispose();
        Assert.Equal(1, api.CloseCount);
    }

    [Theory]
    [InlineData(5u, IssueKind.PermissionDenied)]
    [InlineData(1062u, IssueKind.ServiceUnavailable)]
    [InlineData(5023u, IssueKind.NotConnected)]
    [InlineData(1168u, IssueKind.InterfaceMissing)]
    public async Task Native_errors_keep_the_actual_code_and_free_returned_memory(uint code, IssueKind kind)
    {
        var api = new FakeWlanApi();
        api.QueryErrors[WlanOpcode.CurrentConnection] = code;
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(kind, issue.Kind);
        Assert.Equal(code, issue.NativeCode);
        Assert.Null(result.Ssid);
        Assert.Null(result.RssiDbm);
        Assert.Equal(api.AllocationCount, api.FreeCount);
        Assert.Equal(1, api.CloseCount);
        Assert.Single(api.Queries);
    }

    [Fact]
    public async Task Permission_guidance_does_not_tell_the_user_to_bypass_consent()
    {
        var api = new FakeWlanApi();
        api.QueryErrors[WlanOpcode.CurrentConnection] = 5;
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Contains("Location", Assert.Single(result.Issues).Message);
    }

    [Fact]
    public async Task Roaming_between_bracketing_reads_discards_the_mixed_snapshot()
    {
        var api = new FakeWlanApi { ConnectionAtRead = i => FakeWlanApi.Connected(i == 0 ? "020000000001" : "020000000002") };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(WifiConnectionState.Unavailable, result.State);
        Assert.Null(result.RssiDbm);
        Assert.Null(result.Channel);
        Assert.Null(result.FrequencyKHz);
        Assert.Null(result.Bssid);
        Assert.Contains(result.Issues, i => i.Kind == IssueKind.ConnectionChangedDuringRead);
        Assert.Equal(api.AllocationCount, api.FreeCount);
    }

    [Fact]
    public async Task Truncated_query_is_rejected_before_marshalling_and_is_freed()
    {
        var api = new FakeWlanApi();
        api.DeclaredQuerySizes[WlanOpcode.CurrentConnection] = 4;
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(IssueKind.InvalidData, Assert.Single(result.Issues).Kind);
        Assert.Equal(api.AllocationCount, api.FreeCount);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Null_success_buffer_is_rejected_without_dereferencing_it()
    {
        var api = new FakeWlanApi { NullQueryPointer = true };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(IssueKind.InvalidData, Assert.Single(result.Issues).Kind);
        Assert.Equal(api.AllocationCount, api.FreeCount);
    }

    [Theory]
    [InlineData(7u, 1u)]
    [InlineData(8u, 1u)]
    [InlineData(368u, uint.MaxValue)]
    public async Task Invalid_BSS_size_or_count_is_rejected_and_the_buffer_is_freed(uint size, uint count)
    {
        var api = new FakeWlanApi { BssDeclaredSize = size, BssDeclaredCount = count };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Null(result.FrequencyKHz);
        Assert.Contains(result.Issues, i => i.Kind == IssueKind.InvalidData);
        Assert.Equal(api.AllocationCount, api.FreeCount);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Oversized_SSID_is_rejected_without_reading_beyond_the_fixed_array()
    {
        var connection = FakeWlanApi.Connected();
        connection.Association.Ssid.Length = 33;
        var api = new FakeWlanApi { Connection = connection };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(IssueKind.InvalidData, Assert.Single(result.Issues).Kind);
        Assert.Equal(api.AllocationCount, api.FreeCount);
    }

    [Fact]
    public async Task Non_UTF8_SSID_keeps_exact_bytes_without_replacement_characters()
    {
        var api = new FakeWlanApi { Connection = FakeWlanApi.Connected(ssid: [0xFF, 0x00, 0xFE]) };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(WifiConnectionState.Connected, result.State);
        Assert.Null(result.Ssid);
        Assert.Equal("FF00FE", result.SsidHex);
    }

    [Fact]
    public async Task Enumerates_interfaces_and_owns_the_list_and_handle()
    {
        var api = new FakeWlanApi();
        var interfaces = await Monitor(api).ListInterfacesAsync(CancellationToken.None);
        Assert.Equal(TestData.InterfaceId, Assert.Single(interfaces).Id);
        Assert.Equal(api.AllocationCount, api.FreeCount);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Enumeration_error_frees_any_returned_buffer_and_closes_handle()
    {
        var api = new FakeWlanApi { EnumCode = 1062 };
        var error = await Assert.ThrowsAsync<NativeWifiException>(() => Monitor(api).ListInterfacesAsync(CancellationToken.None));
        Assert.Equal(IssueKind.ServiceUnavailable, error.Issue.Kind);
        Assert.Equal(api.AllocationCount, api.FreeCount);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Open_failure_does_not_attempt_queries_and_closes_a_nonzero_returned_handle()
    {
        var api = new FakeWlanApi { OpenCode = 5 };
        var result = await Monitor(api).ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(IssueKind.PermissionDenied, Assert.Single(result.Issues).Kind);
        Assert.Empty(api.Queries);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Timed_out_native_worker_keeps_ownership_until_return_and_blocks_overlap()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeWlanApi
        {
            BeforeQuery = _ => { entered.TrySetResult(); release.Wait(TimeSpan.FromSeconds(5)); }
        };
        var monitor = Monitor(api);
        var read = monitor.ReadAsync(TestData.InterfaceId, CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var timedOut = await read.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(IssueKind.ObservationTimeout, Assert.Single(timedOut.Issues).Kind);
            Assert.Equal(0, Volatile.Read(ref api.CloseCount));
            var busy = await monitor.ReadAsync(TestData.InterfaceId, CancellationToken.None);
            Assert.Equal(IssueKind.Busy, Assert.Single(busy.Issues).Kind);
        }
        finally { release.Set(); }
        await api.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(api.AllocationCount, api.FreeCount);
        Assert.Equal(1, api.CloseCount);
    }

    private static WindowsWifiMonitor Monitor(FakeWlanApi api) => new(api, new ManualClock());
}
