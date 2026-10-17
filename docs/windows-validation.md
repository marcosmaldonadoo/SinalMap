# Validação manual da coleta no Windows

**Estado: coleta básica em hardware real examinada e aprovada; casos adicionais pendentes.** O [registro da análise de uma sessão estável](validacao-coleta-estavel.md) documenta a coleta com o adaptador MediaTek Wi-Fi 6 MT7921. Testes simulados e runners hospedados continuam distintos dessa evidência e não certificam precisão de RSSI.

Não avançar para WPF antes de concluir este roteiro e registrar o aceite. Ausência de uma métrica por limitação do driver pode ser aceita se estiver representada como ausência com causa, sem dados estimados.

## Preparação

1. Instalar um SDK .NET 10 estável e executar `dotnet --info`.
2. Registrar versão/build do Windows, modelo do adaptador e versão do driver. Não registrar senhas.
3. Conectar ao Wi-Fi a ser testado. Usar `dotnet run --project src/SinalMap.Console.Probe -c Release -- --list` para obter o GUID.
4. Identificar o gateway IPv4 real dessa interface nas configurações da rede ou em `ipconfig`. Não adotar o IP dos exemplos sem verificar.
5. Conferir WlanSvc e as permissões de localização. Se houver bloqueio corporativo, registrar o bloqueio e o código; não tentar contorná-lo.

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
| Cancelamento | Pressionar Ctrl+C após cerca de 10 s | Arquivo JSON válido, `cancelled=true`, dados concluídos preservados e posições restantes identificadas |
| Desconexão | Desconectar o Wi-Fi durante uma sessão de teste | Estado/erros reais, sem reaproveitar RSSI/BSSID antigo como leitura nova |
| Mudança de BSSID | Em rede com roaming, caminhar entre APs do mesmo SSID | Mudança observada com sequências anterior/atual; não exigir evento se o dispositivo não fez roaming |
| Mudança de SSID | Trocar manualmente de rede durante o teste | Snapshots com identidades diferentes; BSSID diferente em outro SSID classificado separadamente |
| VPN/Ethernet | Fazer o destino ser roteado por outra interface | `RouteRejected`, índices de rota diferentes e `sendAttempted=false` |
| Arquivo existente | Repetir com `--output` apontando para um arquivo já existente | Recusa sem sobrescrever a coleta anterior |

Não usar testes de desligamento/roaming em uma conexão que esteja sustentando acesso remoto indispensável. O programa não faz essas alterações; os casos são conduzidos manualmente em uma rede de teste.

## Inspeção rápida do JSON em PowerShell

Para obter primeiro um resumo sem identificadores, execute `dotnet run --project src/SinalMap.Console.Probe -c Release -- --analyze results/ponto-01.json`. Os critérios estão no [guia de análise local](analise-local.md). O aceite manual exige examinar os casos deste roteiro; um resumo concluído não os aprova automaticamente.

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
- [ ] Versão do driver registrada para reproduzir a compatibilidade.
- [ ] Comportamento do RSSI em locais diferentes examinado.
- [ ] Permissões e campos indisponíveis representados corretamente.
- [ ] Cancelamento e gravação parcial verificados.
- [ ] Roteamento por outra interface rejeitado no cenário de teste.
- [x] Cache BSS e temporização da coleta examinados no JSON.
- [ ] Decisão registrada sobre o que o driver permite coletar na próxima fase.

Os JSONs da validação devem continuar locais até revisão dos identificadores de rede. Não preencher este checklist com resultados simulados.
