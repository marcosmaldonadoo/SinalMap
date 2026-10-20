# Validação manual da coleta no Windows

**Estado: casos principais de coleta, interrupção, permissão e rejeição de rota evidenciados em hardware real; alcance e limites aceitos para o piloto WPF técnico em 2026-10-20.** O [registro da análise de uma sessão estável](validacao-coleta-estavel.md) documenta o JSON examinado com o adaptador MediaTek Wi-Fi 6 MT7921. A [validação manual complementar](validacao-manual-complementar.md) registra os resumos fornecidos para comparação de locais, cancelamento, desconexão, reconexão, permissão negada e rota pela Ethernet. Os JSONs dessa rodada complementar não foram recebidos nem examinados integralmente. Testes simulados e runners hospedados continuam distintos dessa evidência e não certificam precisão de RSSI.

Em 2026-10-20, o responsável aceitou o alcance demonstrado e os casos físicos não exercitados para o piloto WPF técnico, conforme o [parecer](parecer-fase-0.md). As lacunas abaixo permanecem pendentes; o avanço não implica sua aprovação. Ausência de uma métrica por limitação do driver pode ser aceita se estiver representada como ausência com causa, sem dados estimados.

## Preparação

1. Instalar um SDK .NET 10 estável e executar `dotnet --info`.
2. Registrar versão/build do Windows, modelo do adaptador e versão do driver. Não registrar senhas.
3. Conectar ao Wi-Fi a ser testado. Usar `dotnet run --project src/SinalMap.Console.Probe -c Release -- --list` para obter o GUID.
4. Identificar o gateway IPv4 real dessa interface nas configurações da rede ou em `ipconfig`. Não adotar o IP dos exemplos sem verificar.
5. Conferir WlanSvc e as permissões de localização. Se houver bloqueio corporativo, registrar o bloqueio e o código; não tentar contorná-lo.

Para os testes de Ctrl+C, compilar uma vez e executar diretamente o binário do console:

```powershell
dotnet build SinalMap.sln -c Release
$probe = ".\src\SinalMap.Console.Probe\bin\Release\net10.0\SinalMap.Console.Probe.exe"
& $probe --list
```

O índice numérico de uma interface não é seu GUID. Quando o alias no Windows for `Wi-Fi`, o GUID também pode ser obtido com `$wifiGuid = (Get-NetAdapter -Name "Wi-Fi").InterfaceGuid`; adaptar o alias ao nome real. Informar explicitamente o IPv4 de destino verificado nessa interface. Preservar o mesmo PowerShell para manter as variáveis e escolher um arquivo novo para cada coleta.

## Casos a executar

| Caso | Procedimento | Evidência esperada |
|---|---|---|
| Coleta estável | Parado perto do AP, executar 60 s contra o gateway | 60 posições planejadas, timestamps monotônicos crescentes e campos retornados pelo driver com fontes |
| Locais diferentes | Fazer coletas separadas em dois locais, mantendo o destino e registrando a conexão observada | Examinar valores e distribuição de RSSI/latência; não exigir uma variação predeterminada nem tratar constância como erro sem contexto |
| Campos sem suporte | Examinar os resultados do driver | `null` com erro específico; ausência de RSSI estimado ou canal derivado |
| Primeira consulta de RSSI falha | Examinar os contadores, `rssiSource` e `issues` | Fonte alternativa identificada se houver retorno válido; se ambas falharem, RSSI nulo com os códigos preservados |
| Cache de frequência | Examinar `frequencySource` e FILETIME BSS | Frequência identificada como cache; não descrita como recepção nova a cada segundo |
| Alvo que não responde ICMP | Usar um destino conhecido que bloqueie ICMP na rede de teste | `TimedOut`/status de erro e RTT nulo; não transformar isso em diagnóstico de sinal |
| Permissão negada | Desativar o consentimento de localização e executar se a versão do Windows aplicar a restrição | Código 5 e orientação; SSID/BSSID não fabricados. Se a API não bloquear nessa versão, registrar esse fato |
| Cancelamento | Pressionar Ctrl+C uma vez, com a coleta ainda em execução, após cerca de 10 s | Arquivo JSON válido, `cancelled=true`, dados concluídos preservados, posições restantes identificadas e código da coleta 130 |
| Desconexão | Desconectar o Wi-Fi durante uma sessão de teste | Estado/erros reais, sem reaproveitar RSSI/BSSID antigo como leitura nova |
| Reconexão | Em outra sessão, após cerca de 10 s desligar o Wi-Fi por 10 s; religar, selecionar a mesma rede e confirmar que o Windows mostra conectado antes do fim da coleta | Posições sem conexão durante a interrupção, retorno das leituras e estado final conectado; manter o Wi-Fi desligado até o fim não testa recuperação |
| Mudança de BSSID | Em rede com roaming, caminhar entre APs do mesmo SSID | Mudança observada com sequências anterior/atual; não exigir evento se o dispositivo não fez roaming |
| Mudança de SSID | Trocar manualmente de rede durante o teste | Snapshots com identidades diferentes; BSSID diferente em outro SSID classificado separadamente |
| VPN/Ethernet | Fazer o destino ser roteado por outra interface | `RouteRejected`, índices de rota diferentes e `sendAttempted=false` |
| Arquivo existente | Repetir com `--output` apontando para um arquivo já existente | Recusa sem sobrescrever a coleta anterior |

