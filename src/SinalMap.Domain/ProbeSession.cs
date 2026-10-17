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

/// <summary>An observed change, not the exact instant or duration of a roaming event.</summary>
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
