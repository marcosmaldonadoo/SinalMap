using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Infrastructure.Windows;

public interface IRouteInspector
{
    Task<RouteObservation> InspectAsync(IPAddress target, Guid interfaceId, CancellationToken cancellationToken);
}

public sealed class WindowsRouteInspector(IProbeClock clock) : IRouteInspector
{
    public Task<RouteObservation> InspectAsync(IPAddress target, Guid interfaceId, CancellationToken cancellationToken) =>
        Task.Run(() => Inspect(target, interfaceId), cancellationToken);

    private RouteObservation Inspect(IPAddress target, Guid interfaceId)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows route inspection requires Windows.");
        if (target.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("Only IPv4 is supported in Phase 0.", nameof(target));

        uint? expected = null;
        try
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces().SingleOrDefault(
                n => Guid.TryParse(n.Id, out var id) && id == interfaceId);
            var index = adapter?.GetIPProperties().GetIPv4Properties()?.Index;
            if (index is null)
                return Failed("Selected Wi-Fi interface has no IPv4 interface index.");
            expected = checked((uint)index.Value);
            var socketAddress = new SockaddrIn
            {
                Family = 2, // AF_INET
                Address = BitConverter.ToUInt32(target.GetAddressBytes()) // Preserve network-order bytes in memory.
            };
            var code = GetBestInterfaceEx(in socketAddress, out var best);
            if (code != 0)
                return new(clock.Capture(), expected, null, false,
                    new(IssueKind.RouteUnavailable, "GetBestInterfaceEx", "Windows could not select a route to the target.", code));
            return new(clock.Capture(), expected, best, expected == best,
                expected == best ? null : new(IssueKind.RouteMismatch, "GetBestInterfaceEx",
                    "The selected route does not use the selected Wi-Fi interface; ICMP was not sent."));
        }
        catch (NetworkInformationException ex)
        {
            return Failed(ex.Message, unchecked((uint)ex.ErrorCode));
        }

        RouteObservation Failed(string message, uint? code = null) => new(clock.Capture(), expected, null, false,
            new(IssueKind.RouteUnavailable, "IPv4 route", message, code));
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SockaddrIn
    {
        public ushort Family;
        public ushort Port;
        public uint Address;
        public ulong Padding;
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetBestInterfaceEx(in SockaddrIn destination, out uint bestInterfaceIndex);
}
