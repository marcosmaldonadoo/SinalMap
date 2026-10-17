# Plano da Fase 0

Este plano foi apresentado antes da implementação. Escopo: comprovar coleta da rede conectada e executar ICMP durante 60 segundos. Não incluir WPF, plantas, mapas de calor, diagnóstico automático, escaneamento ativo, análise de redes vizinhas ou estimativas de cobertura.

## Arquivos e dependências

| Projeto / arquivos | Responsabilidade |
|---|---|
| `SinalMap.sln`, `global.json`, `Directory.Build.props` | SDK .NET 10, solução e configuração de compilação |
| Domain: `Observations.cs`, `ProbeSession.cs` | Valores observados, proveniência, ausência, erros, timestamps e eventos de BSSID |
| Application: `Abstractions.cs`, `ProbeOptions.cs` | Contratos independentes do Windows e parâmetros fixos da prova |
| Application: `ProbeRunner.cs`, `BssidChangeDetector.cs` | Agenda absoluta, concorrência, cancelamento e comparação de observações |
| Infrastructure.Windows: `Native/` | ABI Win32, `SafeHandle`, buffers e tratamento dos códigos retornados |
| Infrastructure.Windows: `WindowsWifiMonitor.cs` | Leitura da conexão e das métricas suportadas pelo driver |
| Infrastructure.Windows: `WindowsRouteInspector.cs`, `WindowsIcmpProbe.cs` | Verificação de rota IPv4 e envio ICMP assíncrono |
| Infrastructure.Windows: `SystemProbeClock.cs`, `JsonSessionStore.cs` | Relógio monotônico/UTC e gravação local sem sobrescrita |
| Console.Probe: `CommandLine.cs`, `Program.cs` | Seleção explícita do adaptador/destino, composição e apresentação mínima |
| `tests/SinalMap.Tests/` | Adaptadores simulados e testes de comportamento e memória |
| `.github/workflows/ci.yml` | Compilação e testes simulados em Linux e Windows |

Dependências de projetos: Domain ← Application ← Infrastructure.Windows ← Console.Probe. Testes podem substituir as fronteiras sem invocar as APIs nativas.

## APIs escolhidas

- Native Wi-Fi: `WlanOpenHandle(2)`, `WlanEnumInterfaces`, `WlanQueryInterface`, `WlanGetNetworkBssList`, `WlanFreeMemory`, `WlanCloseHandle`.
- IP Helper: `GetBestInterfaceEx`, comparado com o índice IPv4 da interface selecionada.
- .NET: `Ping.SendPingAsync`, `Stopwatch`, `DateTimeOffset`, `Task.Delay`, `System.Text.Json`, `CancellationToken` e `SafeHandle`.

Não usar `netsh` como parser de dados do produto, percentuais convertidos em RSSI, tabelas de canal convertidas em dados medidos ou inferência de frequência quando ela não é retornada.

## Riscos e decisões

1. **Permissão de localização e políticas:** preservar `ERROR_ACCESS_DENIED` e orientar o usuário, sem tentar contornar as permissões.
2. **Driver sem suporte:** campos opcionais nulos e erro por operação; não impedir a leitura dos campos restantes.
3. **Cache BSS:** frequência identificada como informação em cache, com o timestamp bruto do driver.
4. **Memória nativa:** `SafeHandle`, validação de tamanho/contagem, `finally` e simulações de erro; IEs variáveis não são interpretados.
5. **RPC bloqueante:** executar fora do chamador, limitar a espera do consumidor a 750 ms e impedir leituras nativas sobrepostas. A chamada nativa não é interrompida à força.
6. **Mudança da conexão durante a leitura:** consultar a conexão antes e depois das métricas; descartar a amostra se a identidade mudar. Isso reduz o risco, mas não torna várias APIs uma operação atômica.
7. **Agendamento:** 60 posições absolutas com relógio monotônico. Registrar perdas de prazo, não compensá-las com rajadas.
8. **Rota por outra interface:** rejeitar o ping quando o Windows indicar outra interface; documentar a corrida entre checagem e envio.
9. **Ambiente de desenvolvimento:** compilar e testar simulações fora do Windows; manter validação com driver/hardware como pendência explícita.

## Critério para encerrar a fase

Compilação e testes devem passar. Uma coleta real de 60 s no Windows deve ser examinada, incluindo permissões, ausência de suporte, cancelamento e comportamento com VPN/Ethernet. Se o driver não fornecer RSSI/canal/frequência, a ausência com causa é um resultado válido da prova de conceito. Nenhuma interface gráfica deve ser iniciada com base apenas na aprovação dos testes simulados.
