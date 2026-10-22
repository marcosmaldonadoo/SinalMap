using System.Buffers.Binary;
using SinalMap.Domain;

namespace SinalMap.Application;

/// <summary>Valida o projeto e os cabeçalhos da imagem, sem inferir medições ou decodificar o bitmap.</summary>
public static class FloorPlanValidation
{
    public const int SchemaVersion = 1;
    public const int MaximumImageBytes = 20 * 1024 * 1024;
    public const int MaximumProjectFileBytes = 40 * 1024 * 1024;
    public const int MaximumImageDimension = 12000;
    public const long MaximumImagePixels = 40_000_000;
    public const int MaximumPoints = 500;
    public const int MaximumMeasurements = 5000;
    public const int MaximumNameLength = 120;

    public static void Validate(FloorPlanProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        Require(project.SchemaVersion == SchemaVersion, "Versão do projeto de planta não suportada.");
        Require(project.Id != Guid.Empty, "O projeto precisa de um identificador válido.");
        ValidateName(project.Name, "O nome do projeto");
        Require(!string.IsNullOrWhiteSpace(project.ImageFileName) && project.ImageFileName.Length <= 255 &&
            project.ImageFileName.IndexOfAny(['/', '\\', ':']) < 0 && !project.ImageFileName.Any(char.IsControl),
            "O nome da imagem deve ser um nome de arquivo, sem diretórios.");
        Require(project.ImageBytes is { Length: > 0 and <= MaximumImageBytes }, "A imagem deve ter até 20 MiB.");
        Require(project.ImagePixelWidth is > 0 and <= MaximumImageDimension &&
            project.ImagePixelHeight is > 0 and <= MaximumImageDimension &&
            (long)project.ImagePixelWidth * project.ImagePixelHeight <= MaximumImagePixels,
            "A imagem deve ter até 12000 pixels por dimensão e 40 milhões de pixels no total.");
        var (width, height) = ImageDimensions(project.ImageBytes, project.ImageFileName);
        Require(width == project.ImagePixelWidth && height == project.ImagePixelHeight,
            "As dimensões da imagem não correspondem ao cabeçalho do arquivo.");
        if (project.Router is not null) ValidatePosition(project.Router);
        Require(project.Points is not null && project.Points.Count <= MaximumPoints,
            "O projeto deve conter no máximo 500 pontos.");
        Require(project.Measurements is not null && project.Measurements.Count <= MaximumMeasurements,
            "O projeto deve conter no máximo 5000 vínculos de coleta.");
        var pointIds = new HashSet<Guid>();
        foreach (var point in project.Points)
        {
            Require(point is not null && point.Id != Guid.Empty && pointIds.Add(point.Id),
                "Os pontos precisam de identificadores válidos e únicos.");
            ValidateName(point.Name, "O nome do ponto");
            ValidatePosition(point.Position);
        }
        var sessionIds = new HashSet<Guid>();
        foreach (var measurement in project.Measurements)
        {
            Require(measurement is not null && measurement.SessionId != Guid.Empty && sessionIds.Add(measurement.SessionId),
                "Os vínculos precisam de identificadores de sessão válidos e únicos.");
            Require(pointIds.Contains(measurement.PointId), "Uma coleta referencia um ponto inexistente.");
            ValidateName(measurement.PointName, "O nome do ponto da coleta");
            ValidatePosition(measurement.PointPosition);
            ValidatePosition(measurement.RouterPosition);
            Require(IsLocalAbsolutePath(measurement.SessionPath), "O caminho da coleta deve ser absoluto e local.");
            Require(measurement.CompletedAt != default, "A coleta precisa de um horário de conclusão.");
            Require(measurement.RssiCount is >= 0 and <= ProbeOptions.SampleCount,
                "A contagem de RSSI deve estar entre zero e 60.");
            if (measurement.RssiCount == 0)
            {
                Require(measurement.RssiMinimumDbm is null && measurement.RssiMeanDbm is null && measurement.RssiMaximumDbm is null,
                    "Uma coleta sem RSSI deve manter as estatísticas nulas.");
            }
            else
            {
                Require(measurement.RssiMinimumDbm is { } minimum && double.IsFinite(minimum) &&
                    measurement.RssiMeanDbm is { } mean && double.IsFinite(mean) &&
                    measurement.RssiMaximumDbm is { } maximum && double.IsFinite(maximum) && minimum <= mean && mean <= maximum,
                    "As estatísticas de RSSI devem ser finitas e respeitar mínimo ≤ média ≤ máximo.");
            }
        }
    }

