using System.Text.Json;
using SinalMap.Domain;
using SinalMap.Presentation;

namespace SinalMap.Tests;

public sealed class SessionResultViewModelTests
{
    [Fact]
    public async Task Missing_metrics_route_rejection_and_permission_errors_remain_explicit_and_private()
    {
        const string secret = "SEGREDO-NAO-PUBLICAR";
        var session = await SessionComparerTests.CreateSession();
        var first = session.Samples[0] with { Icmp = session.Samples[0].Icmp! with { RoundTripTimeMilliseconds = 0 } };
        var issue = new ProbeIssue(IssueKind.PermissionDenied, secret, secret, 5);
        var second = session.Samples[1] with
        {
            Wifi = WifiSnapshot.Unavailable(session.InterfaceId, session.StartedAt, session.CompletedAt, issue),
            Icmp = session.Samples[1].Icmp! with
            {
                Outcome = IcmpOutcome.RouteRejected, SendAttempted = false, RoundTripTimeMilliseconds = null,
                Issue = new(IssueKind.RouteMismatch, secret, secret)
            }
        };
        var model = new SessionResultViewModel(session with { Samples = [first, second], InterfaceDescription = secret });
        Assert.Equal("0 ms", model.Samples[0].Rtt);
        Assert.Equal("Indisponível", model.Samples[1].Rtt);
        Assert.Equal("Indisponível", model.Samples[1].Rssi);
        Assert.Equal("Rota rejeitada", model.Samples[1].Icmp);
        Assert.Contains("PermissionDenied (5)", model.Samples[1].Occurrences);
        Assert.Contains(model.Occurrences, i => i.Kind == "PermissionDenied" && i.Code == "5" && i.Positions == 1);
        var text = JsonSerializer.Serialize(model);
        foreach (var value in new[] { secret, session.InterfaceId.ToString(), session.SessionId.ToString(), session.TargetAddress, "02:00:00:00:00:01", "74657374" })
            Assert.DoesNotContain(value, text);
    }

    [Fact]
    public async Task Varied_context_and_unrecognized_frequency_source_are_not_presented_as_stable_or_cached()
    {
        var session = await SessionComparerTests.CreateSession();
        var samples = session.Samples.Take(2).ToArray();
        samples[0] = samples[0] with { Wifi = samples[0].Wifi! with { Bssid = "02:00:00:00:00:02", FrequencySource = "private unknown source" } };
        var model = new SessionResultViewModel(session with { Samples = samples });
        Assert.Contains("contexto variou", model.ContextNotice);
        Assert.Contains("contexto indeterminado", model.ContextNotice);
        Assert.Contains("fonte não reconhecida", model.Samples[0].CachedFrequency);
        Assert.DoesNotContain("cache BSS", model.Samples[0].CachedFrequency);
        Assert.Contains("cache BSS", model.Samples[1].CachedFrequency);
    }

    [Fact]
    public async Task ICMP_error_and_absent_RSSI_never_turn_into_zero_values()
    {
        var session = await SessionComparerTests.CreateSession();
        var sample = session.Samples[0] with
        {
            Wifi = session.Samples[0].Wifi! with { RssiDbm = null },
            Icmp = session.Samples[0].Icmp! with { Outcome = IcmpOutcome.IcmpError, RoundTripTimeMilliseconds = null }
        };
        var model = new SessionResultViewModel(session with { Samples = [sample] });
        Assert.Equal("Sem valores registrados", model.Rtt);
        Assert.Equal("Sem valores registrados", model.Rssi);
        Assert.Equal("Ausente", model.Samples[0].RssiSource);
        Assert.Equal("Erro ICMP", model.Samples[0].Icmp);
    }
}
