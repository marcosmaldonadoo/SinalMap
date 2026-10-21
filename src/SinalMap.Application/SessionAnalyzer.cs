using SinalMap.Domain;

namespace SinalMap.Application;

public sealed record NumericSummary(int Count, double? Minimum, double? Mean, double? Maximum);
public sealed record IcmpOutcomeCount(IcmpOutcome Outcome, int Count);
public sealed record IssueCount(IssueKind Kind, uint? NativeCode, int AffectedPositions);

/// <summary>Resumo descritivo sem identificadores, horários absolutos ou mensagens livres do arquivo original.</summary>
public sealed record SessionSummary(
    int Positions, int Dispatched, int SkippedLate, int CancelledBeforeDispatch,
    bool Cancelled, double DurationSeconds, int ConnectedWifi,
    int IcmpAttempts, int UnknownIcmpAttempts, IReadOnlyList<IcmpOutcomeCount> IcmpOutcomes,
    NumericSummary LatencyMilliseconds, NumericSummary RssiDbm,
    int PrimaryRssi, int AlternativeRssi, int OtherRssiSource,
    int Channels, int Frequencies, int CachedFrequencies, int BssidChanges,
    IReadOnlyList<IssueCount> Issues, SessionContextSummary Context)
{
    public bool ActiveWifiScanning { get; init; }
    public int WifiScanAttempts { get; init; }
    public int WifiScansCompleted { get; init; }
}

public static class SessionAnalyzer
{
    public static SessionSummary Analyze(ProbeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Validate(session);
        var samples = session.Samples;
        var rssi = Describe(samples.Where(s => s.Wifi?.RssiDbm is not null).Select(s => (double)s.Wifi!.RssiDbm!.Value));
        var primary = samples.Count(s => s.Wifi?.RssiDbm is not null && s.Wifi.RssiSource == "WlanQueryInterface:rssi");
        var alternative = samples.Count(s => s.Wifi?.RssiDbm is not null &&
            s.Wifi.RssiSource == "WlanQueryInterface:realtime_connection_quality.linksInfo[0].lRssi");
        // Uma ocorrência repetida em vários campos da mesma posição conta uma única vez por categoria/código.
        var issues = samples.SelectMany(s => EnumerateIssues(s).Select(i => (i.Kind, i.NativeCode)).Distinct())
            .GroupBy(i => i).OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.NativeCode)
            .Select(g => new IssueCount(g.Key.Kind, g.Key.NativeCode, g.Count())).ToArray();
        return new(
            samples.Count,
            samples.Count(s => s.Disposition == SlotDisposition.Dispatched),
            samples.Count(s => s.Disposition == SlotDisposition.SkippedLate),
            samples.Count(s => s.Disposition == SlotDisposition.CancelledBeforeDispatch),
            session.Cancelled,
            (double)(((decimal)session.CompletedAt.MonotonicTicks - session.StartedAt.MonotonicTicks) / session.MonotonicFrequency),
            samples.Count(s => s.Wifi?.State == WifiConnectionState.Connected),
            samples.Count(s => s.Icmp?.SendAttempted == true),
            samples.Count(s => s.Icmp is { SendAttempted: null }),
            samples.Where(s => s.Icmp is not null).GroupBy(s => s.Icmp!.Outcome).OrderBy(g => g.Key)
                .Select(g => new IcmpOutcomeCount(g.Key, g.Count())).ToArray(),
            Describe(samples.Where(s => s.Icmp?.Outcome == IcmpOutcome.Success)
                .Select(s => (double)s.Icmp!.RoundTripTimeMilliseconds!.Value)),
            rssi, primary, alternative, rssi.Count - primary - alternative,
            samples.Count(s => s.Wifi?.Channel is not null),
            samples.Count(s => s.Wifi?.FrequencyKHz is not null),
            samples.Count(s => s.Wifi?.FrequencyKHz is not null &&
                s.Wifi.FrequencySource == "WlanGetNetworkBssList:ulChCenterFrequency (cached)"),
            session.BssidChanges.Count, issues, SessionContextSummary.FromSession(session))
        {
            ActiveWifiScanning = session.ActiveWifiScanning,
            WifiScanAttempts = session.WifiScans?.Count ?? 0,
            WifiScansCompleted = session.WifiScans?.Count(s => s.Completed) ?? 0
        };
    }

    private static NumericSummary Describe(IEnumerable<double> values)
    {
        var items = values.ToArray();
        return items.Length == 0 ? new(0, null, null, null) : new(items.Length, items.Min(), items.Average(), items.Max());
    }

    private static IEnumerable<ProbeIssue> EnumerateIssues(ProbeSample sample) =>
        sample.Issues.Concat(sample.Wifi?.Issues ?? [])
            .Concat(sample.Icmp?.Issue is { } ping ? [ping] : Array.Empty<ProbeIssue>())
            .Concat(sample.Icmp?.Route?.Issue is { } route ? [route] : Array.Empty<ProbeIssue>());

    private static void Validate(ProbeSession session)
    {
        if (session.SchemaVersion != 1)
            throw new InvalidDataException("Versão do JSON não suportada; esta análise aceita schemaVersion 1.");
        if (session.StartedAt is null || session.CompletedAt is null || session.MonotonicFrequency <= 0 ||
            session.CompletedAt.MonotonicTicks < session.StartedAt.MonotonicTicks)
            throw new InvalidDataException("Metadados do relógio monotônico inválidos.");
        if (session.Samples is null || session.BssidChanges is null)
            throw new InvalidDataException("Listas obrigatórias ausentes no JSON.");
        if ((!session.ActiveWifiScanning && session.WifiScans is { Count: > 0 }) ||
            session.WifiScans?.Any(scan => scan is null || scan.StartedAt is null || scan.CompletedAt is null ||
            scan.CompletedAt.MonotonicTicks < scan.StartedAt.MonotonicTicks ||
            scan.Completed == (scan.Issue is not null) || (scan.Issue is not null && !Enum.IsDefined(scan.Issue.Kind))) == true)
            throw new InvalidDataException("Registro de varredura Wi-Fi inválido.");
        if (session.BssidChanges.Any(change => change is null || !Enum.IsDefined(change.Kind)))
            throw new InvalidDataException("Mudança de BSSID inválida no JSON.");
        var sequences = new HashSet<int>();
        foreach (var sample in session.Samples)
        {
            if (sample is null || sample.Issues is null || !Enum.IsDefined(sample.Disposition) ||
                sample.Sequence < 0 || !sequences.Add(sample.Sequence))
                throw new InvalidDataException("Posição inválida ou sequência repetida no JSON.");
            if (sample.Wifi is { } wifi && (wifi.Issues is null || !Enum.IsDefined(wifi.State) ||
                (wifi.State != WifiConnectionState.Connected &&
                    (wifi.RssiDbm is not null || wifi.Channel is not null || wifi.FrequencyKHz is not null))))
                throw new InvalidDataException("Observação Wi-Fi inválida no JSON.");
            if (sample.Icmp is { } icmp && (!Enum.IsDefined(icmp.Outcome) ||
                (icmp.Outcome == IcmpOutcome.Success
                    ? icmp.RoundTripTimeMilliseconds is null or < 0 || icmp.SendAttempted != true
                    : icmp.RoundTripTimeMilliseconds is not null)))
                throw new InvalidDataException("Resultado ICMP inconsistente no JSON.");
            if (EnumerateIssues(sample).Any(i => i is null || !Enum.IsDefined(i.Kind)))
                throw new InvalidDataException("Tipo de ocorrência inválido no JSON.");
        }
    }
}
