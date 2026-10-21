using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows.Native;

namespace SinalMap.Infrastructure.Windows;

public sealed class WindowsWifiSignalRefresher : IWifiSignalRefresher
{
    private readonly IWlanApi api;
    private readonly IProbeClock clock;
    private readonly TimeSpan notificationTimeout;
    private readonly TimeSpan callTimeout;
    private readonly SemaphoreSlim gate = new(1, 1);

    public WindowsWifiSignalRefresher(IProbeClock clock)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("A varredura Wi-Fi requer Windows.");
        api = new WlanApi();
        this.clock = clock;
        notificationTimeout = TimeSpan.FromSeconds(4);
        callTimeout = TimeSpan.FromSeconds(5);
    }

    internal WindowsWifiSignalRefresher(IWlanApi api, IProbeClock clock, TimeSpan? notificationTimeout = null, TimeSpan? callTimeout = null)
    {
        this.api = api;
        this.clock = clock;
        this.notificationTimeout = notificationTimeout ?? TimeSpan.FromSeconds(4);
        this.callTimeout = callTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<WifiScanObservation> RefreshAsync(Guid interfaceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = clock.Capture();
        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return new(start, clock.Capture(), false, new(IssueKind.Busy, "WlanScan", "A varredura anterior ainda está em andamento."));
        var worker = Task.Run(async () =>
        {
            try
            {
                using var client = new WlanClient(api);
                await client.WaitForScanAsync(interfaceId, notificationTimeout, cancellationToken).ConfigureAwait(false);
                return new WifiScanObservation(start, clock.Capture(), true, null);
            }
            finally { gate.Release(); }
        }, CancellationToken.None);
        _ = worker.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try { return await worker.WaitAsync(callTimeout).ConfigureAwait(false); }
        catch (TimeoutException)
        {
            return new(start, clock.Capture(), false, new(IssueKind.ObservationTimeout, "WlanScan", "Sem confirmação de conclusão no prazo da varredura."));
        }
        catch (NativeWifiException ex) { return new(start, clock.Capture(), false, ex.Issue); }
    }
}
