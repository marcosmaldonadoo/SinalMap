namespace SinalMap.Domain;

/// <summary>Coordenadas normalizadas na imagem, com origem no canto superior esquerdo.</summary>
public sealed record PlanPosition(double X, double Y);

public sealed record FloorPlanPoint(Guid Id, string Name, PlanPosition Position);

/// <summary>Preserva a marcação manual usada na coleta, mesmo que o projeto seja editado depois.</summary>
public sealed record PlacedMeasurement(
    Guid SessionId,
    Guid PointId,
    string PointName,
    PlanPosition PointPosition,
    PlanPosition RouterPosition,
    string SessionPath,
    DateTimeOffset CompletedAt,
    bool Cancelled,
    int RssiCount,
    double? RssiMinimumDbm,
    double? RssiMeanDbm,
    double? RssiMaximumDbm);

/// <summary>Projeto local da planta; seu formato é independente do JSON original de coleta.</summary>
public sealed record FloorPlanProject(
    int SchemaVersion,
    Guid Id,
    string Name,
    string ImageFileName,
    byte[] ImageBytes,
    int ImagePixelWidth,
    int ImagePixelHeight,
    PlanPosition? Router,
    IReadOnlyList<FloorPlanPoint> Points,
    IReadOnlyList<PlacedMeasurement> Measurements);
