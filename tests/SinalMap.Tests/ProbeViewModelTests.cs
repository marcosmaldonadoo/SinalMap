using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Presentation;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class ProbeViewModelTests
{
    [Fact]
    public async Task Collection_waits_for_an_accepted_preview_scan_and_records_only_its_own_scan_results()
    {
        var refresher = new PendingRefresher();
        var h = new Harness(signalRefresher: refresher);
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        Assert.Equal(1, refresher.Calls);
        var collection = h.Model.StartAsync();
        Assert.False(collection.IsCompleted);
        Assert.Empty(h.Icmp.Invocations);
        var stamp = new ManualClock().Capture();
        refresher.Pending.SetResult(new(stamp, stamp, true, null));
        await collection.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, refresher.Calls);
        Assert.Single(Assert.Single(h.Store.Sessions).WifiScans!);
        Assert.Equal(60, h.Icmp.Invocations.Count);
    }

    private sealed class PendingRefresher : IWifiSignalRefresher
    {
        public int Calls { get; private set; }
        public TaskCompletionSource<WifiScanObservation> Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<WifiScanObservation> RefreshAsync(Guid interfaceId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (++Calls == 1) return Pending.Task;
            var stamp = new ManualClock().Capture();
            return Task.FromResult(new WifiScanObservation(stamp, stamp, true, null));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Collection_records_scans_and_the_RTT_notice_even_when_refresh_fails(bool succeeds)
    {
        var refresher = new Refresher { Succeeds = succeeds };
        var h = new Harness(signalRefresher: refresher);
        await h.Model.RefreshAsync();
        await h.Model.StartAsync();
        var saved = Assert.Single(h.Store.Sessions);
        Assert.True(saved.ActiveWifiScanning);
        Assert.NotEmpty(saved.WifiScans!);
        Assert.All(saved.WifiScans!, scan => Assert.Equal(succeeds, scan.Completed));
        Assert.Contains("RTT", h.Model.Result!.ScanNotice);
        Assert.Equal(60, h.Icmp.Invocations.Count);
    }

    private sealed class Refresher : IWifiSignalRefresher
    {
        public bool Succeeds { get; init; }
        public Task<WifiScanObservation> RefreshAsync(Guid interfaceId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var stamp = new ManualClock().Capture();
            return Task.FromResult(new WifiScanObservation(stamp, stamp, Succeeds,
                Succeeds ? null : new(IssueKind.ObservationTimeout, "WlanScan", "synthetic")));
        }
    }
    [Fact]
    public async Task Gateway_suggestion_follows_the_selected_interface_until_the_target_is_edited()
    {
        var h = new Harness(presetTarget: false);
        h.Wifi.Items = [h.Wifi.Items[0] with { Ipv4Gateway = "192.0.2.1" }];
        await h.Model.RefreshAsync();
        Assert.Equal("192.0.2.1", h.Model.TargetText);
        Assert.Contains("Gateway", h.Model.TargetHint);
        h.Model.SelectedInterface = new(Guid.NewGuid(), "other", WifiConnectionState.Connected) { Ipv4Gateway = "198.51.100.1" };
        Assert.Equal("198.51.100.1", h.Model.TargetText);
        h.Model.SelectedInterface = null;
        Assert.Empty(h.Model.TargetText);
        h.Model.TargetText = "203.0.113.8";
        h.Model.SelectedInterface = h.Wifi.Items[0];
        await h.Model.RefreshAsync();
        Assert.Equal("203.0.113.8", h.Model.TargetText);
        Assert.Empty(h.Icmp.Invocations);
    }

    [Fact]
    public async Task Monitoring_without_a_connected_interface_stays_waiting_without_querying_or_saving()
    {
        var h = new Harness(presetTarget: false);
        h.Wifi.Items = [];
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        Assert.False(h.Model.IsMonitoring);
        Assert.True(h.Model.ShowOperationNotice);
        Assert.Equal("AGUARDANDO", h.Model.Signal.StateLabel);
        Assert.Empty(h.Model.Signal.Points);
        Assert.Equal(0, h.Wifi.Calls);
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Store.Sessions);
        Assert.True(await h.Model.RequestCloseAsync());
    }

    [Fact]
    public async Task Monitoring_shows_RSSI_without_a_target_without_ICMP_and_without_saving()
    {
        var h = new Harness(presetTarget: false);
        var observed = Signal();
        h.Wifi.OnRead = _ => Task.FromResult(TestData.Wifi(new ManualClock()) with { Ssid = "Rede de teste", RssiSource = "WlanQueryInterface:rssi", RssiDbm = -61 });
        h.Model.Signal.PropertyChanged += (_, _) => { if (h.Model.Signal.Value == "-61 dBm") observed.TrySetResult(); };
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("MONITORANDO", h.Model.Signal.StateLabel);
        Assert.Equal("AO VIVO", h.Model.OperationLabel);
        Assert.Equal("Rede de teste", h.Model.ConnectedNetworkName);
        Assert.False(h.Model.ShowOperationNotice);
        Assert.Empty(h.Model.TargetText);
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Store.Sessions);
        Assert.Null(h.Model.Result);
        Assert.False(h.Model.IsBusy);
        Assert.True(await h.Model.RequestCloseAsync());
    }

    [Theory]
    [InlineData(WifiConnectionState.Connected, "Outra rede de teste", "Outra rede de teste")]
    [InlineData(WifiConnectionState.Connected, null, "SSID indisponível")]
    [InlineData(WifiConnectionState.Connected, "", "SSID indisponível")]
    [InlineData(WifiConnectionState.Disconnected, "Nome antigo", "Wi-Fi desconectado")]
    [InlineData(WifiConnectionState.Unavailable, "Nome antigo", "Rede indisponível")]
    public async Task Connected_network_tracks_live_changes_without_reusing_an_old_name(
        WifiConnectionState state, string? ssid, string expected)
    {
        var h = new Harness(presetTarget: false);
        var observed = Signal();
        var calls = 0;
        h.Wifi.OnRead = _ => Task.FromResult(TestData.Wifi(new ManualClock()) with
        {
            State = calls++ == 0 ? WifiConnectionState.Connected : state,
            Ssid = calls == 1 ? "Rede inicial de teste" : ssid,
            SsidHex = "FFFE"
        });
        h.Model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ProbeViewModel.ConnectedNetworkName) && h.Model.ConnectedNetworkName == expected)
                observed.TrySetResult();
        };
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        Assert.Equal("Rede inicial de teste", h.Model.ConnectedNetworkName);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expected, h.Model.ConnectedNetworkName);
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Store.Sessions);
        Assert.True(await h.Model.RequestCloseAsync());
    }

    [Fact]
    public async Task Starting_a_test_waits_for_preview_cancellation_and_keeps_the_saved_result()
    {
        var h = new Harness();
        var entered = Signal();
        var cancelled = Signal();
        var calls = 0;
        h.Wifi.OnRead = async token =>
        {
            if (++calls == 1)
            {
                entered.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { cancelled.TrySetResult(); }
            }
            return TestData.Wifi(new ManualClock()) with { RssiSource = "WlanQueryInterface:rssi" };
        };
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await h.Model.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(cancelled.Task.IsCompleted);
        Assert.Equal(60, h.Icmp.Invocations.Count);
        Assert.Equal(61, calls);
        Assert.Equal("HISTÓRICO", h.Model.Signal.StateLabel);
        Assert.Equal(60, Assert.Single(h.Store.Sessions).Samples.Count);
        Assert.True(await h.Model.RequestCloseAsync());
    }

    [Fact]
    public async Task Changing_interface_discards_a_late_preview_and_closing_waits_for_the_reader()
    {
        var h = new Harness();
        var first = new TaskCompletionSource<WifiSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = Signal();
        var other = new WifiInterfaceInfo(Guid.NewGuid(), "other", WifiConnectionState.Connected);
        var calls = 0;
        var leaked = false;
        h.Wifi.OnRead = async token =>
        {
            if (++calls == 1) return await first.Task;
            return TestData.Wifi(new ManualClock(), other.Id) with { Ssid = "Segunda rede de teste", RssiSource = "WlanQueryInterface:rssi", RssiDbm = -70 };
        };
        h.Model.Signal.PropertyChanged += (_, _) =>
        {
            leaked |= h.Model.Signal.Value == "-30 dBm";
            if (h.Model.Signal.Value == "-70 dBm") second.TrySetResult();
        };
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        h.Model.SelectedInterface = other;
        Assert.Equal("Consultando rede…", h.Model.ConnectedNetworkName);
        Assert.Equal(1, calls);
        first.SetResult(TestData.Wifi(new ManualClock()) with { RssiSource = "WlanQueryInterface:rssi", RssiDbm = -30 });
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(leaked);
        Assert.Equal("-70 dBm", h.Model.Signal.Value);
        Assert.Equal("Segunda rede de teste", h.Model.ConnectedNetworkName);
        Assert.Single(h.Model.Signal.Points);
        Assert.True(await h.Model.RequestCloseAsync());
        Assert.Empty(h.Icmp.Invocations);
    }

    [Fact]
    public async Task Opening_history_stops_monitoring_and_live_command_can_resume_without_packets()
    {
        var h = new Harness();
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        await h.Model.OpenAsync("local.json");
        Assert.Equal("HISTÓRICO", h.Model.Signal.StateLabel);
        Assert.NotNull(h.Model.Result);
        Assert.Equal("HISTÓRICO", h.Model.OperationLabel);
        Assert.Equal("Monitor pausado", h.Model.ConnectedNetworkName);
        await h.Model.MonitorCommand.ExecuteAsync();
        Assert.Equal("MONITORANDO", h.Model.Signal.StateLabel);
        Assert.Null(h.Model.Result);
        Assert.Empty(h.Model.SavedPath);
        Assert.Empty(h.Icmp.Invocations);
        Assert.True(await h.Model.RequestCloseAsync());
    }

    [Fact]
    public async Task Closing_waits_for_an_inflight_preview_and_does_not_publish_its_late_result()
    {
        var h = new Harness(presetTarget: false);
        var pending = new TaskCompletionSource<WifiSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Wifi.OnRead = _ => pending.Task;
        await h.Model.RefreshAsync();
        h.Model.StartMonitoring();
        var close = h.Model.RequestCloseAsync();
        Assert.False(close.IsCompleted);
        pending.SetResult(TestData.Wifi(new ManualClock()) with { RssiSource = "WlanQueryInterface:rssi" });
        Assert.True(await close.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(h.Model.Signal.Points);
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Store.Sessions);
        Assert.False(h.Model.MonitorCommand.CanExecute(null));
    }

    [Fact]
    public async Task Signal_is_published_before_the_session_finishes_and_unavailable_updates_do_not_reuse_it()
    {
        var h = new Harness();
        var observed = Signal();
        var count = 0;
        h.Wifi.OnRead = async token =>
        {
            if (Interlocked.Increment(ref count) == 1)
                return TestData.Wifi(new ManualClock()) with { Ssid = "Rede de coleta simulada", RssiDbm = -60, RssiSource = "WlanQueryInterface:rssi" };
            await Task.Delay(Timeout.Infinite, token);
            return null!;
        };
        h.Model.Signal.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SignalHistoryViewModel.Value) && h.Model.Signal.Value == "-60 dBm")
                observed.TrySetResult();
        };
        await h.Model.RefreshAsync();
        var collection = h.Model.StartAsync();
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(h.Model.IsBusy);
        Assert.Null(h.Model.Result);
        Assert.Equal("-60 dBm", h.Model.Signal.Value);
        Assert.Equal("Rede de coleta simulada", h.Model.ConnectedNetworkName);
        h.Model.Cancel();
        await collection;
        Assert.False(h.Model.IsBusy);
        Assert.True(Assert.Single(h.Store.Sessions).Cancelled);
        Assert.Equal("Indisponível", h.Model.Signal.Value);
        Assert.Equal("Monitor pausado", h.Model.ConnectedNetworkName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("127.1")]
    [InlineData("010.0.0.1")]
    [InlineData("::1")]
    [InlineData("224.0.0.1")]
    public async Task Invalid_target_never_starts_collection_or_storage(string target)
    {
        var h = new Harness();
        await h.Model.RefreshAsync();
        h.Model.TargetText = target;
        Assert.True(h.Model.HasTargetError);
        Assert.False(h.Model.StartCommand.CanExecute(null));
        await h.Model.StartAsync();
        Assert.Equal(0, h.Wifi.Calls);
        Assert.Empty(h.Store.Sessions);
        Assert.NotEmpty(h.Model.Error);
        Assert.False(h.Model.IsBusy);
    }

    [Fact]
    public async Task Correcting_the_target_clears_inline_validation_and_enables_collection_without_sending_packets()
    {
        var h = new Harness(presetTarget: false);
        await h.Model.RefreshAsync();
        Assert.False(h.Model.HasTargetError);
        Assert.False(h.Model.StartCommand.CanExecute(null));
        h.Model.TargetText = "127.0.0.1";
        Assert.True(h.Model.HasTargetError);
        Assert.False(h.Model.StartCommand.CanExecute(null));
        h.Model.TargetText = "192.0.2.1";
        Assert.False(h.Model.HasTargetError);
        Assert.True(h.Model.StartCommand.CanExecute(null));
        Assert.Empty(h.Icmp.Invocations);
        Assert.Empty(h.Store.Sessions);
    }

    [Fact]
    public async Task Two_connected_interfaces_require_explicit_selection()
    {
        var h = new Harness();
        h.Wifi.Items = [new(TestData.InterfaceId, "one", WifiConnectionState.Connected), new(Guid.NewGuid(), "two", WifiConnectionState.Connected)];
        await h.Model.RefreshAsync();
        Assert.Null(h.Model.SelectedInterface);
        Assert.True(h.Model.ShowOperationNotice);
        Assert.False(h.Model.StartCommand.CanExecute(null));
        await h.Model.StartAsync();
        Assert.Equal(0, h.Wifi.Calls);
        h.Model.SelectedInterface = h.Wifi.Items[1];
        await h.Model.StartAsync();
        Assert.Equal(h.Wifi.Items[1].Id, Assert.Single(h.Store.Sessions).InterfaceId);
    }

    [Fact]
    public async Task Completed_collection_is_saved_once_with_a_unique_path_and_an_independent_token()
    {
        var h = new Harness();
        await h.Model.RefreshAsync();
        await h.Model.StartAsync();
        Assert.Equal(60, h.Model.Result!.Summary.Positions);
        Assert.False(h.Model.HasPendingSave);
        Assert.False(h.Store.SawCancelledToken);
        Assert.Empty(h.Model.Error);
        var first = h.Model.SavedPath;
        await h.Model.StartAsync();
        Assert.Equal(2, h.Store.Sessions.Count);
        Assert.NotEqual(first, h.Model.SavedPath);
        Assert.Equal(Path.GetFullPath(h.Model.OutputFolder), Path.GetDirectoryName(h.Model.SavedPath));
    }

    [Fact]
    public async Task Concurrent_start_open_and_refresh_are_blocked_during_acquisition()
    {
        var h = new Harness();
        var readStarted = Signal();
        h.Wifi.OnRead = async token => { readStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return null!; };
        await h.Model.RefreshAsync();
        var collection = h.Model.StartAsync();
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await h.Model.StartAsync();
        await h.Model.OpenAsync("unused.json");
        await h.Model.RefreshAsync();
        Assert.Equal(1, h.Wifi.ListCalls);
        Assert.Equal(0, h.ReadCalls);
        Assert.False(h.Model.StartCommand.CanExecute(null));
        h.Model.Cancel();
        await collection;
        Assert.True(Assert.Single(h.Store.Sessions).Cancelled);
        Assert.False(h.Store.SawCancelledToken);
        Assert.Contains("parcial salva", h.Model.Status);
        Assert.False(h.Model.HasPendingSave);
    }

    [Fact]
    public async Task Closing_during_collection_cancels_waits_and_saves_before_allowing_exit()
    {
        var h = new Harness();
        var readStarted = Signal();
        h.Wifi.OnRead = async token => { readStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return null!; };
        h.Store.Hold = Signal();
        await h.Model.RefreshAsync();
        var collection = h.Model.StartAsync();
        await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var close = h.Model.RequestCloseAsync();
        await h.Store.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(close.IsCompleted);
        Assert.Equal(ProbeOperation.Saving, h.Model.Operation);
        h.Store.Hold.SetResult();
        Assert.True(await close);
        await collection;
        Assert.True(Assert.Single(h.Store.Sessions).Cancelled);
        Assert.False(h.Store.SawCancelledToken);
    }

    [Fact]
    public async Task Save_failure_retains_the_same_session_blocks_replacement_and_allows_retry_in_another_folder()
    {
        var h = new Harness();
        h.Store.Fail = true;
        await h.Model.RefreshAsync();
        await h.Model.StartAsync();
        Assert.True(h.Model.HasPendingSave);
        Assert.NotNull(h.Model.Result);
        Assert.Equal("", h.Model.SavedPath);
        Assert.Contains("memória", h.Model.Status);
        Assert.False(await h.Model.RequestCloseAsync());
        await h.Model.StartAsync();
        await h.Model.OpenAsync("unused.json");
        Assert.Single(h.Store.Sessions);
        Assert.Equal(0, h.ReadCalls);
        h.Model.OutputFolder = Path.Combine(h.Model.OutputFolder, "retry");
        h.Store.Fail = false;
        await h.Model.RetrySaveAsync();
        Assert.Same(h.Store.Sessions[0], h.Store.Sessions[1]);
        Assert.Contains("retry", h.Model.SavedPath);
        Assert.False(h.Model.HasPendingSave);
        Assert.Empty(h.Model.Error);
        Assert.True(await h.Model.RequestCloseAsync());
    }

    [Fact]
    public async Task Closing_during_a_failed_save_keeps_the_window_recoverable()
    {
        var h = new Harness();
        h.Store.Hold = Signal();
        h.Store.Fail = true;
        await h.Model.RefreshAsync();
        var collection = h.Model.StartAsync();
        await h.Store.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var close = h.Model.RequestCloseAsync();
        Assert.False(close.IsCompleted);
        h.Store.Hold.SetResult();
        Assert.False(await close);
        await collection;
        Assert.True(h.Model.CanEditFolder);
        Assert.True(h.Model.RetrySaveCommand.CanExecute(null));
        h.Store.Fail = false;
        await h.Model.RetrySaveAsync();
        Assert.False(h.Model.HasPendingSave);
    }

    [Fact]
    public async Task Discard_requires_explicit_confirmation_and_does_not_delete_saved_files()
    {
        var h = new Harness();
        h.Store.Fail = true;
        await h.Model.RefreshAsync();
        await h.Model.StartAsync();
        await h.Model.DiscardCommand.ExecuteAsync();
        Assert.True(h.Model.HasPendingSave);
        h.Dialogs.AcceptDiscard = true;
        await h.Model.DiscardCommand.ExecuteAsync();
        Assert.False(h.Model.HasPendingSave);
        Assert.Single(h.Store.Sessions);
        Assert.True(await h.Model.RequestCloseAsync());
    }

    [Fact]
    public async Task Opening_saved_JSON_does_not_collect_and_invalid_input_keeps_the_previous_result()
    {
        var h = new Harness();
        await h.Model.OpenAsync("local.json");
        var previous = h.Model.Result;
        Assert.NotNull(previous);
        Assert.Equal(0, h.Wifi.ListCalls);
        Assert.Equal(0, h.Wifi.Calls);
        Assert.Empty(h.Store.Sessions);
        h.ReadFailure = true;
        await h.Model.OpenAsync("invalid.json");
        Assert.Same(previous, h.Model.Result);
        Assert.Contains("JSON inválido", h.Model.Error);
        Assert.False(h.Model.HasPendingSave);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Harness
    {
        public Monitor Wifi { get; }
        public FakeIcmp Icmp { get; }
        public Store Store { get; } = new();
        public Dialogs Dialogs { get; } = new();
        public ProbeViewModel Model { get; }
        public int ReadCalls { get; private set; }
        public bool ReadFailure { get; set; }
        public Harness(bool presetTarget = true, IWifiSignalRefresher? signalRefresher = null)
        {
            var clock = new ManualClock();
            Wifi = new(clock);
            Icmp = new(clock);
            Model = new(Wifi, Icmp, clock, Store, async (_, _) =>
            {
                ReadCalls++;
                if (ReadFailure) throw new InvalidDataException("synthetic private text");
                return await TestData.Session();
            }, Dialogs, Path.Combine(Path.GetTempPath(), "sinalmap-simulated"), signalRefresher: signalRefresher);
            if (presetTarget) Model.TargetText = "192.0.2.1";
        }
    }

    private sealed class Monitor(ManualClock clock) : IWifiMonitor
    {
        public IReadOnlyList<WifiInterfaceInfo> Items { get; set; } = [new(TestData.InterfaceId, "simulated", WifiConnectionState.Connected)];
        public Func<CancellationToken, Task<WifiSnapshot>>? OnRead { get; set; }
        public int Calls { get; private set; }
        public int ListCalls { get; private set; }
        public Task<IReadOnlyList<WifiInterfaceInfo>> ListInterfacesAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult(Items);
        }
        public Task<WifiSnapshot> ReadAsync(Guid id, CancellationToken cancellationToken)
        {
            Calls++;
            return OnRead?.Invoke(cancellationToken) ?? Task.FromResult(TestData.Wifi(clock, id));
        }
    }

    private sealed class Store : ISessionStore
    {
        public List<ProbeSession> Sessions { get; } = [];
        public TaskCompletionSource Started { get; } = Signal();
        public TaskCompletionSource? Hold { get; set; }
        public bool Fail { get; set; }
        public bool SawCancelledToken { get; private set; }
        public async Task SaveAsync(ProbeSession session, string path, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            SawCancelledToken |= cancellationToken.IsCancellationRequested;
            Started.TrySetResult();
            if (Hold is not null) await Hold.Task;
            if (Fail) throw new IOException("synthetic private text");
        }
    }

    private sealed class Dialogs : IProbeDialogs
    {
        public bool AcceptDiscard { get; set; }
        public string? ChooseOutputFolder(string currentFolder) => null;
        public string? ChooseSessionFile() => null;
        public bool ConfirmDiscard() => AcceptDiscard;
    }
}
