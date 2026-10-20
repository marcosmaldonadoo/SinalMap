using SinalMap.Domain;
using static SinalMap.Application.RecordedContextValues;

namespace SinalMap.Application;

public enum RecordedContextState { SingleValue, Varied, Unknown }

/// <summary>Descreve somente o contexto registrado, sem expor identificadores ou afirmar estabilidade física.</summary>
public sealed record SessionContextSummary(
    RecordedContextState Interface, RecordedContextState Ssid, RecordedContextState Bssid,
    RecordedContextState Target, RecordedContextState Channel, RecordedContextState CachedFrequency,
    RecordedContextState RssiSource)
{
    public bool HasVariation => States.Any(s => s == RecordedContextState.Varied);
    public bool HasUnknown => States.Any(s => s == RecordedContextState.Unknown);

    private RecordedContextState[] States => [Interface, Ssid, Bssid, Target, Channel, CachedFrequency, RssiSource];

    internal static SessionContextSummary FromSession(ProbeSession session)
    {
        var wifi = Connected(session);
        return new(
            Describe(wifi.Select(w => NormalizeGuid(w.InterfaceId)).Prepend(NormalizeGuid(session.InterfaceId))),
            Describe(wifi.Select(w => NormalizeSsid(w.SsidHex))),
            Describe(wifi.Select(w => NormalizeBssid(w.Bssid))),
            Describe([NormalizeTarget(session.TargetAddress)]),
            Describe(wifi.Select(RecordedContextValues.Channel)),
            Describe(wifi.Select(Frequency)),
            Describe(wifi.Where(w => w.RssiDbm.HasValue).Select(RecordedContextValues.RssiSource)));
    }

    private static RecordedContextState Describe(IEnumerable<string?> values)
    {
        var items = values.ToArray();
        // Valores reconhecidos distintos demonstram variação mesmo quando há lacunas adicionais.
        if (items.Where(v => v is not null).Distinct(StringComparer.Ordinal).Skip(1).Any())
            return RecordedContextState.Varied;
        return items.Length == 0 || items.Any(v => v is null)
            ? RecordedContextState.Unknown : RecordedContextState.SingleValue;
    }
}
