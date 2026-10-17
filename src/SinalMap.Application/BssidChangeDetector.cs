using SinalMap.Domain;

namespace SinalMap.Application;

public static class BssidChangeDetector
{
    public static IReadOnlyList<BssidChange> Detect(IEnumerable<ProbeSample> samples)
    {
        var changes = new List<BssidChange>();
        ProbeSample? previous = null;
        foreach (var sample in samples.OrderBy(s => s.Sequence))
        {
            if (sample.Wifi is not { State: WifiConnectionState.Connected, Bssid: not null } wifi)
                continue;

            if (previous?.Wifi is { Bssid: not null } before && before.Bssid != wifi.Bssid)
            {
                changes.Add(new(previous.Sequence, sample.Sequence, wifi.CompletedAt, before.Bssid,
                    wifi.Bssid, before.SsidHex == wifi.SsidHex
                        ? BssidChangeKind.SameSsidBssidChangeObserved
                        : BssidChangeKind.DifferentSsidBssidChangeObserved,
                    sample.Sequence != previous.Sequence + 1));
            }
            previous = sample;
        }
        return changes;
    }
}
