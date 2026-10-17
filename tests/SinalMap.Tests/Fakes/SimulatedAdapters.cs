using System.Net;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Tests.Fakes;

// Synthetic observations exist only in this test project. The executable has no demo data mode.
internal sealed class ManualClock : IProbeClock
{
    private long ticks;
    public long Frequency => TimeSpan.TicksPerSecond;
    public TimeSpan WallClockAdjustment { get; set; }
    public DateTimeOffset Origin { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public ClockStamp Capture() => new(Origin + TimeSpan.FromTicks(Interlocked.Read(ref ticks)) + WallClockAdjustment,
        Interlocked.Read(ref ticks));
    public TimeSpan ElapsedSince(long startTimestamp) => TimeSpan.FromTicks(Interlocked.Read(ref ticks) - startTimestamp);
    public void Advance(TimeSpan elapsed) => Interlocked.Add(ref ticks, elapsed.Ticks);
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Advance(delay);
        return Task.CompletedTask;
    }
}

internal sealed class FakeWifi(ManualClock clock) : IWifiMonitor
{
    public Func<int, Guid, CancellationToken, Task<WifiSnapshot>>? OnRead { get; set; }
    public int Calls { get; private set; }
    public Task<IReadOnlyList<WifiInterfaceInfo>> ListInterfacesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WifiInterfaceInfo>>([new(TestData.InterfaceId, "simulated adapter", WifiConnectionState.Connected)]);

    public Task<WifiSnapshot> ReadAsync(Guid interfaceId, CancellationToken cancellationToken)
    {
        var sequence = Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        return OnRead?.Invoke(sequence, interfaceId, cancellationToken) ??
            Task.FromResult(TestData.Wifi(clock, interfaceId));
    }
}

internal sealed class FakeIcmp(ManualClock clock) : IIcmpProbe
{
    public Func<int, CancellationToken, Task<IcmpMeasurement>>? OnSend { get; set; }
    public List<ClockStamp> Invocations { get; } = [];
    public Task<IcmpMeasurement> SendAsync(IPAddress target, Guid interfaceId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var sequence = Invocations.Count;
        Invocations.Add(clock.Capture());
        cancellationToken.ThrowIfCancellationRequested();
        return OnSend?.Invoke(sequence, cancellationToken) ?? Task.FromResult(TestData.Icmp(clock));
    }
}

internal static class TestData
{
    public static readonly Guid InterfaceId = Guid.Parse("e10187ed-0000-4000-8000-000000000001");
    public static ProbeOptions Options => new(IPAddress.Parse("192.0.2.1"), InterfaceId);
    public static WifiSnapshot Wifi(IProbeClock clock, Guid? id = null, string bssid = "02:00:00:00:00:01", string ssidHex = "74657374") =>
        new(id ?? InterfaceId, clock.Capture(), clock.Capture(), WifiConnectionState.Connected, "test", ssidHex,
            bssid, -55, 6, 2437000, 1234, "simulated", "simulated", "simulated", []);
    public static IcmpMeasurement Icmp(IProbeClock clock) =>
        new(clock.Capture(), clock.Capture(), IcmpOutcome.Success, true, 0, "Success", "192.0.2.1", null, null);
    public static ProbeSample Sample(int sequence, WifiSnapshot? wifi) =>
        new(sequence, TimeSpan.FromSeconds(sequence), DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(sequence),
            null, 0, SlotDisposition.Dispatched, wifi, null, []);
    public static Task<ProbeSession> Session(ManualClock? clock = null)
    {
        clock ??= new();
        return new ProbeRunner(new FakeWifi(clock), new FakeIcmp(clock), clock).RunAsync(Options, CancellationToken.None);
    }
}
