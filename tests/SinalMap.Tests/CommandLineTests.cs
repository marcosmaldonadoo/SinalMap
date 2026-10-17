using SinalMap.Console.Probe;
using SinalMap.Domain;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class CommandLineTests
{
    [Theory]
    [InlineData("--target", "example.com")]
    [InlineData("--target", "::1")]
    [InlineData("--target", "127.0.0.1")]
    [InlineData("--target", "0.0.0.0")]
    [InlineData("--target", "255.255.255.255")]
    [InlineData("--target", "224.0.0.1")]
    [InlineData("--target", "1")]
    [InlineData("--target", "0xC0000201")]
    [InlineData("--target", "010.0.0.1")]
    [InlineData("--unknown", "value")]
    public void Rejects_ambiguous_or_unsupported_targets(string flag, string target) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse([flag, target]));

    [Fact]
    public void Parses_target_interface_and_output_explicitly()
    {
        var command = CommandLine.Parse(["--target", "192.0.2.1", "--interface", TestData.InterfaceId.ToString(), "--output", "local.json"]);
        Assert.Equal("192.0.2.1", command.Target!.ToString());
        Assert.Equal(TestData.InterfaceId, command.InterfaceId);
        Assert.Equal("local.json", command.Output);
    }

    [Fact]
    public void Multiple_connected_interfaces_require_an_explicit_selection()
    {
        var interfaces = new[]
        {
            new WifiInterfaceInfo(TestData.InterfaceId, "fake 1", WifiConnectionState.Connected),
            new WifiInterfaceInfo(Guid.NewGuid(), "fake 2", WifiConnectionState.Connected)
        };
        Assert.Throws<ArgumentException>(() => Program.SelectInterface(interfaces, null));
        Assert.Equal(TestData.InterfaceId, Program.SelectInterface(interfaces, TestData.InterfaceId).Id);
    }

    [Fact]
    public void Duplicate_options_and_disconnected_interfaces_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(["--target", "192.0.2.1", "--target", "192.0.2.2"]));
        Assert.Throws<ArgumentException>(() => Program.SelectInterface(
            [new(TestData.InterfaceId, "fake", WifiConnectionState.Disconnected)], TestData.InterfaceId));
    }
}