Não usar testes de desligamento/roaming em uma conexão que esteja sustentando acesso remoto indispensável. O programa não faz essas alterações; os casos são conduzidos manualmente em uma rede de teste.

Capturar `$LASTEXITCODE` imediatamente após a coleta, antes de executar `--analyze`: a análise tem seu próprio código de saída. As 60 posições incluem posições canceladas ou descartadas; posições não enviadas não são perdas ICMP. Uma coleta concluída com código 0 pode conter falhas de conectividade.

No teste de localização, anotar a configuração anterior e restaurá-la ao terminar. Uma sessão sem leituras Wi-Fi conectadas válidas pode ser salva com código 3. Acesso negado é diferente de desconexão ou campo sem suporte: avaliar a categoria e o código registrados. `ObservationTimeout` e `Busy` também podem aparecer se uma chamada nativa demorar; não atribuir a causa a uma janela de consentimento sem observá-la.

Para a rota alternativa, manter o Wi-Fi conectado e usar `Find-NetRoute -RemoteIPAddress $destino | Select-Object InterfaceAlias, InterfaceIndex -Unique` para conferir a escolha atual do Windows. Não restringir essa consulta ao índice Wi-Fi: isso deixaria de verificar a escolha livre da rota. Uma rota diferente exige `RouteMismatch`, índices distintos e `sendAttempted=false`; falha na consulta de rota, por si só, não comprova esse cenário. O estado `Up` de `Get-NetAdapter` não substitui a seleção `Connected` da API WLAN no início da coleta. Usar `--list` para conferir o estado reconhecido pelo programa.

## Inspeção rápida do JSON em PowerShell

Para obter primeiro um resumo sem identificadores, execute `dotnet run --project src/SinalMap.Console.Probe -c Release -- --analyze results/ponto-01.json`. Os critérios estão no [guia de análise local](analise-local.md). O aceite manual exige examinar os casos deste roteiro; um resumo concluído não os aprova automaticamente.

Para duas sessões distintas, use `dotnet run --project src/SinalMap.Console.Probe -c Release -- --compare results/ponto-a.json results/ponto-b.json`. O [guia de comparação](comparacao-coletas.md) explica os requisitos das diferenças. A comparação dos registros não comprova que o dispositivo mudou fisicamente de ponto.

```powershell
$s = Get-Content -Raw results/ponto-01.json | ConvertFrom-Json
$s.samples.Count  # 60 posições; nem todas necessariamente enviadas
$s.samples | Group-Object disposition | Select-Object Name, Count
$s.samples | ForEach-Object {
    [pscustomobject]@{
        Sequence = $_.sequence
        Planned = $_.plannedOffset
        DispatchUtc = $_.dispatchedAt.utc
        DispatchTicks = $_.dispatchedAt.monotonicTicks
        BSSID = $_.wifi.bssid
        RSSI = $_.wifi.rssiDbm
        Channel = $_.wifi.channel
        FrequencyKHz = $_.wifi.frequencyKHz
        IcmpStatus = $_.icmp.outcome
        RTTms = $_.icmp.roundTripTimeMilliseconds
    }
} | Format-Table -AutoSize
$s.bssidChanges
```

Os ticks devem ser interpretados com `$s.monotonicFrequency`. Compare SSID/BSSID/canal com as informações do Windows, lembrando que consultas feitas em momentos diferentes podem divergir. Uma porcentagem exibida pelo Windows não é uma referência independente de RSSI em dBm. Para comprovar o tráfego por uma interface, use captura de pacotes apropriada; a checagem de rota anterior ao envio não é essa comprovação.

## Registro do aceite

- [x] Compilação Release e testes automatizados passaram.
- [x] Coleta real de 60 s examinada, com identificação do SO e modelo do adaptador registrados.
- [x] Versão do driver informada por CIM e registrada: MediaTek MT7921, 3.5.0.1349, em Windows 11 Pro.
- [x] Comportamento do RSSI em locais diferentes examinado: constante nas duas sessões comparadas; variação em outras execuções, sem comprovação de resposta à distância ou calibração.
- [x] Permissão de localização negada evidenciada: código 5 preservado, nenhuma métrica Wi-Fi registrada e JSON legível com saída 3.
- [ ] Limitações de campos sem suporte pelo driver examinadas nos casos aplicáveis; o bloqueio de permissão não comprova falta de suporte.
- [x] Cancelamento e gravação parcial evidenciados pelo resumo local e código 130.
- [x] Desconexão e conclusão da sessão evidenciadas pelos resumos e posições relatadas.
- [x] Retomada após reconexão evidenciada por interrupção relatada, posições sem conexão e estado final conectado.
- [x] Rota pela Ethernet rejeitada nas 60 posições, índices distintos e `sendAttempted=false`, conforme resumo e inspeção local relatados.
- [x] Cache BSS e temporização da coleta examinados no JSON.
- [x] Decisão registrada sobre o que o driver permite coletar na próxima fase: piloto técnico limitado aprovado em 2026-10-20, mantendo os limites do parecer.

Os itens complementares marcados acima têm a origem e os limites descritos no [registro dos testes manuais](validacao-manual-complementar.md). O [parecer da Fase 0](parecer-fase-0.md) distingue casos demonstrados, cenários ainda não exercitados e a decisão de avanço de fase. O item de campos sem suporte continua sem ensaio físico específico; essa limitação precisa ser aceita explicitamente, sem declarar um teste que não ocorreu. Os JSONs da validação devem continuar locais até revisão dos identificadores de rede. Não preencher este checklist com resultados simulados.
