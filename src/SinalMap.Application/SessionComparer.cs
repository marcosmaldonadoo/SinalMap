using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SinalMap.Domain;

namespace SinalMap.Application;

public enum ContextComparison { Same, Different, Unknown, VariedWithinSession }

/// <summary>Compara os contextos registrados, sem transportar seus identificadores para a saída.</summary>
public sealed record SessionComparisonContext(
    ContextComparison Interface, ContextComparison Ssid, ContextComparison Bssid,
    ContextComparison Target, ContextComparison Channel, ContextComparison CachedFrequency,
    ContextComparison RssiSource);

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
                wifiB.Where(w => w.RssiDbm.HasValue).Select(RssiSource)));

        // Diferenças são B − A; contexto insuficiente não é substituído por valores presumidos.
        var latency = AllSame(context.Interface, context.Ssid, context.Target)
            ? b.LatencyMilliseconds.Mean - a.LatencyMilliseconds.Mean : null;
        var rssi = AllSame(context.Interface, context.Ssid, context.Bssid, context.Channel,
            context.CachedFrequency, context.RssiSource) ? b.RssiDbm.Mean - a.RssiDbm.Mean : null;
        return new(a, b, context, latency, rssi);
    }

    private static WifiSnapshot[] Connected(ProbeSession session) => session.Samples
        .Where(s => s.Wifi?.State == WifiConnectionState.Connected).Select(s => s.Wifi!).ToArray();

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

    private static string? NormalizeGuid(Guid value) => value == Guid.Empty ? null : value.ToString("D");

    private static string? NormalizeSsid(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length > 64 || hex.Length % 2 != 0) return null;
        try { return Convert.ToHexString(Convert.FromHexString(hex)); }
        catch (FormatException) { return null; }
    }

    private static string? NormalizeBssid(string? text)
    {
        if (!PhysicalAddress.TryParse(text, out var address)) return null;
        var bytes = address.GetAddressBytes();
        return bytes.Length == 6 && bytes.Any(b => b != 0) ? Convert.ToHexString(bytes) : null;
    }

    private static string? NormalizeTarget(string? text) =>
        IPAddress.TryParse(text, out var address) && address.AddressFamily == AddressFamily.InterNetwork && address.ToString() == text
            ? address.ToString() : null;

    private static string? Channel(WifiSnapshot wifi) => wifi.Channel is > 0 &&
        wifi.ChannelSource == "WlanQueryInterface:channel_number" ? wifi.Channel.Value.ToString(CultureInfo.InvariantCulture) : null;

    private static string? Frequency(WifiSnapshot wifi) => wifi.FrequencyKHz is > 0 &&
        wifi.FrequencySource == "WlanGetNetworkBssList:ulChCenterFrequency (cached)" ? wifi.FrequencyKHz.Value.ToString(CultureInfo.InvariantCulture) : null;

    private static string? RssiSource(WifiSnapshot wifi) => wifi.RssiSource is "WlanQueryInterface:rssi" or
        "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" ? wifi.RssiSource : null;
}
