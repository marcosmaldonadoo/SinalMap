using System.Net;
using System.Net.NetworkInformation;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class IcmpTests
{
    [Theory]
    [InlineData(IPStatus.Success, IcmpOutcome.Success, 0L)]
    [InlineData(IPStatus.TimedOut, IcmpOutcome.TimedOut, null)]
    [InlineData(IPStatus.DestinationHostUnreachable, IcmpOutcome.IcmpError, null)]
    public async Task Only_success_has_latency_and_zero_milliseconds_is_a_valid_result(IPStatus status, IcmpOutcome outcome, long? latency)
    {
        var clock = new ManualClock();
        var transport = new FakeTransport { Reply = new(status, 0, "192.0.2.1") };
        var probe = new WindowsIcmpProbe(new FakeRoute(clock), transport, clock);
        var result = await probe.SendAsync(TestData.Options.Target, TestData.InterfaceId, TimeSpan.FromMilliseconds(800), CancellationToken.None);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(latency, result.RoundTripTimeMilliseconds);
        Assert.True(result.SendAttempted);
        Assert.Equal(status.ToString(), result.ReplyStatus);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task Route_mismatch_blocks_the_transport()
    {
        var clock = new ManualClock();
        var transport = new FakeTransport();
        var probe = new WindowsIcmpProbe(new FakeRoute(clock) { Matches = false }, transport, clock);
        var result = await probe.SendAsync(TestData.Options.Target, TestData.InterfaceId, TimeSpan.FromMilliseconds(800), CancellationToken.None);
        Assert.Equal(IcmpOutcome.RouteRejected, result.Outcome);
        Assert.False(result.SendAttempted);
        Assert.Null(result.RoundTripTimeMilliseconds);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(IssueKind.RouteMismatch, result.Issue!.Kind);
    }

    [Fact]
    public async Task Preflight_delay_does_not_send_a_catch_up_packet()
    {
        var clock = new ManualClock();
        var transport = new FakeTransport();
        var probe = new WindowsIcmpProbe(new FakeRoute(clock) { BeforeReturn = () => clock.Advance(TimeSpan.FromMilliseconds(300)) }, transport, clock);
        var result = await probe.SendAsync(TestData.Options.Target, TestData.InterfaceId, TimeSpan.FromMilliseconds(800), CancellationToken.None);
        Assert.False(result.SendAttempted);
        Assert.Equal(IssueKind.ScheduleMissed, result.Issue!.Kind);
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task Ping_exception_is_not_a_timeout_or_zero_latency()
    {
        var clock = new ManualClock();
        var transport = new FakeTransport { Failure = new PingException("simulated local error") };
        var probe = new WindowsIcmpProbe(new FakeRoute(clock), transport, clock);
        var result = await probe.SendAsync(TestData.Options.Target, TestData.InterfaceId, TimeSpan.FromMilliseconds(800), CancellationToken.None);
        Assert.Equal(IcmpOutcome.LocalError, result.Outcome);
        Assert.True(result.SendAttempted);
        Assert.Null(result.RoundTripTimeMilliseconds);
        Assert.Null(result.ReplyStatus);
    }

    [Fact]
    public async Task Cancellation_before_preflight_does_not_send()
    {
        var clock = new ManualClock();
        var transport = new FakeTransport();
        var probe = new WindowsIcmpProbe(new FakeRoute(clock), transport, clock);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await probe.SendAsync(TestData.Options.Target, TestData.InterfaceId, TimeSpan.FromMilliseconds(800), cancellation.Token);
        Assert.Equal(IcmpOutcome.Cancelled, result.Outcome);
        Assert.False(result.SendAttempted);
        Assert.Equal(0, transport.Calls);
    }

    private sealed class FakeRoute(ManualClock clock) : IRouteInspector
    {
        public bool Matches { get; init; } = true;
        public Action? BeforeReturn { get; init; }
        public Task<RouteObservation> InspectAsync(IPAddress target, Guid interfaceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeforeReturn?.Invoke();
            return Task.FromResult(new RouteObservation(clock.Capture(), 10, Matches ? 10u : 20u, Matches,
                Matches ? null : new(IssueKind.RouteMismatch, "simulated route", "Other interface selected.")));
        }
    }

    private sealed class FakeTransport : IPingTransport
    {
        public int Calls { get; private set; }
        public PingTransportReply Reply { get; init; } = new(IPStatus.Success, 0, "192.0.2.1");
        public Exception? Failure { get; init; }
        public Task<PingTransportReply> SendAsync(IPAddress target, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls++;
            return Failure is null ? Task.FromResult(Reply) : Task.FromException<PingTransportReply>(Failure);
        }
    }
}
