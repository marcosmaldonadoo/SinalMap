using SinalMap.Application;
using SinalMap.Domain;
using SinalMap.Infrastructure.Windows;
using SinalMap.Infrastructure.Windows.Native;
using Terminal = System.Console;

namespace SinalMap.Console.Probe;

internal static class Program
{
    private const string Help = """
        SinalMap — Fase 0: prova de coleta Wi-Fi para Windows (.NET 10)

        Listar interfaces:
          dotnet run --project src/SinalMap.Console.Probe -- --list

        Analisar um JSON local (também funciona em Linux; resumo sem identificadores):
          dotnet run --project src/SinalMap.Console.Probe -- --analyze <arquivo.json>

        Comparar duas coletas salvas (diferenças B − A, sem identificadores):
          dotnet run --project src/SinalMap.Console.Probe -- --compare <coleta-a.json> <coleta-b.json>

        Coletar 60 segundos (destino explícito; sem DNS nem destino público padrão):
          dotnet run --project src/SinalMap.Console.Probe -- --target <IPv4> [--interface <GUID>] [--output <arquivo.json>]

        Há 60 posições planejadas: t=0..59 s, com timeout ICMP de 800 ms.
        Mais de uma interface conectada exige --interface. Ctrl+C salva a sessão parcial.
        A rota IPv4 é verificada antes de cada ping. Dados indisponíveis ficam null.
        Nenhuma telemetria. O JSON contém SSID/BSSID; revise antes de compartilhar.
        """;

    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Terminal.CancelKeyPress += handler;
        try
        {
            var command = CommandLine.Parse(args);
            if (command.Help) { Terminal.WriteLine(Help); return 0; }
            if (command.Analyze is { } path)
                return await SessionReportCommand.ExecuteAsync(path, Terminal.Out, Terminal.Error, cancellation.Token);
            if (command.Compare is { } comparison)
                return await SessionComparisonCommand.ExecuteAsync(comparison, Terminal.Out, Terminal.Error, cancellation.Token);
            if (!OperatingSystem.IsWindows())
            {
                Terminal.Error.WriteLine("A coleta nativa requer Windows. A compilação e os testes simulados podem ser executados em outros sistemas.");
                return 2;
            }

            var clock = new SystemProbeClock();
            var monitor = new WindowsWifiMonitor(clock);
            var interfaces = await monitor.ListInterfacesAsync(cancellation.Token);
            if (command.List)
            {
                foreach (var item in interfaces)
                    Terminal.WriteLine($"{item.Id:D}\t{item.State}\t{item.Description}");
                if (interfaces.Count == 0)
                    Terminal.WriteLine("Nenhuma interface Wi-Fi encontrada.");
                return 0;
            }

            var selected = SelectInterface(interfaces, command.InterfaceId);
            var output = command.Output ?? Path.Combine("results", $"sinalmap-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            if (File.Exists(output))
                throw new IOException("O arquivo de saída já existe. Escolha outro caminho; medições anteriores não serão substituídas.");
            Terminal.WriteLine($"Interface: {selected.Id:D} — {selected.Description}");
            Terminal.WriteLine($"Destino: {command.Target}. Coleta de 60 s iniciada; Ctrl+C cancela e salva os dados disponíveis.");

            var session = await new ProbeRunner(monitor, new WindowsIcmpProbe(clock), clock)
                .RunAsync(new ProbeOptions(command.Target!, selected.Id, selected.Description), cancellation.Token);
            // O token da aquisição pode estar cancelado; salvar os dados parciais é uma operação separada.
            await new JsonSessionStore().SaveAsync(session, output, CancellationToken.None);
            Terminal.WriteLine($"JSON: {Path.GetFullPath(output)}");
            var sent = session.Samples.Count(s => s.Icmp?.SendAttempted == true);
            var success = session.Samples.Count(s => s.Icmp?.Outcome == IcmpOutcome.Success);
            var observed = session.Samples.Count(s => s.Wifi?.State == WifiConnectionState.Connected);
            Terminal.WriteLine($"Posições: {session.Samples.Count}; chamadas ICMP: {sent}; respostas com sucesso: {success}; leituras Wi-Fi: {observed}; mudanças de BSSID observadas: {session.BssidChanges.Count}.");
            var rssiAvailable = session.Samples.Count(s => s.Wifi?.RssiDbm is not null);
            var channelAvailable = session.Samples.Count(s => s.Wifi?.Channel is not null);
            var frequencyAvailable = session.Samples.Count(s => s.Wifi?.FrequencyKHz is not null);
            Terminal.WriteLine($"Métricas disponíveis por posição: RSSI {rssiAvailable}/{session.Samples.Count}; canal {channelAvailable}/{session.Samples.Count}; frequência em cache {frequencyAvailable}/{session.Samples.Count}.");
            foreach (var issue in session.Samples.SelectMany(s => s.Issues
                         .Concat(s.Wifi?.Issues ?? [])
                         .Concat(s.Icmp?.Issue is { } pingIssue ? [pingIssue] : Array.Empty<ProbeIssue>()))
                         .DistinctBy(i => (i.Kind, i.Operation, i.NativeCode)).Take(12))
                Terminal.Error.WriteLine($"{issue.Kind}: {issue.Operation}: {issue.Message} (código: {issue.NativeCode?.ToString() ?? "n/a"})");

            if (session.Cancelled) return 130;
            return observed == 0 ? 3 : 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Terminal.Error.WriteLine("Cancelado antes do início da sessão; nenhum arquivo de medição foi criado.");
            return 130;
        }
        catch (NativeWifiException ex)
        {
            Terminal.Error.WriteLine($"{ex.Issue.Operation}: {ex.Message} (código: {ex.Issue.NativeCode})");
            return 1;
        }
        catch (ArgumentException ex)
        {
            Terminal.Error.WriteLine(ex.Message);
            Terminal.Error.WriteLine("Use --help para consultar a sintaxe.");
            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            Terminal.Error.WriteLine(ex.Message);
            return 1;
        }
        finally { Terminal.CancelKeyPress -= handler; }
    }

    internal static WifiInterfaceInfo SelectInterface(IReadOnlyList<WifiInterfaceInfo> interfaces, Guid? requested)
    {
        var connected = interfaces.Where(i => i.State == WifiConnectionState.Connected).ToArray();
        if (requested is { } id)
            return connected.SingleOrDefault(i => i.Id == id)
                ?? throw new ArgumentException("O GUID informado não corresponde a uma interface Wi-Fi conectada. Consulte --list.");
        return connected.Length switch
        {
            1 => connected[0],
            0 => throw new ArgumentException("Conecte o dispositivo ao Wi-Fi antes de iniciar a coleta."),
            _ => throw new ArgumentException("Há mais de uma interface Wi-Fi conectada; selecione uma com --interface. Consulte --list.")
        };
    }
}
