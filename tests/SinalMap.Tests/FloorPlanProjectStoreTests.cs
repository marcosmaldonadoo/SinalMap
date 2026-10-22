using System.Buffers.Binary;
using System.Text.Json;
using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;

namespace SinalMap.Tests;

public sealed class FloorPlanProjectStoreTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j0ioAAAAASUVORK5CYII=");

    [Fact]
    public async Task A_project_cannot_overwrite_a_regular_collection_JSON()
    {
        var directory = TemporaryDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "coleta.json");
            await File.WriteAllTextAsync(path, "coleta original de teste");
            await Assert.ThrowsAsync<InvalidDataException>(() => new JsonFloorPlanProjectStore().SaveAsync(Project(), path, default));
            Assert.Equal("coleta original de teste", await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task Round_trip_embeds_the_image_and_preserves_the_original_measurement_placement()
    {
        var directory = TemporaryDirectory();
        try
        {
            var project = Project();
            var original = Assert.Single(project.Measurements);
            project = project with
            {
                Router = new(.9, .8),
                Points = [project.Points[0] with { Name = "Ponto reposicionado", Position = new(.1, .1) }]
            };
            var path = Path.Combine(directory, "loja.sinalmap-planta.json");
            var store = new JsonFloorPlanProjectStore();
            await store.SaveAsync(project, path, default);
            var restored = await store.ReadAsync(path, default);
            Assert.Equal(project.Id, restored.Id);
            Assert.Equal(project.Name, restored.Name);
            Assert.Equal(project.ImageBytes, restored.ImageBytes);
            Assert.Equal(project.Router, restored.Router);
            Assert.Equal(project.Points, restored.Points);
            Assert.Equal(original, Assert.Single(restored.Measurements));
            Assert.NotEqual(restored.Points[0].Position, restored.Measurements[0].PointPosition);
            Assert.NotEqual(restored.Router, restored.Measurements[0].RouterPosition);
            Assert.Single(Directory.GetFiles(directory));
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal(Png, json.RootElement.GetProperty("imageBytes").GetBytesFromBase64());
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task Explicit_save_replaces_only_the_chosen_project_after_complete_serialization()
    {
        var directory = TemporaryDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "loja.sinalmap-planta.json");
            var other = Path.Combine(directory, "original.json");
            await File.WriteAllTextAsync(other, "evidência anterior");
            var store = new JsonFloorPlanProjectStore();
            var project = Project();
            await store.SaveAsync(project, path, default);
            await store.SaveAsync(project with { Name = "Loja revisada" }, path, default);
            Assert.Equal("Loja revisada", (await store.ReadAsync(path, default)).Name);
            Assert.Equal("evidência anterior", await File.ReadAllTextAsync(other));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task Cancellation_and_invalid_data_preserve_the_previous_file_and_leave_no_temporary()
    {
        var directory = TemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "loja.sinalmap-planta.json");
            var store = new JsonFloorPlanProjectStore();
            var project = Project();
            await store.SaveAsync(project, path, default);
            var previous = await File.ReadAllBytesAsync(path);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(project with { Name = "Cancelado" }, path, cancellation.Token));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(project with { Id = Guid.Empty }, path, default));
            Assert.Equal(previous, await File.ReadAllBytesAsync(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task Failed_replacement_cleans_the_temporary_and_keeps_the_existing_destination()
    {
        var directory = TemporaryDirectory();
        try
        {
            var destination = Path.Combine(directory, "destination.sinalmap-planta.json");
            Directory.CreateDirectory(destination);
            var store = new JsonFloorPlanProjectStore();
            var error = await Record.ExceptionAsync(() => store.SaveAsync(Project(), destination, default));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.True(Directory.Exists(destination));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task Reading_rejects_oversized_files_before_deserialization()
    {
        var directory = TemporaryDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "oversized.sinalmap-planta.json");
            await using (var file = new FileStream(path, FileMode.CreateNew))
                file.SetLength(FloorPlanValidation.MaximumProjectFileBytes + 1L);
            await Assert.ThrowsAsync<InvalidDataException>(() => new JsonFloorPlanProjectStore().ReadAsync(path, default));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { DeleteDirectory(directory); }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1,\"id\":null}")]
    public async Task Reading_rejects_missing_or_invalid_required_data(string content)
    {
        var directory = TemporaryDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "invalid.sinalmap-planta.json");
            await File.WriteAllTextAsync(path, content);
            var error = await Record.ExceptionAsync(() => new JsonFloorPlanProjectStore().ReadAsync(path, default));
            Assert.True(error is JsonException or InvalidDataException);
            Assert.Equal(content, await File.ReadAllTextAsync(path));
        }
        finally { DeleteDirectory(directory); }
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("project-id")]
    [InlineData("name")]
    [InlineData("control-name")]
    [InlineData("long-name")]
    [InlineData("image-name")]
    [InlineData("no-image")]
    [InlineData("large-image")]
    [InlineData("invalid-png")]
    [InlineData("unsupported-format")]
    [InlineData("mismatched-size")]
    [InlineData("large-size")]
    [InlineData("large-pixels")]
    [InlineData("null-points")]
    [InlineData("duplicate-points")]
    [InlineData("empty-point-id")]
    [InlineData("invalid-position")]
    [InlineData("large-points")]
    [InlineData("null-measurements")]
    [InlineData("duplicate-sessions")]
    [InlineData("empty-session-id")]
    [InlineData("missing-point")]
    [InlineData("relative-session")]
    [InlineData("remote-session")]
    [InlineData("url-session")]
    [InlineData("large-measurements")]
    [InlineData("count")]
    [InlineData("missing-metric")]
    [InlineData("non-finite-metric")]
    [InlineData("inverted-metric")]
    [InlineData("unexpected-metric")]
    public void Validation_rejects_inconsistent_projects(string scenario)
    {
        var project = Project();
        var point = project.Points[0];
        var measurement = project.Measurements[0];
        var changed = scenario switch
        {
            "schema" => project with { SchemaVersion = 2 },
            "project-id" => project with { Id = Guid.Empty },
            "name" => project with { Name = " " },
            "control-name" => project with { Name = "Loja\nOutra" },
            "long-name" => project with { Name = new('x', FloorPlanValidation.MaximumNameLength + 1) },
            "image-name" => project with { ImageFileName = "../imagem.png" },
            "no-image" => project with { ImageBytes = [] },
            "large-image" => project with { ImageBytes = new byte[FloorPlanValidation.MaximumImageBytes + 1] },
            "invalid-png" => project with { ImageBytes = [1, 2, 3, 4] },
            "unsupported-format" => project with { ImageFileName = "imagem.gif" },
            "mismatched-size" => project with { ImagePixelWidth = 2 },
            "large-size" => project with { ImagePixelWidth = FloorPlanValidation.MaximumImageDimension + 1 },
            "large-pixels" => project with { ImagePixelWidth = 8000, ImagePixelHeight = 8000 },
            "null-points" => project with { Points = null! },
            "duplicate-points" => project with { Points = [point, point] },
            "empty-point-id" => project with { Points = [point with { Id = Guid.Empty }] },
            "invalid-position" => project with { Router = new(double.NaN, .5) },
            "large-points" => project with { Points = Enumerable.Range(0, FloorPlanValidation.MaximumPoints + 1).Select(_ => point with { Id = Guid.NewGuid() }).ToArray() },
            "null-measurements" => project with { Measurements = null! },
            "duplicate-sessions" => project with { Measurements = [measurement, measurement] },
            "empty-session-id" => project with { Measurements = [measurement with { SessionId = Guid.Empty }] },
            "missing-point" => project with { Measurements = [measurement with { PointId = Guid.NewGuid() }] },
            "relative-session" => project with { Measurements = [measurement with { SessionPath = "results/coleta.json" }] },
            "remote-session" => project with { Measurements = [measurement with { SessionPath = @"\\server\share\coleta.json" }] },
            "url-session" => project with { Measurements = [measurement with { SessionPath = "https://example.test/coleta.json" }] },
            "large-measurements" => project with { Measurements = Enumerable.Range(0, FloorPlanValidation.MaximumMeasurements + 1).Select(_ => measurement with { SessionId = Guid.NewGuid() }).ToArray() },
            "count" => project with { Measurements = [measurement with { RssiCount = 61 }] },
            "missing-metric" => project with { Measurements = [measurement with { RssiMeanDbm = null }] },
            "non-finite-metric" => project with { Measurements = [measurement with { RssiMinimumDbm = double.NegativeInfinity }] },
            "inverted-metric" => project with { Measurements = [measurement with { RssiMeanDbm = -1 }] },
            "unexpected-metric" => project with { Measurements = [measurement with { RssiCount = 0 }] },
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<InvalidDataException>(() => FloorPlanValidation.Validate(changed));
    }

    [Theory]
    [InlineData(-.01, .5)]
    [InlineData(.5, 1.01)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Positions_outside_the_image_or_non_finite_are_not_clamped(double x, double y) =>
        Assert.Throws<InvalidDataException>(() => FloorPlanValidation.ValidatePosition(new(x, y)));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(.25, .75)]
    public void Positions_include_the_image_edges(double x, double y) => FloorPlanValidation.ValidatePosition(new(x, y));

    [Fact]
    public void Missing_RSSI_remains_null_and_session_files_do_not_have_to_exist()
    {
        var project = Project();
        project = project with { Measurements = [project.Measurements[0] with
        {
            SessionPath = @"C:\pasta-inexistente-simulada\coleta.json", RssiCount = 0,
            RssiMinimumDbm = null, RssiMeanDbm = null, RssiMaximumDbm = null
        }] };
        FloorPlanValidation.Validate(project);
    }

    [Fact]
    public void JPEG_header_dimensions_are_checked_without_decoding_pixels()
    {
        var project = Project();
        // Cabeçalho mínimo simulado: SOI, APP e SOF com dimensões 3 × 2; não é uma fotografia decodificável.
        byte[] header = [0xff, 0xd8, 0xff, 0xe0, 0, 2, 0xff, 0xc0, 0, 8, 8, 0, 2, 0, 3, 1];
        project = project with { ImageFileName = "imagem.jpeg", ImageBytes = header, ImagePixelWidth = 3, ImagePixelHeight = 2 };
        FloorPlanValidation.Validate(project);
        Assert.Throws<InvalidDataException>(() => FloorPlanValidation.Validate(project with { ImagePixelHeight = 3 }));
        Assert.Throws<InvalidDataException>(() => FloorPlanValidation.Validate(project with { ImageBytes = header[..^1] }));
    }

    [Fact]
    public void PNG_header_cannot_disguise_dimensions_larger_than_the_declared_bitmap()
    {
        var project = Project();
        var bytes = project.ImageBytes.ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), FloorPlanValidation.MaximumImageDimension + 1);
        Assert.Throws<InvalidDataException>(() => FloorPlanValidation.Validate(project with { ImageBytes = bytes }));
    }

    private static FloorPlanProject Project()
    {
        var point = new FloorPlanPoint(Guid.NewGuid(), "Ponto de teste", new(.25, .75));
        var router = new PlanPosition(.5, .5);
        var measurement = new PlacedMeasurement(Guid.NewGuid(), point.Id, point.Name, point.Position, router,
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "coleta-ficticia.json")), DateTimeOffset.Parse("2026-10-21T12:00:00Z"),
            true, 2, -75, -70, -65);
        return new(FloorPlanValidation.SchemaVersion, Guid.NewGuid(), "Loja de teste", "planta.png", Png.ToArray(), 1, 1,
            router, [point], [measurement]);
    }

    private static string TemporaryDirectory() => Path.Combine(Path.GetTempPath(), $"sinalmap-plan-test-{Guid.NewGuid():N}");
    private static void DeleteDirectory(string path) { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
}
