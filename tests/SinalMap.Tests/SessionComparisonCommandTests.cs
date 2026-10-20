using SinalMap.Console.Probe;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;

namespace SinalMap.Tests;

public sealed class SessionComparisonCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"sinalmap-compare-{Guid.NewGuid():N}");
    private ComparisonFiles Files => new(Path.Combine(directory, "private-a.json"), Path.Combine(directory, "private-b.json"));

    [Fact]
    public async Task Comparison_includes_context_warning_in_the_affected_session_summary()
    {
        var first = await SessionComparerTests.CreateSession();
        var second = await SessionComparerTests.CreateSession();
        var samples = second.Samples.ToArray();
        samples[0] = samples[0] with { Wifi = samples[0].Wifi! with { Bssid = "02:00:00:00:00:02" } };
        await Save(first, second with { Samples = samples });
        var output = new StringWriter();
        Assert.Equal(0, await SessionComparisonCommand.ExecuteAsync(Files, output, new StringWriter(), CancellationToken.None));
        var text = output.ToString();
        Assert.DoesNotContain("Atenção: o contexto variou", text[..text.IndexOf("Coleta B:", StringComparison.Ordinal)]);
        Assert.Contains("Atenção: o contexto variou", text[text.IndexOf("Coleta B:", StringComparison.Ordinal)..]);
        Assert.Contains("RSSI (B − A): não calculada", text);
        Assert.DoesNotContain("02:00:00:00:00:02", text);
    }

    [Fact]
    public async Task Comparison_preserves_both_files_and_omits_identifiers_and_untrusted_text()
    {
        const string secret = "SEGREDO-NAO-PUBLICAR";
        var first = await SessionComparerTests.CreateSession(-50, 0);
        var second = await SessionComparerTests.CreateSession(-70, 9);
        var ssidHex = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(secret));
        first = SessionComparerTests.MapWifi(first, w => w with { Ssid = secret, SsidHex = ssidHex });
        second = SessionComparerTests.MapWifi(second, w => w with { Ssid = secret, SsidHex = ssidHex });
        var issue = new ProbeIssue(IssueKind.NativeError, secret, secret, 27);
        first = first with
        {
            InterfaceDescription = secret, OperatingSystem = secret, Runtime = secret,
            Samples = first.Samples.Select(s => s with { Issues = [issue] }).ToArray()
        };
        await Save(first, second);
        var bytesA = await File.ReadAllBytesAsync(Files.First);
        var bytesB = await File.ReadAllBytesAsync(Files.Second);
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(0, await SessionComparisonCommand.ExecuteAsync(Files, output, error, CancellationToken.None));
        Assert.Equal("", error.ToString());
        foreach (var value in new[] { secret, ssidHex, Files.First, Files.Second, "private-a", first.SessionId.ToString(), second.SessionId.ToString(), first.InterfaceId.ToString(), "192.0.2.1", "02:00:00:00:00:01" })
            Assert.DoesNotContain(value, output.ToString());
        Assert.Contains("RSSI (B − A): -20,00 dB", output.ToString());
        Assert.Contains("RTT (B − A): +9,00 ms", output.ToString());
        Assert.Equal(bytesA, await File.ReadAllBytesAsync(Files.First));
        Assert.Equal(bytesB, await File.ReadAllBytesAsync(Files.Second));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public async Task A_bad_second_file_produces_no_partial_summary_or_source_snippet()
    {
        await Save(await SessionComparerTests.CreateSession(), await SessionComparerTests.CreateSession());
        await File.WriteAllTextAsync(Files.Second, "{\"SEGREDO-NAO-PUBLICAR\":");
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(1, await SessionComparisonCommand.ExecuteAsync(Files, output, error, CancellationToken.None));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain("SEGREDO", error.ToString());
        Assert.DoesNotContain(Files.Second, error.ToString());
    }

    [Fact]
    public async Task Comparing_the_same_session_is_rejected_even_with_two_file_names()
    {
        var first = await SessionComparerTests.CreateSession();
        await Save(first, first);
        var error = new StringWriter();
        Assert.Equal(1, await SessionComparisonCommand.ExecuteAsync(Files, new StringWriter(), error, CancellationToken.None));
        Assert.Contains("mesma sessão", error.ToString());
    }

    [Fact]
    public async Task Missing_files_and_cancelled_comparison_have_safe_errors_and_distinct_exit_codes()
    {
        var error = new StringWriter();
        Assert.Equal(1, await SessionComparisonCommand.ExecuteAsync(Files, new StringWriter(), error, CancellationToken.None));
        Assert.DoesNotContain(directory, error.ToString());
        await Save(await SessionComparerTests.CreateSession(), await SessionComparerTests.CreateSession());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Equal(130, await SessionComparisonCommand.ExecuteAsync(Files, new StringWriter(), new StringWriter(), cancelled.Token));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    private async Task Save(ProbeSession first, ProbeSession second)
    {
        var store = new JsonSessionStore();
        await store.SaveAsync(first, Files.First, CancellationToken.None);
        await store.SaveAsync(second, Files.Second, CancellationToken.None);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
