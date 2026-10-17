using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Infrastructure.Windows;

internal sealed record PingTransportReply(IPStatus Status, long RoundTripTimeMilliseconds, string? Address);

internal interface IPingTransport
{
    Task<PingTransportReply> SendAsync(IPAddress target, TimeSpan timeout, CancellationToken cancellationToken);
}

internal sealed class PingTransport : IPingTransport
{
    public async Task<PingTransportReply> SendAsync(IPAddress target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var ping = new Ping(); // A separate instance owns each in-flight request.
        var reply = await ping.SendPingAsync(target, timeout, new byte[32],
            new PingOptions(64, dontFragment: false), cancellationToken).ConfigureAwait(false);
        return new(reply.Status, reply.RoundtripTime, reply.Address?.ToString());
    }
}

public sealed class WindowsIcmpProbe : IIcmpProbe
{
    private readonly IRouteInspector routes;
    private readonly IPingTransport transport;
    private readonly IProbeClock clock;

    public WindowsIcmpProbe(IProbeClock clock) : this(new WindowsRouteInspector(clock), new PingTransport(), clock) { }

    internal WindowsIcmpProbe(IRouteInspector routes, IPingTransport transport, IProbeClock clock)
    {
        this.routes = routes;
        this.transport = transport;
        this.clock = clock;
    }

    public async Task<IcmpMeasurement> SendAsync(IPAddress target, Guid interfaceId, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = clock.Capture();
        var attempted = false;
        RouteObservation? route = null;
        try
        {
            // Bound preflight work as well as ICMP. A delayed route query must not send a late packet.
            var routeTask = routes.InspectAsync(target, interfaceId, cancellationToken);
            _ = routeTask.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            route = await routeTask.WaitAsync(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
            if (!route.IsSelectedInterface)
                return Result(IcmpOutcome.RouteRejected, null, null, null, route.Issue);
            if (clock.ElapsedSince(start.MonotonicTicks) > ProbeOptions.MaximumStartLateness)
                return Result(IcmpOutcome.LocalError, null, null, null,
                    new(IssueKind.ScheduleMissed, "ICMP preflight", "Route preflight missed the send deadline; no ICMP was sent."));

            cancellationToken.ThrowIfCancellationRequested();
            start = clock.Capture(); // Time the SendPingAsync invocation separately from route inspection.
            attempted = true;
            var reply = await transport.SendAsync(target, timeout, cancellationToken).ConfigureAwait(false);
            var outcome = reply.Status switch
            {
                IPStatus.Success => IcmpOutcome.Success,
                IPStatus.TimedOut => IcmpOutcome.TimedOut,
                _ => IcmpOutcome.IcmpError
            };
            return Result(outcome, reply.Status == IPStatus.Success ? reply.RoundTripTimeMilliseconds : null,
                reply.Status.ToString(), reply.Address,
                reply.Status == IPStatus.Success ? null : new(IssueKind.NativeError, "ICMP reply", reply.Status.ToString()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result(IcmpOutcome.Cancelled, null, null, null, new(IssueKind.Cancelled, "ICMP", "Operation cancelled."));
        }
        catch (TimeoutException)
        {
            return Result(IcmpOutcome.LocalError, null, null, null,
                new(IssueKind.RouteUnavailable, "IPv4 route", "Route preflight exceeded 200 ms; ICMP was not sent."));
        }
        catch (Exception ex) when (ex is PingException or SocketException or NetworkInformationException or PlatformNotSupportedException)
        {
            var nativeCode = ex is SocketException socket ? (uint?)socket.NativeErrorCode : null;
            return Result(IcmpOutcome.LocalError, null, null, null,
                new(IssueKind.NativeError, "ICMP", ex.InnerException?.Message ?? ex.Message, nativeCode));
        }

        IcmpMeasurement Result(IcmpOutcome outcome, long? latency, string? status, string? address, ProbeIssue? issue) =>
            new(start, clock.Capture(), outcome, attempted, latency, status, address, route, issue);
    }
}
