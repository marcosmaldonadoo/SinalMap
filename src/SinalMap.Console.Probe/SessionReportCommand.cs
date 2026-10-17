using System.Globalization;
using System.Text.Json;
using SinalMap.Application;
using SinalMap.Infrastructure.Windows;

namespace SinalMap.Console.Probe;

internal static class SessionReportCommand
{
    internal static async Task<int> ExecuteAsync(string path, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            var session = await new JsonSessionReader().ReadAsync(path, cancellationToken);
            WriteSummary(SessionAnalyzer.Analyze(session), output);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error.WriteLine("Análise cancelada; o arquivo original foi preservado.");
            return 130;
        }
        catch (InvalidDataException ex)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
        catch (JsonException)
        {
            error.WriteLine("JSON inválido ou incompleto para o formato de coleta suportado.");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Mensagens do sistema podem conter nomes de usuário, caminhos e outros identificadores.
            error.WriteLine("Não foi possível ler o arquivo. Confira o caminho e as permissões de leitura.");
            return 1;
        }
    }

    internal static void WriteSummary(SessionSummary summary, TextWriter output)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        output.WriteLine("Resumo local — estatísticas calculadas a partir dos registros do JSON");
        output.WriteLine($"Sessão cancelada: {(summary.Cancelled ? "sim" : "não")}; duração monotônica: {summary.DurationSeconds.ToString("N4", culture)} s.");
        output.WriteLine($"Posições: {summary.Positions}; despachadas: {summary.Dispatched}; descartadas por atraso: {summary.SkippedLate}; canceladas antes do despacho: {summary.CancelledBeforeDispatch}.");
        output.WriteLine($"ICMP: {summary.IcmpAttempts} chamadas confirmadas; {summary.UnknownIcmpAttempts} com tentativa desconhecida.");
        foreach (var outcome in summary.IcmpOutcomes)
            output.WriteLine($"  {outcome.Outcome}: {outcome.Count}");
        WriteMetric("RTT em sucesso (ms)", summary.LatencyMilliseconds);
        output.WriteLine($"Wi-Fi conectado: {summary.ConnectedWifi}/{summary.Positions}; mudanças de BSSID registradas: {summary.BssidChanges}.");
        WriteMetric("RSSI registrado (dBm)", summary.RssiDbm);
        output.WriteLine($"Fontes RSSI: principal {summary.PrimaryRssi}; alternativa {summary.AlternativeRssi}; outra ou não identificada {summary.OtherRssiSource}.");
        output.WriteLine($"Métricas por posição: canal {summary.Channels}/{summary.Positions}; frequência {summary.Frequencies}/{summary.Positions}, sendo {summary.CachedFrequencies} identificadas como cache BSS.");
        if (summary.Issues.Count == 0)
            output.WriteLine("Nenhuma ocorrência registrada.");
        else
        {
            output.WriteLine("Posições com ocorrências por tipo/código (categorias podem se sobrepor):");
            foreach (var issue in summary.Issues)
                output.WriteLine($"  {issue.Kind}; código {issue.NativeCode?.ToString(culture) ?? "n/a"}: {issue.AffectedPositions}");
        }
        output.WriteLine("Este resumo não certifica precisão do driver nem saúde da rede. Frequência em cache não é uma nova recepção por consulta.");

        void WriteMetric(string label, NumericSummary metric) => output.WriteLine(metric.Count == 0
            ? $"{label}: sem valores registrados."
            : $"{label}: {metric.Count} valores; mínimo {metric.Minimum!.Value.ToString("N2", culture)}; média {metric.Mean!.Value.ToString("N2", culture)}; máximo {metric.Maximum!.Value.ToString("N2", culture)}.");
    }
}
