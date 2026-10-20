using System.Runtime.InteropServices;
using System.Threading.Channels;
using SinalMap.Domain;

namespace SinalMap.Application;

public sealed class ProbeRunner(IWifiMonitor wifi, IIcmpProbe icmp, IProbeClock clock)
{
    public async Task<ProbeSession> RunAsync(ProbeOptions options, CancellationToken cancellationToken,
        ChannelWriter<ProbeSample>? liveSamples = null)
    {
        options.Validate();
        var start = clock.Capture();
        var tasks = new Task<ProbeSample>[ProbeOptions.SampleCount];
        var cancelled = false;

        for (var sequence = 0; sequence < tasks.Length; sequence++)
        {
            var planned = TimeSpan.FromSeconds(sequence);
            try
            {
                await DelayUntilAsync(start.MonotonicTicks, planned, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
            }

            if (cancelled)
            {
                tasks[sequence] = Task.FromResult(new ProbeSample(sequence, planned, start.Utc + planned,
                    null, null, SlotDisposition.CancelledBeforeDispatch, null, null,
                    [new(IssueKind.Cancelled, "Schedule", "Cancelled before this planned slot.")]));
                liveSamples?.TryWrite(await tasks[sequence].ConfigureAwait(false));
                continue;
            }

            var dispatched = clock.Capture();
            var elapsed = clock.ElapsedSince(start.MonotonicTicks);
            var lateness = elapsed - planned;
            if (lateness > ProbeOptions.MaximumStartLateness || elapsed >= ProbeOptions.Duration)
            {
                tasks[sequence] = Task.FromResult(new ProbeSample(sequence, planned, start.Utc + planned,
                    null, lateness.TotalMilliseconds, SlotDisposition.SkippedLate, null, null,
                    [new(IssueKind.ScheduleMissed, "Schedule", "Slot missed; no catch-up packet was sent.")]));
                liveSamples?.TryWrite(await tasks[sequence].ConfigureAwait(false));
                continue;
            }

            tasks[sequence] = CaptureAsync(sequence, planned, start.Utc + planned, dispatched,
                lateness.TotalMilliseconds, options, cancellationToken, liveSamples);
        }

        if (!cancelled)
        {
            try
            {
                await DelayUntilAsync(start.MonotonicTicks, ProbeOptions.Duration, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
            }
        }

        var samples = await Task.WhenAll(tasks).ConfigureAwait(false);
        return new(1, Guid.NewGuid(), options.InterfaceId, options.InterfaceDescription, options.Target.ToString(), start, clock.Capture(),
            clock.Frequency, ProbeOptions.Duration, ProbeOptions.Interval, ProbeOptions.Timeout,
            ProbeOptions.MaximumStartLateness, cancelled || cancellationToken.IsCancellationRequested,
            RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, samples,
            BssidChangeDetector.Detect(samples));
    }

    private async Task DelayUntilAsync(long start, TimeSpan offset, CancellationToken cancellationToken)
    {
        // Verificar novamente após despertar antes do prazo; o relógio civil não controla a agenda.
        while (offset - clock.ElapsedSince(start) is var remaining && remaining > TimeSpan.Zero)
            await clock.DelayAsync(remaining, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProbeSample> CaptureAsync(int sequence, TimeSpan planned, DateTimeOffset plannedUtc,
        ClockStamp dispatched, double lateness, ProbeOptions options, CancellationToken cancellationToken,
        ChannelWriter<ProbeSample>? liveSamples)
    {
        // Iniciar ICMP primeiro. Um RPC WLAN síncrono lento não deve atrasar a agenda dos pings.
        var pingTask = CaptureIcmpAsync(options, cancellationToken);
        var wifiTask = CaptureWifiAsync(options.InterfaceId, cancellationToken);
        await Task.WhenAll(pingTask, wifiTask).ConfigureAwait(false);
        var sample = new ProbeSample(sequence, planned, plannedUtc, dispatched, lateness, SlotDisposition.Dispatched,
            await wifiTask.ConfigureAwait(false), await pingTask.ConfigureAwait(false), []);
        // TryWrite não espera a interface gráfica; o canal usado pela janela não executa continuações síncronas.
        liveSamples?.TryWrite(sample);
        return sample;
    }

    private async Task<IcmpMeasurement> CaptureIcmpAsync(ProbeOptions options, CancellationToken cancellationToken)
    {
        var start = clock.Capture();
        try
        {
            return await icmp.SendAsync(options.Target, options.InterfaceId, ProbeOptions.Timeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(start, clock.Capture(), IcmpOutcome.Cancelled, null, null, null, null, null,
                new(IssueKind.Cancelled, "ICMP", "Cancelled; send state is unknown because the adapter threw."));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(start, clock.Capture(), IcmpOutcome.LocalError, null, null, null, null, null,
                new(IssueKind.AdapterError, "ICMP", ex.Message));
        }
    }

    private async Task<WifiSnapshot> CaptureWifiAsync(Guid interfaceId, CancellationToken cancellationToken)
    {
        var start = clock.Capture();
        try
        {
            return await wifi.ReadAsync(interfaceId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return WifiSnapshot.Unavailable(interfaceId, start, clock.Capture(),
                new(IssueKind.Cancelled, "Wi-Fi", "Observation cancelled."));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return WifiSnapshot.Unavailable(interfaceId, start, clock.Capture(),
                new(IssueKind.AdapterError, "Wi-Fi", ex.Message));
        }
    }
}
