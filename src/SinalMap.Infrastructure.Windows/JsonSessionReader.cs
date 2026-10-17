using System.Text.Json;
using System.Text.Json.Serialization;
using SinalMap.Domain;

namespace SinalMap.Infrastructure.Windows;

/// <summary>Leitura local, independente das APIs nativas; não altera o arquivo da coleta.</summary>
public sealed class JsonSessionReader
{
    public const int MaximumFileBytes = 4 * 1024 * 1024;

    public async Task<ProbeSession> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await file.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (content.Length + read > MaximumFileBytes)
                throw new InvalidDataException("O JSON ultrapassa o limite de 4 MiB para análise local.");
            content.Write(buffer, 0, read);
        }
        content.Position = 0;
        var options = JsonSessionStore.CreateOptions();
        options.MaxDepth = 32;
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.Converters.Clear();
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return await JsonSerializer.DeserializeAsync<ProbeSession>(content, options, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("O JSON não contém uma sessão de coleta.");
    }
}
