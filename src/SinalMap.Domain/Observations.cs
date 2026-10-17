namespace SinalMap.Domain;

/// <summary>UTC can jump; monotonic ticks, interpreted with the session frequency, cannot.</summary>
public sealed record ClockStamp(DateTimeOffset Utc, long MonotonicTicks);

public enum IssueKind
{
    PermissionDenied,
    NotSupported,
    ServiceUnavailable,
    NotConnected,
    InterfaceMissing,
    InvalidData,
    NativeError,
    BssEntryMissing,
    ConnectionChangedDuringRead,
    Busy,
    ObservationTimeout,
    AdapterError,
    ScheduleMissed,
    RouteMismatch,
    RouteUnavailable,
    Cancelled
}

public sealed record ProbeIssue(IssueKind Kind, string Operation, string Message, uint? NativeCode = null);

public enum WifiConnectionState { Unknown, Connected, Disconnected, Unavailable }

public sealed record WifiInterfaceInfo(Guid Id, string Description, WifiConnectionState State);

public sealed record WifiSnapshot(
    Guid InterfaceId,
    ClockStamp StartedAt,
    ClockStamp CompletedAt,
    WifiConnectionState State,
    string? Ssid,
    string? SsidHex,
    string? Bssid,
    int? RssiDbm,
    uint? Channel,
    uint? FrequencyKHz,
    ulong? BssHostTimestampFileTime,
    string? RssiSource,
    string? ChannelSource,
    string? FrequencySource,
    IReadOnlyList<ProbeIssue> Issues)
{
    public static WifiSnapshot Unavailable(Guid id, ClockStamp start, ClockStamp end, ProbeIssue issue) =>
        new(id, start, end, WifiConnectionState.Unavailable, null, null, null, null, null, null,
            null, null, null, null, [issue]);
}

public sealed record RouteObservation(
    ClockStamp CheckedAt,
    uint? SelectedInterfaceIndex,
    uint? BestInterfaceIndex,
    bool IsSelectedInterface,
    ProbeIssue? Issue);

public enum IcmpOutcome { Success, TimedOut, IcmpError, LocalError, RouteRejected, Cancelled }

public sealed record IcmpMeasurement(
    ClockStamp StartedAt,
    ClockStamp CompletedAt,
    IcmpOutcome Outcome,
    bool? SendAttempted,
    long? RoundTripTimeMilliseconds,
    string? ReplyStatus,
    string? ReplyAddress,
    RouteObservation? Route,
    ProbeIssue? Issue);
