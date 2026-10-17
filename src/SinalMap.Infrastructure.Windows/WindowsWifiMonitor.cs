using System.Text;
using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows.Native;

namespace SinalMap.Infrastructure.Windows;

public sealed class WindowsWifiMonitor : IWifiMonitor
{
    private static readonly TimeSpan ReadBudget = TimeSpan.FromMilliseconds(750);
    private readonly SemaphoreSlim readGate = new(1, 1);
    private readonly IWlanApi api;
    private readonly IProbeClock clock;

    public WindowsWifiMonitor(IProbeClock clock)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Native Wi-Fi collection requires Windows.");
        api = new WlanApi();
        this.clock = clock;
    }

    internal WindowsWifiMonitor(IWlanApi api, IProbeClock clock)
    {
        this.api = api;
        this.clock = clock;
    }

    public async Task<IReadOnlyList<WifiInterfaceInfo>> ListInterfacesAsync(CancellationToken cancellationToken)
    {
        // RPCs WLAN nativos são síncronos; executá-los fora da thread do chamador.
        var task = Task.Run<IReadOnlyList<WifiInterfaceInfo>>(() =>
        {
            using var client = new WlanClient(api);
            return client.EnumerateInterfaces().Select(i =>
                new WifiInterfaceInfo(i.Id, i.GetDescription(), ToState(i.State))).ToArray();
        }, cancellationToken);
        ObserveFault(task);
        return await task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
    }

    public async Task<WifiSnapshot> ReadAsync(Guid interfaceId, CancellationToken cancellationToken)
    {
        var start = clock.Capture();
        if (!await readGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return WifiSnapshot.Unavailable(interfaceId, start, clock.Capture(),
                new(IssueKind.Busy, "Wi-Fi", "A previous native read is still running; no stale sample was reused."));

        // Cada tarefa mantém a propriedade do handle e dos buffers até a chamada nativa retornar.
        // O timeout do chamador não deve fechar um handle cujo RPC ainda esteja em andamento.
        var task = Task.Run(() =>
        {
            try { return ReadNative(interfaceId, start, cancellationToken); }
            finally { readGate.Release(); }
        }, CancellationToken.None);
        ObserveFault(task);
        try
        {
            return await task.WaitAsync(ReadBudget, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return WifiSnapshot.Unavailable(interfaceId, start, clock.Capture(),
                new(IssueKind.ObservationTimeout, "Wi-Fi", "Native read exceeded 750 ms; late data will be discarded."));
        }
    }

    private WifiSnapshot ReadNative(Guid id, ClockStamp start, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var client = new WlanClient(api);
            var connection = client.Query<WlanConnectionAttributes>(id, WlanOpcode.CurrentConnection);
            if (connection.State != WlanInterfaceState.Connected)
                return Disconnected(id, start, new(IssueKind.NotConnected, "CurrentConnection", "The selected interface is not connected."));

            var issues = new List<ProbeIssue>();
            var ssidBytes = connection.Association.Ssid.ToBytes();
            var ssid = DecodeSsid(ssidBytes, issues);
            var bssid = connection.Association.Bssid.ToText();
            if (bssid == "00:00:00:00:00:00")
                throw NativeWifiException.InvalidData("CurrentConnection", "The driver returned an all-zero BSSID while connected.");

            int? rssi = TryRead(() => client.Query<int>(id, WlanOpcode.Rssi), issues);
            uint? channel = TryRead(() => client.Query<uint>(id, WlanOpcode.ChannelNumber), issues);
            if (channel == 0)
            {
                channel = null;
                issues.Add(new(IssueKind.InvalidData, "ChannelNumber", "The driver returned channel 0; no channel was inferred."));
            }

            uint? frequency = null;
            ulong? hostTimestamp = null;
            try
            {
                var bss = client.FindConnectedBss(id, connection);
                if (bss is null)
                    issues.Add(new(IssueKind.BssEntryMissing, "WlanGetNetworkBssList", "No cached entry matched both the connected SSID bytes and BSSID."));
                else if (bss.Value.PhyType == 1 || bss.Value.CenterFrequencyKHz == 0)
                    issues.Add(new(IssueKind.NotSupported, "CenterFrequencyKHz", "Frequency is unavailable or invalid for FHSS; no value was inferred."));
                else
                {
                    frequency = bss.Value.CenterFrequencyKHz;
                    hostTimestamp = bss.Value.HostTimestamp;
                }
            }
            catch (NativeWifiException ex) { issues.Add(ex.Issue); }

            // Consultar a conexão antes e depois para evitar atribuir métricas de um AP ao BSSID de outro.
            var after = client.Query<WlanConnectionAttributes>(id, WlanOpcode.CurrentConnection);
            if (after.State != WlanInterfaceState.Connected ||
                after.Association.Bssid.ToText() != bssid ||
                !after.Association.Ssid.ToBytes().AsSpan().SequenceEqual(ssidBytes))
            {
                issues.Add(new(IssueKind.ConnectionChangedDuringRead, "CurrentConnection", "Connection changed between bracketing reads; metrics were discarded."));
                return new(id, start, clock.Capture(), WifiConnectionState.Unavailable,
                    null, null, null, null, null, null, null, null, null, null, issues);
            }

            return new(id, start, clock.Capture(), WifiConnectionState.Connected, ssid,
                Convert.ToHexString(ssidBytes), bssid, rssi, channel, frequency, hostTimestamp,
                rssi.HasValue ? "WlanQueryInterface:rssi" : null,
                channel.HasValue ? "WlanQueryInterface:channel_number" : null,
                frequency.HasValue ? "WlanGetNetworkBssList:ulChCenterFrequency (cached)" : null, issues);
        }
        catch (NativeWifiException ex)
        {
            return ex.Issue.Kind == IssueKind.NotConnected
                ? Disconnected(id, start, ex.Issue)
                : WifiSnapshot.Unavailable(id, start, clock.Capture(), ex.Issue);
        }
    }

    private WifiSnapshot Disconnected(Guid id, ClockStamp start, ProbeIssue issue) =>
        new(id, start, clock.Capture(), WifiConnectionState.Disconnected, null, null, null,
            null, null, null, null, null, null, null, [issue]);

    private static T? TryRead<T>(Func<T> read, List<ProbeIssue> issues) where T : struct
    {
        try { return read(); }
        catch (NativeWifiException ex) { issues.Add(ex.Issue); return null; }
    }

    private static string? DecodeSsid(byte[] bytes, List<ProbeIssue> issues)
    {
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            issues.Add(new(IssueKind.NotSupported, "SSID encoding", "SSID is not UTF-8; its exact bytes remain available in ssidHex."));
            return null;
        }
    }

    private static WifiConnectionState ToState(WlanInterfaceState state) => state switch
    {
        WlanInterfaceState.Connected => WifiConnectionState.Connected,
        WlanInterfaceState.Disconnected or WlanInterfaceState.Disconnecting => WifiConnectionState.Disconnected,
        _ => WifiConnectionState.Unknown
    };

    private static void ObserveFault(Task task) => _ = task.ContinueWith(
        t => { _ = t.Exception; }, CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
