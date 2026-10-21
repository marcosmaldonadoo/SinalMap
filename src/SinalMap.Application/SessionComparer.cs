using SinalMap.Domain;
using static SinalMap.Application.RecordedContextValues;

namespace SinalMap.Application;

public enum ContextComparison { Same, Different, Unknown, VariedWithinSession }

/// <summary>Compara os contextos registrados, sem transportar seus identificadores para a saída.</summary>
public sealed record SessionComparisonContext(
    ContextComparison Interface, ContextComparison Ssid, ContextComparison Bssid,
    ContextComparison Target, ContextComparison Channel, ContextComparison CachedFrequency,
    ContextComparison RssiSource)
{
    public ContextComparison ActiveScanning { get; init; }
}

public sealed record SessionComparison(
    SessionSummary First, SessionSummary Second, SessionComparisonContext Context,
    double? MeanLatencyDifferenceMilliseconds, double? MeanRssiDifferenceDb);

public static class SessionComparer
{
    public static SessionComparison Compare(ProbeSession first, ProbeSession second)
    {
        var a = SessionAnalyzer.Analyze(first);
        var b = SessionAnalyzer.Analyze(second);
        if (first.SessionId == Guid.Empty || second.SessionId == Guid.Empty)
            throw new InvalidDataException("As duas coletas precisam ter identificadores de sessão válidos.");
        if (first.SessionId == second.SessionId)
            throw new InvalidDataException("Os arquivos representam a mesma sessão; selecione duas coletas distintas.");

        var wifiA = Connected(first);
        var wifiB = Connected(second);
        var context = new SessionComparisonContext(
            CompareValues(wifiA.Select(w => NormalizeGuid(w.InterfaceId)).Prepend(NormalizeGuid(first.InterfaceId)),
                wifiB.Select(w => NormalizeGuid(w.InterfaceId)).Prepend(NormalizeGuid(second.InterfaceId))),
            CompareValues(wifiA.Select(w => NormalizeSsid(w.SsidHex)), wifiB.Select(w => NormalizeSsid(w.SsidHex))),
            CompareValues(wifiA.Select(w => NormalizeBssid(w.Bssid)), wifiB.Select(w => NormalizeBssid(w.Bssid))),
            CompareValues([NormalizeTarget(first.TargetAddress)], [NormalizeTarget(second.TargetAddress)]),
            CompareValues(wifiA.Select(Channel), wifiB.Select(Channel)),
            CompareValues(wifiA.Select(Frequency), wifiB.Select(Frequency)),
            CompareValues(wifiA.Where(w => w.RssiDbm.HasValue).Select(RssiSource),
                wifiB.Where(w => w.RssiDbm.HasValue).Select(RssiSource)))
        {
            ActiveScanning = first.ActiveWifiScanning == second.ActiveWifiScanning ? ContextComparison.Same : ContextComparison.Different
        };

        // Diferenças são B − A; contexto insuficiente não é substituído por valores presumidos.
        var latency = AllSame(context.Interface, context.Ssid, context.Target, context.ActiveScanning)
            ? b.LatencyMilliseconds.Mean - a.LatencyMilliseconds.Mean : null;
        var rssi = AllSame(context.Interface, context.Ssid, context.Bssid, context.Channel,
            context.CachedFrequency, context.RssiSource, context.ActiveScanning) ? b.RssiDbm.Mean - a.RssiDbm.Mean : null;
        return new(a, b, context, latency, rssi);
    }

    private static bool AllSame(params ContextComparison[] values) => values.All(v => v == ContextComparison.Same);

    private static ContextComparison CompareValues(IEnumerable<string?> first, IEnumerable<string?> second)
    {
        var a = first.ToArray();
        var b = second.ToArray();
        var distinctA = a.Where(v => v is not null).ToHashSet(StringComparer.Ordinal);
        var distinctB = b.Where(v => v is not null).ToHashSet(StringComparer.Ordinal);
        if (distinctA.Count > 1 || distinctB.Count > 1) return ContextComparison.VariedWithinSession;
        if (a.Length == 0 || b.Length == 0 || a.Any(v => v is null) || b.Any(v => v is null)) return ContextComparison.Unknown;
        return distinctA.SetEquals(distinctB) ? ContextComparison.Same : ContextComparison.Different;
    }
}
