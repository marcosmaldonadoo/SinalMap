using System.Net;
using System.Net.Sockets;

namespace SinalMap.Application;

public sealed record ProbeOptions(IPAddress Target, Guid InterfaceId, string? InterfaceDescription = null)
{
    public const int SampleCount = 60;
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(800);
    public static readonly TimeSpan MaximumStartLateness = TimeSpan.FromMilliseconds(250);

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Target);
        if (InterfaceId == Guid.Empty)
            throw new ArgumentException("A Wi-Fi interface GUID is required.", nameof(InterfaceId));
        ValidateTarget(Target);
    }

    public static void ValidateTarget(IPAddress target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.AddressFamily != AddressFamily.InterNetwork ||
            IPAddress.IsLoopback(target) || target.Equals(IPAddress.Any) ||
            target.Equals(IPAddress.Broadcast) || target.GetAddressBytes()[0] is 0 or >= 224)
            throw new ArgumentException("Phase 0 requires a unicast IPv4 target, excluding loopback and 0.0.0.0.", nameof(Target));
    }
}
