using System.Text.Json;
using System.Text.Json.Nodes;
using SinalMap.Console.Probe;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class SessionAnalysisCommandTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"sinalmap-analysis-{Guid.NewGuid():N}");
    private string Input => Path.Combine(directory, "private-name.json");

    [Fact]
    public async Task Analysis_is_read_only_and_omits_identifiers_paths_and_untrusted_text()
    {
        const string secret = "IDENTIFICADOR-NAO-PUBLICAR";
        var session = await TestData.Session();
        var issue = new ProbeIssue(IssueKind.NativeError, secret, secret, 27);
        session = session with
        {
            InterfaceDescription = secret,
            TargetAddress = secret,
            OperatingSystem = secret,
            Samples = [session.Samples[0] with { Issues = [issue], Wifi = session.Samples[0].Wifi! with { Ssid = secret, RssiSource = secret } }]
        };
        await new JsonSessionStore().SaveAsync(session, Input, CancellationToken.None);
        var original = await File.ReadAllBytesAsync(Input);
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await SessionReportCommand.ExecuteAsync(Input, output, error, CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal("", error.ToString());
        foreach (var value in new[] { secret, Input, "private-name", session.SessionId.ToString(), session.InterfaceId.ToString(), "02:00:00:00:00:01", "192.0.2.1" })
            Assert.DoesNotContain(value, output.ToString());
        Assert.Contains("NativeError; código 27: 1", output.ToString());
        Assert.Contains("mínimo 0,00", output.ToString());
        Assert.Equal(original, await File.ReadAllBytesAsync(Input));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task Reader_round_trip_preserves_nullable_measurements_and_session_metadata()
    {
        var session = await TestData.Session();
        session = session with { Samples = [session.Samples[0] with { Wifi = null, Icmp = null }] };
        await new JsonSessionStore().SaveAsync(session, Input, CancellationToken.None);
        var restored = await new JsonSessionReader().ReadAsync(Input, CancellationToken.None);
        Assert.Equal(session.SessionId, restored.SessionId);
        Assert.Equal(session.MonotonicFrequency, restored.MonotonicFrequency);
        Assert.Null(Assert.Single(restored.Samples).Wifi);
        Assert.Null(Assert.Single(restored.Samples).Icmp);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{invalid-json")]
    public async Task Invalid_input_fails_without_outputting_source_contents(string json)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Input, json);
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(1, await SessionReportCommand.ExecuteAsync(Input, output, error, CancellationToken.None));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain(Input, error.ToString());
        Assert.NotEmpty(error.ToString());
    }

    [Theory]
    [InlineData("missing-field")]
    [InlineData("null-position")]
    [InlineData("integer-enum")]
    [InlineData("unknown-enum")]
    public async Task Missing_fields_null_positions_and_invalid_enums_are_rejected(string fault)
    {
        var session = await TestData.Session();
        var root = JsonNode.Parse(JsonSerializer.Serialize(session, JsonSessionStore.CreateOptions()))!;
        switch (fault)
        {
            case "missing-field": root.AsObject().Remove("monotonicFrequency"); break;
            case "null-position": root["samples"]![0] = null; break;
            case "integer-enum": root["samples"]![0]!["disposition"] = 0; break;
            case "unknown-enum": root["samples"]![0]!["disposition"] = "IDENTIFICADOR-NAO-PUBLICAR"; break;
        }
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Input, root.ToJsonString());
        var error = new StringWriter();
        Assert.Equal(1, await SessionReportCommand.ExecuteAsync(Input, new StringWriter(), error, CancellationToken.None));
        Assert.DoesNotContain("IDENTIFICADOR-NAO-PUBLICAR", error.ToString());
    }

    [Fact]
    public async Task Oversized_file_is_rejected_before_interpreting_measurements()
    {
        Directory.CreateDirectory(directory);
        await using (var file = File.Create(Input)) file.SetLength(JsonSessionReader.MaximumFileBytes + 1L);
        await Assert.ThrowsAsync<InvalidDataException>(() => new JsonSessionReader().ReadAsync(Input, CancellationToken.None));
    }

    [Fact]
    public async Task Missing_file_and_cancelled_analysis_have_distinct_exit_codes_and_no_path_disclosure()
    {
        var error = new StringWriter();
        Assert.Equal(1, await SessionReportCommand.ExecuteAsync(Input, new StringWriter(), error, CancellationToken.None));
        Assert.DoesNotContain(Input, error.ToString());
        await new JsonSessionStore().SaveAsync(await TestData.Session(), Input, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(130, await SessionReportCommand.ExecuteAsync(Input, new StringWriter(), new StringWriter(), cancellation.Token));
        Assert.Single(Directory.GetFiles(directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
