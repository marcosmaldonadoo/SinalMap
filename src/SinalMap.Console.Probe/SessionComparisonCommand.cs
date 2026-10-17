using System.Globalization;
using System.Text.Json;
using SinalMap.Application;
using SinalMap.Infrastructure.Windows;

namespace SinalMap.Console.Probe;

internal static class SessionComparisonCommand
{
    internal static async Task<int> ExecuteAsync(ComparisonFiles files, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            var reader = new JsonSessionReader();
            var first = await reader.ReadAsync(files.First, cancellationToken);
            var second = await reader.ReadAsync(files.Second, cancellationToken);
            var comparison = SessionComparer.Compare(first, second);
            cancellationToken.ThrowIfCancellationRequested();
            output.WriteLine("Coleta A:");
            SessionReportCommand.WriteSummary(comparison.First, output);
            output.WriteLine();
            output.WriteLine("Coleta B:");
            SessionReportCommand.WriteSummary(comparison.Second, output);
            output.WriteLine();
            output.WriteLine("Comparação dos contextos registrados nas observações conectadas:");
            var context = comparison.Context;
            WriteContext("Interface registrada", context.Interface);
            WriteContext("SSID em bytes", context.Ssid);
            WriteContext("AP (BSSID)", context.Bssid);
            WriteContext("Destino ICMP", context.Target);
            WriteContext("Canal", context.Channel);
            WriteContext("Frequência em cache BSS", context.CachedFrequency);
            WriteContext("Fonte RSSI reconhecida", context.RssiSource);
            WriteDifference("Diferença da média de RTT (B − A)", comparison.MeanLatencyDifferenceMilliseconds, "ms");
            WriteDifference("Diferença da média de RSSI (B − A)", comparison.MeanRssiDifferenceDb, "dB");
            output.WriteLine("As diferenças descrevem os registros; não isolam causas nem comprovam mudança física de local. Compare também as durações, os contadores e as ocorrências.");
            return 0;

            void WriteContext(string label, ContextComparison value) => output.WriteLine($"  {label}: {value switch
            {
                ContextComparison.Same => "igual",
                ContextComparison.Different => "diferente",
                ContextComparison.VariedWithinSession => "variou dentro de pelo menos uma coleta",
                _ => "indeterminado (dados ausentes ou fonte não reconhecida)"
            }}.");

            void WriteDifference(string label, double? value, string unit) => output.WriteLine(value.HasValue
                ? $"{label}: {value.Value.ToString("+0.00;-0.00;0.00", CultureInfo.GetCultureInfo("pt-BR"))} {unit}."
                : $"{label}: não calculada (contexto incompatível, indeterminado ou sem valores).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error.WriteLine("Comparação cancelada; os arquivos originais foram preservados.");
            return 130;
        }
        catch (InvalidDataException ex)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
        catch (JsonException)
        {
            error.WriteLine("Um dos arquivos contém JSON inválido ou incompleto para o formato suportado.");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error.WriteLine("Não foi possível ler as duas coletas. Confira os caminhos e as permissões de leitura.");
            return 1;
        }
    }
}
