using System.Diagnostics;
using SinalMap.Application;
using SinalMap.Domain;

namespace SinalMap.Infrastructure.Windows;

public sealed class SystemProbeClock : IProbeClock
{
    public long Frequency => Stopwatch.Frequency;
    public ClockStamp Capture() => new(DateTimeOffset.UtcNow, Stopwatch.GetTimestamp());
    public TimeSpan ElapsedSince(long startTimestamp) => Stopwatch.GetElapsedTime(startTimestamp);
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
}
