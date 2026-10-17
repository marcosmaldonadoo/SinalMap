namespace SinalMap.Domain;

public enum SlotDisposition { Dispatched, SkippedLate, CancelledBeforeDispatch }

public sealed record ProbeSample(
    int Sequence,
    TimeSpan PlannedOffset,
    DateTimeOffset PlannedAtUtc,
    ClockStamp? DispatchedAt,
    double? StartLatenessMilliseconds,
    SlotDisposition Disposition,
    WifiSnapshot? Wifi,
    IcmpMeasurement? Icmp,
    IReadOnlyList<ProbeIssue> Issues);

public enum BssidChangeKind { SameSsidBssidChangeObserved, DifferentSsidBssidChangeObserved }

/// <summary>Registra uma mudança observada, sem afirmar o instante exato ou a duração do roaming.</summary>
public sealed record BssidChange(
    int PreviousSequence,
    int Sequence,
    ClockStamp DetectedAt,
    string PreviousBssid,
    string Bssid,
    BssidChangeKind Kind,
    bool HasObservationGap);

public sealed record ProbeSession(
    int SchemaVersion,
    Guid SessionId,
    Guid InterfaceId,
    string? InterfaceDescription,
    string TargetAddress,
    ClockStamp StartedAt,
    ClockStamp CompletedAt,
    long MonotonicFrequency,
    TimeSpan PlannedDuration,
    TimeSpan PlannedInterval,
    TimeSpan IcmpTimeout,
    TimeSpan MaximumStartLateness,
    bool Cancelled,
    string OperatingSystem,
    string Runtime,
    IReadOnlyList<ProbeSample> Samples,
    IReadOnlyList<BssidChange> BssidChanges);
