using System.Globalization;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Presentation;

public sealed record ContextRow(string Field, string State);
public sealed record OccurrenceRow(string Kind, string Code, int Positions);
public sealed record SampleRow(int Position, string Disposition, string Wifi, string Icmp, string Rtt,
    string Rssi, string Channel, string CachedFrequency, string RssiSource, string Occurrences);

/// <summary>Projeção local sem SSID, BSSID, GUIDs, IPs ou mensagens livres da sessão.</summary>
public sealed class SessionResultViewModel
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");
    public SessionSummary Summary { get; }
    public IReadOnlyList<SampleRow> Samples { get; }
    public IReadOnlyList<ContextRow> Context { get; }
    public IReadOnlyList<OccurrenceRow> Occurrences { get; }
    public string Title => Summary.Cancelled ? "Sessão parcial · cancelada" : "Sessão carregada";
    public string Positions => $"{Summary.Dispatched}/{Summary.Positions}";
    public string Icmp => $"{Summary.IcmpOutcomes.Where(o => o.Outcome == IcmpOutcome.Success).Sum(o => o.Count)} respostas / {Summary.IcmpAttempts} chamadas";
    public string Rtt => Metric(Summary.LatencyMilliseconds, "ms");
    public string Rssi => Metric(Summary.RssiDbm, "dBm");
    public string Availability => $"Wi-Fi conectado: {Summary.ConnectedWifi}/{Summary.Positions} · RSSI: {Summary.RssiDbm.Count}/{Summary.Positions} · canal: {Summary.Channels}/{Summary.Positions} · frequência: {Summary.Frequencies}/{Summary.Positions} ({Summary.CachedFrequencies} em cache BSS)";
    public string Schedule => $"Duração: {Summary.DurationSeconds.ToString("N2", Culture)} s · descartadas por atraso: {Summary.SkippedLate} · canceladas antes do despacho: {Summary.CancelledBeforeDispatch} · tentativas ICMP desconhecidas: {Summary.UnknownIcmpAttempts}";
    public string Sources => $"RSSI: consulta tradicional {Summary.PrimaryRssi} · tempo real {Summary.AlternativeRssi} · outra/não identificada {Summary.OtherRssiSource} · mudanças de BSSID registradas: {Summary.BssidChanges}";
    public string ContextNotice => (Summary.Context.HasVariation
        ? "O contexto variou nos registros. As médias globais podem reunir redes, APs ou condições diferentes. " : "") +
        (Summary.Context.HasUnknown ? "Há contexto indeterminado por dados ausentes, inválidos ou fontes não reconhecidas. " : "") +
        "As estatísticas são globais; um único valor não comprova conexão contínua ou ausência de mudanças entre leituras.";
    public string OccurrenceNotice => Occurrences.Count == 0 ? "Nenhuma ocorrência registrada." : "Posições por tipo/código; categorias podem se sobrepor.";
    public string ScanNotice { get; }

    public SessionResultViewModel(ProbeSession session)
    {
        Summary = SessionAnalyzer.Analyze(session);
        ScanNotice = session.ActiveWifiScanning ?
            $"Varredura Wi-Fi ativa: {session.WifiScans?.Count(s => s.Completed) ?? 0}/{session.WifiScans?.Count ?? 0} confirmações registradas. O RTT pode ter sido aumentado pelas varreduras." :
            "Não há registro de varredura Wi-Fi ativa feita por esta coleta.";
        var context = Summary.Context;
        Context = [Row("Interface registrada", context.Interface), Row("SSID em bytes", context.Ssid),
            Row("AP (BSSID)", context.Bssid), Row("Destino ICMP", context.Target), Row("Canal", context.Channel),
            Row("Frequência em cache BSS", context.CachedFrequency), Row("Fonte RSSI", context.RssiSource)];
        Occurrences = Summary.Issues.Select(i => new OccurrenceRow(i.Kind.ToString(), i.NativeCode?.ToString(Culture) ?? "n/a", i.AffectedPositions)).ToArray();
        Samples = session.Samples.OrderBy(s => s.Sequence).Select(s => new SampleRow(
            s.Sequence, s.Disposition switch
            {
                SlotDisposition.Dispatched => "Despachada",
                SlotDisposition.SkippedLate => "Descartada por atraso",
                _ => "Cancelada antes do despacho"
            },
            s.Wifi?.State switch
            {
                WifiConnectionState.Connected => "Conectado", WifiConnectionState.Disconnected => "Desconectado",
                WifiConnectionState.Unavailable => "Indisponível", _ => "Sem leitura válida"
            },
            s.Icmp?.Outcome switch
            {
                IcmpOutcome.Success => "Sucesso", IcmpOutcome.TimedOut => "Timeout",
                IcmpOutcome.IcmpError => "Erro ICMP", IcmpOutcome.LocalError => "Erro local",
                IcmpOutcome.RouteRejected => "Rota rejeitada", IcmpOutcome.Cancelled => "Cancelado", _ => "Sem registro"
            },
            s.Icmp?.Outcome == IcmpOutcome.Success ? Value(s.Icmp.RoundTripTimeMilliseconds, "ms") : "Indisponível",
            Value(s.Wifi?.RssiDbm, "dBm"), Value(s.Wifi?.Channel, ""),
            s.Wifi?.FrequencyKHz is null ? "Indisponível" : Value(s.Wifi.FrequencyKHz, "kHz") +
                (s.Wifi.FrequencySource == "WlanGetNetworkBssList:ulChCenterFrequency (cached)" ? " · cache BSS" : " · fonte não reconhecida"),
            s.Wifi?.RssiDbm is null ? "Ausente" : s.Wifi.RssiSource switch
            {
                "WlanQueryInterface:rssi" => "Tradicional",
                "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi" => "Tempo real",
                _ => "Ausente/não reconhecida"
            },
            string.Join("; ", s.Issues.Concat(s.Wifi?.Issues ?? [])
                .Concat(s.Icmp?.Issue is { } ping ? [ping] : Array.Empty<ProbeIssue>())
                .Concat(s.Icmp?.Route?.Issue is { } route ? [route] : Array.Empty<ProbeIssue>())
                .DistinctBy(i => (i.Kind, i.NativeCode))
                .Select(i => $"{i.Kind} ({i.NativeCode?.ToString(Culture) ?? "n/a"})")))).ToArray();
    }

    private static ContextRow Row(string label, RecordedContextState state) => new(label, state switch
    {
        RecordedContextState.SingleValue => "Um único valor nos registros", RecordedContextState.Varied => "Variou nos registros", _ => "Indeterminado"
    });

    private static string Metric(NumericSummary metric, string unit) => metric.Count == 0 ? "Sem valores registrados" :
        $"{metric.Mean!.Value.ToString("N2", Culture)} {unit} · média\n{metric.Minimum!.Value.ToString("N2", Culture)} a {metric.Maximum!.Value.ToString("N2", Culture)} {unit} · {metric.Count} valores";

    private static string Value<T>(T? value, string unit) where T : struct, IFormattable => value.HasValue
        ? $"{value.Value.ToString(null, Culture)} {unit}".TrimEnd() : "Indisponível";
}