    public static void ValidatePosition(PlanPosition position)
    {
        Require(position is not null && double.IsFinite(position.X) && double.IsFinite(position.Y) &&
            position.X is >= 0 and <= 1 && position.Y is >= 0 and <= 1,
            "A posição deve estar dentro da imagem, entre zero e um em cada eixo.");
    }

    private static void ValidateName(string name, string label) => Require(
        !string.IsNullOrWhiteSpace(name) && name.Length <= MaximumNameLength && !name.Any(char.IsControl),
        $"{label} deve ter entre 1 e {MaximumNameLength} caracteres, sem caracteres de controle.");

    private static bool IsLocalAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || path.Any(char.IsControl) ||
            path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith("\\\\", StringComparison.Ordinal)) return false;
        // Preserva referências Windows ao inspecionar um projeto em outro sistema operacional.
        var windowsDrive = path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '/' or '\\';
        return windowsDrive || path.StartsWith('/');
    }

    private static (int Width, int Height) ImageDimensions(byte[] bytes, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        ReadOnlySpan<byte> image = bytes;
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            Require(image.Length >= 33 && image[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
                BinaryPrimitives.ReadUInt32BigEndian(image.Slice(8, 4)) == 13 && image.Slice(12, 4).SequenceEqual("IHDR"u8),
                "O cabeçalho PNG é inválido.");
            var width = BinaryPrimitives.ReadUInt32BigEndian(image.Slice(16, 4));
            var height = BinaryPrimitives.ReadUInt32BigEndian(image.Slice(20, 4));
            Require(width <= MaximumImageDimension && height <= MaximumImageDimension,
                "As dimensões do cabeçalho PNG ultrapassam o limite.");
            return ((int)width, (int)height);
        }
        Require(extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase),
            "Use uma imagem PNG ou JPEG.");
        Require(image.Length >= 4 && image[0] == 0xff && image[1] == 0xd8, "O cabeçalho JPEG é inválido.");
        var offset = 2;
        while (offset < image.Length)
        {
            Require(image[offset++] == 0xff, "A sequência de marcadores JPEG é inválida.");
            while (offset < image.Length && image[offset] == 0xff) offset++;
            Require(offset < image.Length, "O cabeçalho JPEG está incompleto.");
            var marker = image[offset++];
            Require(marker is not (0 or 0xd9 or 0xda), "O JPEG não informa dimensões antes dos dados da imagem.");
            if (marker is 0x01 or >= 0xd0 and <= 0xd8) continue;
            Require(offset + 2 <= image.Length, "O cabeçalho JPEG está incompleto.");
            var length = BinaryPrimitives.ReadUInt16BigEndian(image.Slice(offset, 2));
            Require(length >= 2 && offset + length <= image.Length, "O segmento JPEG está incompleto.");
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or 0xc5 or 0xc6 or 0xc7 or 0xc9 or 0xca or 0xcb or 0xcd or 0xce or 0xcf)
            {
                Require(length >= 8, "O segmento de dimensões JPEG é inválido.");
                return (BinaryPrimitives.ReadUInt16BigEndian(image.Slice(offset + 5, 2)),
                    BinaryPrimitives.ReadUInt16BigEndian(image.Slice(offset + 3, 2)));
            }
            offset += length;
        }
        throw new InvalidDataException("O JPEG não contém um cabeçalho de dimensões suportado.");
    }

    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }
}
