using System.Globalization;
using SinalMap.Domain;

namespace SinalMap.Presentation;

public sealed record SignalPoint(double Seconds, int? RssiDbm, bool BreakBefore);

/// <summary>Histórico de RSSI registrado, sem preencher lacunas ou expor identidades de rede.</summary>
public sealed class SignalHistoryViewModel : ObservableObject
{
    private readonly SortedDictionary<int, ProbeSample> samples = [];
    private bool collecting;
    private ProbeSample? latest;
    private IReadOnlyList<SignalPoint> points = [];
    public IReadOnlyList<SignalPoint> Points => points;
    public string Heading => collecting ? "Sinal atual" : "Última leitura registrada";
    public string Value => Rssi(latest) is { } value ? $"{value} dBm" : "Indisponível";
    public string Source => Rssi(latest) is null ? "Sem RSSI válido nesta posição" : latest!.Wifi!.RssiSource switch
    {
        "WlanQueryInterface:rssi" => "Fonte: consulta RSSI do Windows",
        _ => "Fonte: consulta de qualidade em tempo real · enlace único"
    };
    public string Position => latest is null ? "Aguardando a primeira leitura" :
        $"Posição {latest.Sequence} · {latest.PlannedOffset.TotalSeconds.ToString("N1", CultureInfo.GetCultureInfo("pt-BR"))} s planejados";
    public string Availability => $"{points.Count(p => p.RssiDbm.HasValue)} valores de RSSI em {points.Count} posições registradas";
    public string Range => points.Where(p => p.RssiDbm.HasValue).Select(p => p.RssiDbm!.Value).ToArray() is { Length: > 0 } values
        ? $"Mínimo {values.Min()} dBm · máximo {values.Max()} dBm" : "Sem valores para mínimo e máximo";
    public string LatestIssues => latest is null ? "" : string.Join(" · ", latest.Issues.Concat(latest.Wifi?.Issues ?? [])
        .DistinctBy(i => (i.Kind, i.NativeCode)).Select(i => $"{i.Kind} (código {i.NativeCode?.ToString() ?? "n/a"})"));

    public void Begin()
    {
        samples.Clear();
        collecting = true;
        Rebuild();
    }

    public void Accept(ProbeSample sample)
    {
        samples[sample.Sequence] = sample;
        Rebuild();
    }

    public void Load(ProbeSession session)
    {
        samples.Clear();
        foreach (var sample in session.Samples) samples[sample.Sequence] = sample;
        collecting = false;
        Rebuild();
    }

    public void End() { collecting = false; Notify(nameof(Heading)); }

    private void Rebuild()
    {
        var history = new List<SignalPoint>(samples.Count);
        ProbeSample? previous = null;
        latest = null;
        foreach (var sample in samples.Values)
        {
            if (sample.Disposition != SlotDisposition.CancelledBeforeDispatch) latest = sample;
            history.Add(new(sample.PlannedOffset.TotalSeconds, Rssi(sample),
                previous is null || sample.Sequence != previous.Sequence + 1 ||
                Rssi(previous) is null || Rssi(sample) is null || !SameContext(previous.Wifi, sample.Wifi)));
            previous = sample;
        }
        points = history;
        Notify(nameof(Points)); Notify(nameof(Heading)); Notify(nameof(Value)); Notify(nameof(Source));
        Notify(nameof(Position)); Notify(nameof(Availability)); Notify(nameof(Range)); Notify(nameof(LatestIssues));
    }

    private static int? Rssi(ProbeSample? sample) => sample is { Disposition: SlotDisposition.Dispatched,
        Wifi: { State: WifiConnectionState.Connected, RssiSource: "WlanQueryInterface:rssi" or
            "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" } wifi } ? wifi.RssiDbm : null;

    private static bool SameContext(WifiSnapshot? a, WifiSnapshot? b) => a is not null && b is not null &&
        a.InterfaceId != Guid.Empty && a.InterfaceId == b.InterfaceId &&
        !string.IsNullOrEmpty(a.SsidHex) && !string.IsNullOrEmpty(a.Bssid) &&
        string.Equals(a.SsidHex, b.SsidHex, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Bssid, b.Bssid, StringComparison.OrdinalIgnoreCase) && a.RssiSource == b.RssiSource;
}
