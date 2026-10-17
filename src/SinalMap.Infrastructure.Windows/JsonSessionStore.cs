using System.Text.Json;
using System.Text.Json.Serialization;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Infrastructure.Windows;

public sealed class JsonSessionStore : ISessionStore
{
    internal static JsonSerializerOptions CreateOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
        // Nulls are intentionally serialized: unavailable is different from zero.
    };

    public async Task SaveAsync(ProbeSession session, string path, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, session, CreateOptions(), cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            // Never silently overwrite a previous measurement.
            File.Move(temporary, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
