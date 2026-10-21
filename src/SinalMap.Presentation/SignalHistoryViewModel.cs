using System.Globalization;
using SinalMap.Domain;

namespace SinalMap.Presentation;

public sealed record SignalPoint(double Seconds, int? RssiDbm, bool BreakBefore);

/// <summary>Histórico de RSSI registrado, sem preencher lacunas ou expor identidades de rede.</summary>
public sealed class SignalHistoryViewModel : ObservableObject
{
    private readonly SortedDictionary<int, ProbeSample> samples = [];
    private bool collecting;
    private bool preview;
    private int previewSequence;
    private string refreshNotice = "";
    public string RefreshNotice { get => refreshNotice; private set => Set(ref refreshNotice, value); }
    public void DescribeRefresh(string notice) => RefreshNotice = notice;
    private ProbeSample? latest;
    private IReadOnlyList<SignalPoint> points = [];
    public IReadOnlyList<SignalPoint> Points => points;
    public string TimeAxisLabel => preview ? "Janela recente (s)" : "Tempo planejado (s)";
    public string Heading => preview ? "Sinal da interface selecionada" : collecting ? "Sinal atual" : "Última leitura registrada";
    public string StateLabel => preview ? "MONITORANDO" : collecting ? "COLETANDO" : points.Count > 0 ? "HISTÓRICO" : "AGUARDANDO";
    public string Minimum => DescribeExtreme(minimum: true);
    public string Maximum => DescribeExtreme(minimum: false);
    public string ReadingsLabel => $"{points.Count(p => p.RssiDbm.HasValue)}/{points.Count} " +
        (preview ? "leituras recentes com RSSI · sem gravação" : "posições com RSSI");
    public bool HasIssues => !string.IsNullOrEmpty(LatestIssues);
    public string Value => Rssi(latest) is { } value ? $"{value} dBm" : "Indisponível";
    public string Source => Rssi(latest) is null ? "Sem RSSI válido nesta posição" : latest!.Wifi!.RssiSource switch
    {
        "WlanQueryInterface:rssi" => "Fonte: consulta RSSI do Windows",
        _ => "Fonte: RSSI da consulta em tempo real · enlace único"
    };
    public string Position => latest is null ? "Aguardando a primeira leitura do adaptador" : preview ?
        $"Resposta do Windows às {latest.Wifi!.CompletedAt.Utc.ToLocalTime():HH:mm:ss}" :
        $"Posição {latest.Sequence} · {latest.PlannedOffset.TotalSeconds.ToString("N1", CultureInfo.GetCultureInfo("pt-BR"))} s planejados";
    public string ReadingStatus => Rssi(latest) is not null ?
        (latest!.Wifi!.RssiSource == "WlanQueryInterface:rssi" ? "Consulta RSSI do Windows · compatibilidade" : "RSSI da consulta em tempo real · enlace único") :
        latest is null ? "Atualização aproximadamente a cada segundo, conforme o driver." :
        latest.Wifi?.Issues.FirstOrDefault()?.Kind switch
        {
            IssueKind.Busy => "Consulta anterior ainda em andamento; esta leitura ficou sem sinal.",
            IssueKind.ObservationTimeout => "O adaptador excedeu o prazo da consulta; nenhum valor foi reaproveitado.",
            IssueKind.PermissionDenied => "O Windows negou acesso. Confira o consentimento de localização.",
            IssueKind.NotConnected => "A interface não está conectada ao Wi-Fi.",
            IssueKind.ConnectionChangedDuringRead => "A conexão mudou durante a consulta; a leitura foi descartada.",
            _ => "Sem RSSI válido nesta posição. Consulte os detalhes da medição."
        };
    public string Availability => $"{points.Count(p => p.RssiDbm.HasValue)} valores de RSSI em {points.Count} posições registradas";
    public string Range => points.Where(p => p.RssiDbm.HasValue).Select(p => p.RssiDbm!.Value).ToArray() is { Length: > 0 } values
        ? $"Mínimo {values.Min()} dBm · máximo {values.Max()} dBm" : "Sem valores para mínimo e máximo";
    public string LatestIssues => latest is null ? "" : string.Join(" · ", latest.Issues.Concat(latest.Wifi?.Issues ?? [])
        .DistinctBy(i => (i.Kind, i.NativeCode)).Select(i => $"{i.Kind} (código {i.NativeCode?.ToString() ?? "n/a"})"));

    public void Begin()
    {
        RefreshNotice = "";
        preview = false;
        samples.Clear();
        collecting = true;
        Rebuild();
    }

    public void BeginPreview()
    {
        RefreshNotice = "";
        samples.Clear();
        preview = true;
        previewSequence = 0;
        collecting = false;
        Rebuild();
    }

    public void AcceptPreview(WifiSnapshot snapshot, TimeSpan elapsed)
    {
        if (!preview) return;
        var sample = new ProbeSample(previewSequence++, elapsed, snapshot.StartedAt.Utc,
            snapshot.StartedAt, null, SlotDisposition.Dispatched, snapshot, null, []);
        samples[sample.Sequence] = sample;
        while (samples.Count > 60) samples.Remove(samples.Keys.First());
        while (samples.Count > 1 && samples.Values.First().PlannedOffset < elapsed - TimeSpan.FromSeconds(59))
            samples.Remove(samples.Keys.First());
        Rebuild();
    }

    public void Accept(ProbeSample sample)
    {
        samples[sample.Sequence] = sample;
        Rebuild();
    }

    public void Load(ProbeSession session)
    {
        RefreshNotice = session.ActiveWifiScanning ? "Esta coleta usou varredura Wi-Fi; o RTT pode ter sido afetado." : "";
        preview = false;
        samples.Clear();
        foreach (var sample in session.Samples) samples[sample.Sequence] = sample;
        collecting = false;
        Rebuild();
    }

    public void End() { collecting = false; Notify(nameof(Heading)); Notify(nameof(StateLabel)); }

    private void Rebuild()
    {
        var history = new List<SignalPoint>(samples.Count);
        ProbeSample? previous = null;
        latest = null;
        var origin = preview && samples.Count > 0 ? Math.Max(0, samples.Values.Last().PlannedOffset.TotalSeconds - 59) : 0;
        foreach (var sample in samples.Values)
        {
            if (sample.Disposition != SlotDisposition.CancelledBeforeDispatch) latest = sample;
            history.Add(new(Math.Max(0, sample.PlannedOffset.TotalSeconds - origin), Rssi(sample),
                previous is null || sample.Sequence != previous.Sequence + 1 ||
                Rssi(previous) is null || Rssi(sample) is null || !SameContext(previous.Wifi, sample.Wifi)));
            previous = sample;
        }
        points = history;
        Notify(nameof(Points)); Notify(nameof(Heading)); Notify(nameof(Value)); Notify(nameof(Source));
        Notify(nameof(Position)); Notify(nameof(Availability)); Notify(nameof(Range)); Notify(nameof(LatestIssues));
        Notify(nameof(StateLabel)); Notify(nameof(Minimum)); Notify(nameof(Maximum)); Notify(nameof(ReadingsLabel)); Notify(nameof(HasIssues));
        Notify(nameof(ReadingStatus));
        Notify(nameof(TimeAxisLabel));
    }

    private string DescribeExtreme(bool minimum)
    {
        var values = points.Where(p => p.RssiDbm.HasValue).Select(p => p.RssiDbm!.Value).ToArray();
        return values.Length == 0 ? "—" : $"{(minimum ? values.Min() : values.Max())} dBm";
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
