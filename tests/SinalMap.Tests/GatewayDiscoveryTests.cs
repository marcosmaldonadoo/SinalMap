using System.Net;
using SinalMap.Infrastructure.Windows;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class GatewayDiscoveryTests
{
    [Theory]
    [InlineData("::1", null)]
    [InlineData("0.0.0.0", null)]
    [InlineData("127.0.0.1", null)]
    [InlineData("224.0.0.1", null)]
    [InlineData("255.255.255.255", null)]
    [InlineData("192.0.2.1", "192.0.2.1")]
    public void Only_unicast_IPv4_is_suggested(string address, string? expected) =>
        Assert.Equal(expected, WindowsGatewayDiscovery.Select([IPAddress.Parse(address)]));

    [Fact]
    public void Multiple_distinct_gateways_are_not_chosen_arbitrarily()
    {
        Assert.Null(WindowsGatewayDiscovery.Select([IPAddress.Parse("192.0.2.1"), IPAddress.Parse("198.51.100.1")]));
        Assert.Equal("192.0.2.1", WindowsGatewayDiscovery.Select([IPAddress.IPv6Any, IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.1")]));
        Assert.Null(WindowsGatewayDiscovery.Select([]));
    }

    [Fact]
    public async Task Gateway_is_joined_by_interface_GUID_without_borrowing_another_adapters_address()
    {
        var api = new FakeWlanApi();
        var gateways = new Dictionary<Guid, string> { [Guid.NewGuid()] = "198.51.100.1" };
        var monitor = new WindowsWifiMonitor(api, new ManualClock(), () => gateways);
        Assert.Null(Assert.Single(await monitor.ListInterfacesAsync(CancellationToken.None)).Ipv4Gateway);
        gateways[TestData.InterfaceId] = "192.0.2.1";
        Assert.Equal("192.0.2.1", Assert.Single(await monitor.ListInterfacesAsync(CancellationToken.None)).Ipv4Gateway);
    }
}
