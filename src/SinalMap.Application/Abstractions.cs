using System.Net;
using SinalMap.Domain;

namespace SinalMap.Application;

public interface IWifiMonitor
{
    Task<IReadOnlyList<WifiInterfaceInfo>> ListInterfacesAsync(CancellationToken cancellationToken);
    Task<WifiSnapshot> ReadAsync(Guid interfaceId, CancellationToken cancellationToken);
}

public interface IIcmpProbe
{
    Task<IcmpMeasurement> SendAsync(
        IPAddress target, Guid interfaceId, TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IProbeClock
{
    long Frequency { get; }
    ClockStamp Capture();
    TimeSpan ElapsedSince(long startTimestamp);
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public interface ISessionStore
{
    Task SaveAsync(ProbeSession session, string path, CancellationToken cancellationToken);
}
