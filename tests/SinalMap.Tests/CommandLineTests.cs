using SinalMap.Console.Probe;
using SinalMap.Domain;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void Comparison_accepts_two_paths_with_spaces_as_an_offline_operation()
    {
        var command = CommandLine.Parse(["--compare", "results/ponto A.json", "results/ponto B.json"]);
        Assert.Equal(new ComparisonFiles("results/ponto A.json", "results/ponto B.json"), command.Compare);
        Assert.Null(command.Target);
        Assert.Null(command.Analyze);
    }

    public static TheoryData<string[]> InvalidComparisonArguments => new()
    {
        new[] { "--compare" },
        new[] { "--compare", "a.json" },
        new[] { "--compare", "a.json", "" },
        new[] { "--compare", "--list", "b.json" },
        new[] { "--compare", "a.json", "b.json", "c.json" },
        new[] { "--compare", "a.json", "b.json", "--target", "192.0.2.1" },
        new[] { "--compare", "a.json", "b.json", "--output", "out.json" },
        new[] { "--analyze", "a.json", "--compare", "b.json", "c.json" }
    };

    [Theory]
    [MemberData(nameof(InvalidComparisonArguments))]
    public void Comparison_requires_exactly_two_paths_and_cannot_start_collection(string[] args) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(args));

    [Fact]
    public void Analysis_accepts_a_path_with_spaces_without_a_network_target()
    {
        var command = CommandLine.Parse(["--analyze", "results/coleta local.json"]);
        Assert.Equal("results/coleta local.json", command.Analyze);
        Assert.Null(command.Target);
        Assert.False(command.List);
    }

    public static TheoryData<string[]> InvalidAnalysisArguments => new()
    {
        new[] { "--analyze" },
        new[] { "--analyze", "" },
        new[] { "--analyze", "--list" },
        new[] { "--analyze", "local.json", "--target", "192.0.2.1" },
        new[] { "--analyze", "local.json", "--output", "other.json" }
    };

    [Theory]
    [MemberData(nameof(InvalidAnalysisArguments))]
    public void Analysis_is_exclusive_and_requires_a_nonempty_path(string[] args) =>
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(args));

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
