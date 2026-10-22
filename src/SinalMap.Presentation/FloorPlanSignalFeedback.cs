using System.Globalization;
using SinalMap.Domain;

namespace SinalMap.Presentation;

public enum SignalVisualBand { Unmeasured, Unavailable, Strong, Good, Moderate, Weak, VeryWeak }

public sealed record PlanPointFeedback(
    Guid PointId,
    string Name,
    PlanPosition Position,
    int Number,
    SignalVisualBand Band,
    string ValueLabel,
    string Detail,
    bool HasMeasurement,
    bool IsPartial,
    bool HasPlacementMismatch);

/// <summary>Projeta faixas visuais do RSSI registrado, sem estimar a cobertura entre os pontos.</summary>
public static class FloorPlanSignalFeedback
{
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    public static IReadOnlyList<PlanPointFeedback> Create(FloorPlanProject? project)
    {
        if (project is null) return [];
        var latestByPoint = new Dictionary<Guid, PlacedMeasurement>();
        foreach (var measurement in project.Measurements)
        {
            // Em horários iguais, o último vínculo do arquivo prevalece; a ordem não substitui o horário.
            if (!latestByPoint.TryGetValue(measurement.PointId, out var previous) || measurement.CompletedAt >= previous.CompletedAt)
                latestByPoint[measurement.PointId] = measurement;
        }
        return project.Points.Select((point, index) => CreatePoint(point, index + 1, project.Router,
            latestByPoint.GetValueOrDefault(point.Id))).ToArray();
    }

    public static SignalVisualBand BandFor(double? value, int count)
    {
        if (count <= 0 || value is not { } rssi || !double.IsFinite(rssi)) return SignalVisualBand.Unavailable;
        return rssi switch
        {
            >= -50 => SignalVisualBand.Strong,
            >= -60 => SignalVisualBand.Good,
            >= -70 => SignalVisualBand.Moderate,
            >= -80 => SignalVisualBand.Weak,
            _ => SignalVisualBand.VeryWeak
        };
    }

    private static PlanPointFeedback CreatePoint(FloorPlanPoint point, int number, PlanPosition? router, PlacedMeasurement? measurement)
    {
        if (measurement is null)
            return new(point.Id, point.Name, point.Position, number, SignalVisualBand.Unmeasured,
                "Não medido", "Nenhuma coleta vinculada a este ponto.", false, false, false);

        var band = BandFor(measurement.RssiMeanDbm, measurement.RssiCount);
        var value = band == SignalVisualBand.Unavailable ? "Sem RSSI" :
            measurement.RssiMeanDbm!.Value.ToString("0.0", Portuguese).Replace('-', '−') + " dBm";
        var kind = measurement.Cancelled ? "coleta parcial" : "coleta concluída";
        var readings = band == SignalVisualBand.Unavailable ? "Nenhuma leitura válida de RSSI nesta coleta" :
            $"Média de {measurement.RssiCount} {(measurement.RssiCount == 1 ? "leitura válida" : "leituras válidas")}";
        var mismatch = measurement.PointPosition != point.Position || measurement.RouterPosition != router;
        var detail = $"{readings} · {kind}";
        if (mismatch)
        {
            band = SignalVisualBand.Unavailable;
            value = "Anterior: " + value;
            detail = "Marcação alterada após a coleta. " + detail;
        }
        return new(point.Id, point.Name, point.Position, number, band, value, detail,
            true, measurement.Cancelled, mismatch);
    }
}
