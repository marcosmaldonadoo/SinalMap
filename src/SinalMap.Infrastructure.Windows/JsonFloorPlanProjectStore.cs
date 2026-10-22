using System.Text.Json;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Infrastructure.Windows;

/// <summary>Guarda a planta separadamente das coletas; a imagem acompanha o projeto em base64.</summary>
public sealed class JsonFloorPlanProjectStore : IFloorPlanProjectStore
{
    private static JsonSerializerOptions Options() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 24,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };

    public async Task<FloorPlanProject> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length > FloorPlanValidation.MaximumProjectFileBytes)
            throw new InvalidDataException("O projeto de planta ultrapassa o limite de 40 MiB.");
        using var content = new MemoryStream();
        var buffer = new byte[65536];
        int read;
        while ((read = await file.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (content.Length + read > FloorPlanValidation.MaximumProjectFileBytes)
                throw new InvalidDataException("O projeto de planta ultrapassa o limite de 40 MiB.");
            content.Write(buffer, 0, read);
        }
        content.Position = 0;
        var project = await JsonSerializer.DeserializeAsync<FloorPlanProject>(content, Options(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("O arquivo não contém um projeto de planta.");
        FloorPlanValidation.Validate(project);
        return project;
    }

    public async Task SaveAsync(FloorPlanProject project, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FloorPlanValidation.Validate(project);
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.EndsWith(".sinalmap-planta.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Salve o projeto com a terminação .sinalmap-planta.json, separadamente dos JSONs de coleta.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, project, Options(), cancellationToken).ConfigureAwait(false);
                if (stream.Length > FloorPlanValidation.MaximumProjectFileBytes)
                    throw new InvalidDataException("O projeto de planta ultrapassa o limite de 40 MiB.");
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // A substituição só ocorre após a escrita completa, no mesmo diretório do arquivo escolhido.
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
