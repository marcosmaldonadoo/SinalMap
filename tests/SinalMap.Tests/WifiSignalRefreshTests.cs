using System.Runtime.InteropServices;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;
using SinalMap.Infrastructure.Windows.Native;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class WifiSignalRefreshTests
{
    [Fact]
    public async Task Slow_native_scan_keeps_handle_and_callback_alive_after_the_callers_timeout()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new FakeWlanApi { OnScan = _ => { entered.TrySetResult(); release.Wait(TimeSpan.FromSeconds(3)); } };
        var refresher = new WindowsWifiSignalRefresher(api, new ManualClock(), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));
        var running = refresher.RefreshAsync(TestData.InterfaceId, CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var timedOut = await running.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(IssueKind.ObservationTimeout, timedOut.Issue!.Kind);
            Assert.Equal(0, api.CloseCount);
            Assert.NotNull(api.ScanCallback);
            var busy = await refresher.RefreshAsync(TestData.InterfaceId, CancellationToken.None);
            Assert.Equal(IssueKind.Busy, busy.Issue!.Kind);
            Assert.Equal(1, api.ScanCalls);
        }
        finally { release.Set(); }
        await api.Closed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Null(api.ScanCallback);
    }

    [Fact]
    public async Task Scan_waits_for_the_selected_interface_notification_and_unregisters_before_close()
    {
        var api = new FakeWlanApi();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.OnScan = _ => entered.TrySetResult();
        var running = new WindowsWifiSignalRefresher(api, new ManualClock()).RefreshAsync(TestData.InterfaceId, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Notify(api, Guid.NewGuid(), 7);
        Notify(api, TestData.InterfaceId, 7, source: 16);
        Assert.False(running.IsCompleted);
        Assert.Equal(0, api.CloseCount);
        Notify(api, TestData.InterfaceId, 7);
        Assert.True((await running).Completed);
        Assert.Equal(new uint[] { 8, 0 }, api.NotificationSources);
        Assert.Equal(1, api.CloseCount);
        Assert.Null(api.ScanCallback);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Native_scan_and_registration_errors_preserve_the_code_without_claiming_completion(bool registration)
    {
        var api = new FakeWlanApi { RegistrationCode = registration ? 5u : 0, ScanCode = 5 };
        var result = await new WindowsWifiSignalRefresher(api, new ManualClock()).RefreshAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.False(result.Completed);
        Assert.Equal(5u, result.Issue!.NativeCode);
        Assert.Equal(IssueKind.PermissionDenied, result.Issue.Kind);
        Assert.Equal(registration ? 0 : 1, api.ScanCalls);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Accepted_request_without_notification_is_a_timeout_and_not_a_completed_scan()
    {
        var api = new FakeWlanApi();
        var result = await new WindowsWifiSignalRefresher(api, new ManualClock(), TimeSpan.FromMilliseconds(20))
            .RefreshAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.False(result.Completed);
        Assert.Equal(IssueKind.ObservationTimeout, result.Issue!.Kind);
        Assert.Equal(new uint[] { 8, 0 }, api.NotificationSources);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Scan_failure_notification_keeps_the_WLAN_reason_code()
    {
        var api = new FakeWlanApi();
        api.OnScan = id =>
        {
            var reason = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt32(reason, 0x10001);
                var data = new WlanNotificationData { Source = 8, Code = 8, InterfaceId = id, DataSize = 4, Data = reason };
                api.ScanCallback!(ref data, 0);
            }
            finally { Marshal.FreeHGlobal(reason); }
        };
        var result = await new WindowsWifiSignalRefresher(api, new ManualClock()).RefreshAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.False(result.Completed);
        Assert.Equal(0x10001u, result.Issue!.NativeCode);
        Assert.Equal(IssueKind.NativeError, result.Issue.Kind);
    }

    [Fact]
    public async Task Cancellation_after_acceptance_waits_for_scan_end_and_prevents_an_overlapping_request()
    {
        var api = new FakeWlanApi();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.OnScan = _ => entered.TrySetResult();
        using var cancel = new CancellationTokenSource();
        var refresher = new WindowsWifiSignalRefresher(api, new ManualClock());
        var running = refresher.RefreshAsync(TestData.InterfaceId, cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        cancel.Cancel();
        Assert.False(running.IsCompleted);
        var busy = await refresher.RefreshAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(IssueKind.Busy, busy.Issue!.Kind);
        Assert.Equal(1, api.ScanCalls);
        Notify(api, TestData.InterfaceId, 7);
        Assert.True((await running).Completed);
        Assert.Equal(1, api.CloseCount);
    }

    [Fact]
    public async Task Preferred_realtime_RSSI_changes_even_when_the_legacy_query_is_constant()
    {
        var api = new FakeWlanApi { Rssi = -66 };
        api.QueryErrors[WlanOpcode.RealtimeConnectionQuality] = 0;
        var values = new Queue<int>([-65, -72]);
        api.BeforeQuery = opcode => { if (opcode == WlanOpcode.RealtimeConnectionQuality) api.RealtimeLinks = [new() { Rssi = values.Dequeue() }]; };
        var monitor = new WindowsWifiMonitor(api, new ManualClock(), preferRealtimeRssi: true);
        var first = await monitor.ReadAsync(TestData.InterfaceId, CancellationToken.None);
        var second = await monitor.ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(-65, first.RssiDbm);
        Assert.Equal(-72, second.RssiDbm);
        Assert.Equal("WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi", second.RssiSource);
        Assert.DoesNotContain(WlanOpcode.Rssi, api.Queries);
        Assert.Equal(api.AllocationCount, api.FreeCount);
    }

    [Fact]
    public async Task Realtime_unsupported_uses_the_legacy_query_and_preserves_the_error()
    {
        var api = new FakeWlanApi();
        var result = await new WindowsWifiMonitor(api, new ManualClock(), preferRealtimeRssi: true)
            .ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Equal(-55, result.RssiDbm);
        Assert.Equal("WlanQueryInterface:rssi", result.RssiSource);
        Assert.Contains(result.Issues, issue => issue.NativeCode == 50);
    }

    [Theory]
    [InlineData("mlo")]
    [InlineData("truncated")]
    [InlineData("permission")]
    public async Task Preferred_realtime_does_not_bypass_ambiguity_invalid_data_or_denied_access(string condition)
    {
        var api = new FakeWlanApi();
        api.QueryErrors[WlanOpcode.RealtimeConnectionQuality] = condition == "permission" ? 5u : 0;
        if (condition == "mlo") api.RealtimeIsMlo = 1;
        if (condition == "truncated") api.DeclaredQuerySizes[WlanOpcode.RealtimeConnectionQuality] = 24;
        var result = await new WindowsWifiMonitor(api, new ManualClock(), preferRealtimeRssi: true)
            .ReadAsync(TestData.InterfaceId, CancellationToken.None);
        Assert.Null(result.RssiDbm);
        Assert.DoesNotContain(WlanOpcode.Rssi, api.Queries);
    }

    private static void Notify(FakeWlanApi api, Guid id, uint code, uint source = 8)
    {
        var data = new WlanNotificationData { Source = source, Code = code, InterfaceId = id };
        api.ScanCallback!(ref data, 0);
    }
}
