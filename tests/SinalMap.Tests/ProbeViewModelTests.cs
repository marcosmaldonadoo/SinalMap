using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Presentation;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class ProbeViewModelTests
{
    [Fact]
    public async Task Signal_is_published_before_the_session_finishes_and_unavailable_updates_do_not_reuse_it()
    {
        var h = new Harness();
        var observed = Signal();
        var count = 0;
        h.Wifi.OnRead = async token =>
        {
            if (Interlocked.Increment(ref count) == 1)
                return TestData.Wifi(new ManualClock()) with { RssiDbm = -60, RssiSource = "WlanQueryInterface:rssi" };
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
        h.Model.Cancel();
        await collection;
        Assert.False(h.Model.IsBusy);
        Assert.True(Assert.Single(h.Store.Sessions).Cancelled);
        Assert.Equal("Indisponível", h.Model.Signal.Value);
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
        await h.Model.StartAsync();
        Assert.Equal(0, h.Wifi.Calls);
        Assert.Empty(h.Store.Sessions);
        Assert.NotEmpty(h.Model.Error);
        Assert.False(h.Model.IsBusy);
    }

    [Fact]
    public async Task Two_connected_interfaces_require_explicit_selection()
    {
        var h = new Harness();
        h.Wifi.Items = [new(TestData.InterfaceId, "one", WifiConnectionState.Connected), new(Guid.NewGuid(), "two", WifiConnectionState.Connected)];
        await h.Model.RefreshAsync();
        Assert.Null(h.Model.SelectedInterface);
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
        public Store Store { get; } = new();
        public Dialogs Dialogs { get; } = new();
        public ProbeViewModel Model { get; }
        public int ReadCalls { get; private set; }
        public bool ReadFailure { get; set; }
        public Harness()
        {
            var clock = new ManualClock();
            Wifi = new(clock);
            Model = new(Wifi, new FakeIcmp(clock), clock, Store, async (_, _) =>
            {
                ReadCalls++;
                if (ReadFailure) throw new InvalidDataException("synthetic private text");
                return await TestData.Session();
            }, Dialogs, Path.Combine(Path.GetTempPath(), "sinalmap-simulated"));
            Model.TargetText = "192.0.2.1";
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
