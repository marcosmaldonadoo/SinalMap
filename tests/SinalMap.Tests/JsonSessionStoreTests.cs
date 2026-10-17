using System.Text.Json;
using SinalMap.Infrastructure.Windows;
using SinalMap.Tests.Fakes;

namespace SinalMap.Tests;

public sealed class JsonSessionStoreTests
{
    [Fact]
    public async Task Round_trip_preserves_all_slots_zero_latency_and_monotonic_metadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sinalmap-test-{Guid.NewGuid():N}");
        try
        {
            var session = await TestData.Session();
            var path = Path.Combine(directory, "session.json");
            await new JsonSessionStore().SaveAsync(session, path, CancellationToken.None);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(60, json.RootElement.GetProperty("samples").GetArrayLength());
            Assert.Equal(0, json.RootElement.GetProperty("samples")[0].GetProperty("icmp").GetProperty("roundTripTimeMilliseconds").GetInt64());
            Assert.Equal(TimeSpan.TicksPerSecond, json.RootElement.GetProperty("monotonicFrequency").GetInt64());
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Null_measurements_stay_null_in_the_JSON()
    {
        var session = await TestData.Session();
        var first = session.Samples[0];
        var modified = session with { Samples = [first with { Wifi = first.Wifi! with { RssiDbm = null, Channel = null, FrequencyKHz = null } }] };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(modified, JsonSessionStore.CreateOptions()));
        var wifi = document.RootElement.GetProperty("samples")[0].GetProperty("wifi");
        Assert.Equal(JsonValueKind.Null, wifi.GetProperty("rssiDbm").ValueKind);
        Assert.Equal(JsonValueKind.Null, wifi.GetProperty("channel").ValueKind);
        Assert.Equal(JsonValueKind.Null, wifi.GetProperty("frequencyKHz").ValueKind);
    }

    [Fact]
    public async Task Existing_result_is_not_overwritten_and_temporary_file_is_cleaned()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sinalmap-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "existing.json");
            await File.WriteAllTextAsync(path, "original evidence");
            var store = new JsonSessionStore();
            var session = await TestData.Session();
            await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(session, path, CancellationToken.None));
            Assert.Equal("original evidence", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Cancelled_write_does_not_leave_a_partial_final_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sinalmap-test-{Guid.NewGuid():N}");
        try
        {
            var path = Path.Combine(directory, "session.json");
            var session = await TestData.Session();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new JsonSessionStore().SaveAsync(session, path, cancellation.Token));
            Assert.False(File.Exists(path));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
