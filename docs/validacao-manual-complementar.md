# Validação manual complementar no Windows

Data do registro: 2026-10-18. O responsável executou os testes em hardware real e forneceu as saídas de `--analyze`, `--compare`, os códigos de saída e inspeções locais em PowerShell. Os JSONs desta rodada permaneceram na máquina do responsável e **não foram recebidos nem examinados integralmente nesta análise**. Esta evidência complementa a [coleta estável examinada em JSON](validacao-coleta-estavel.md).

A publicação de resultados agregados foi autorizada com remoção dos dados sensíveis. Este registro omite SSID, BSSID, endereços IP, GUIDs, identificadores de sessão, horários exatos e caminhos locais. As durações abaixo são intervalos monotônicos, sem horário de início da coleta.

## Ambiente relatado

- Windows, com adaptador MediaTek Wi-Fi 6 MT7921 Wireless LAN Card.
- Aplicativo de console da Fase 0 em .NET 10, executado pelo binário compilado em Release.
- Versão do driver ainda não informada. A versão/build do Windows não foi fornecida para esta rodada.
- Nenhuma coleta RF foi executada no ambiente Linux de desenvolvimento.

## Coletas em dois locais

O responsável realizou uma coleta próxima ao roteador e outra em local mais distante, seguindo o roteiro manual. Os resumos apresentaram:

| Observação | Próxima, coleta A | Distante, coleta B |
|---|---|---|
| Duração monotônica | 60,0042 s | 60,0072 s |
| Posições despachadas | 60 de 60 | 60 de 60 |
| Chamadas ICMP confirmadas | 60 | 60 |
| Resultados ICMP | 60 `Success` | 59 `Success`, 1 `TimedOut` |
| RTT em sucesso: mínimo / média / máximo | 4,00 / 74,73 / 193,00 ms | 62,00 / 79,95 / 129,00 ms |
| Wi-Fi conectado | 60 de 60 | 60 de 60 |
| RSSI | −55 dBm nas 60 leituras | −55 dBm nas 60 leituras |
| Fonte RSSI | Consulta principal nas 60 leituras | Consulta principal nas 60 leituras |
| Canal e frequência em cache BSS | Disponíveis nas 60 posições | Disponíveis nas 60 posições |
| Mudanças de BSSID observadas | 0 | 0 |

O comparador informou contextos iguais para interface, SSID em bytes, BSSID, destino ICMP, canal, frequência em cache e fonte RSSI. Foram calculadas diferenças B − A de **+5,22 ms na média de RTT** e **0,00 dB na média de RSSI**. A diferença de RTT usa apenas respostas com sucesso; o timeout da coleta B permanece contabilizado separadamente. Não houve descarte por atraso ou cancelamento nessas sessões.

Os registros não isolam a causa da variação de RTT nem comprovam a mudança física de local. A informação sobre os locais vem do procedimento relatado pelo responsável. A constância do RSSI nessas duas sessões não demonstra, por si só, falha do coletor ou do driver.

Uma execução posterior, inicialmente destinada ao cancelamento, terminou normalmente após 60,0045 s, com código 0, 60 respostas ICMP e RSSI entre **−55 e −51 dBm**, média −53,78 dBm. Essa execução não foi aceita como teste de cancelamento. Ela mostra variação do valor reportado no ambiente, mas não estabelece sua precisão ou sua resposta à distância. A fonte permaneceu a consulta principal.

## Cancelamento com salvamento parcial

Em uma nova execução, o responsável pressionou Ctrl+C durante a coleta:

| Verificação | Resultado relatado |
|---|---|
| Duração monotônica | 10,8470 s |
| Sessão cancelada | Sim |
| Posições planejadas | 60 |
| Posições despachadas | 11 |
| Canceladas antes do despacho | 49 |
| Descartadas por atraso | 0 |
| Chamadas e respostas ICMP | 11 confirmadas, todas `Success`; RTT de 3 ms |
| Wi-Fi conectado e RSSI | 11 leituras; −53 dBm pela consulta principal |
| Canal e frequência em cache BSS | Disponíveis nas 11 leituras |
| Código da coleta | 130 |
| Persistência | O arquivo salvo foi lido pelo analisador local |

As 49 posições restantes foram registradas como `CancelledBeforeDispatch`, com ocorrências `Cancelled`. Elas representam envios não iniciados, não perdas de pacotes. As 11 leituras concluídas foram preservadas no resumo da sessão parcial. **O cancelamento e a leitura posterior do arquivo parcial foram evidenciados nesse ambiente.**

## Desconexão mantida até o encerramento

O responsável desligou o Wi-Fi durante outra sessão e esclareceu que o manteve desligado até a coleta terminar:

- Duração monotônica de 60,0076 s; 60 posições despachadas, sem cancelamento ou descarte por atraso.
- 13 posições conectadas, com RSSI, canal e frequência em cache; 47 ocorrências `NotConnected`, código 5023.
- A inspeção local mostrou conexão somente nas sequências 0 a 12 e estado final `Disconnected`.
- 60 chamadas ICMP confirmadas: 13 `Success`, 39 `TimedOut` e 8 `IcmpError`.
- Código da coleta 0 e arquivo legível pelo analisador.

O teste evidenciou a detecção da interrupção e a conclusão da sessão. Não testou reconexão, porque o Wi-Fi permaneceu desligado. O código 0 indica conclusão da coleta com alguma leitura Wi-Fi conectada válida; não exige sucesso em todos os pings.

## Desconexão seguida de reconexão

Em uma sessão separada, o responsável seguiu o roteiro de desligar e religar o Wi-Fi durante a mesma janela de coleta:

| Verificação | Resultado relatado |
|---|---|
| Duração monotônica | 60,0052 s |
| Posições despachadas | 60 de 60; sem atraso descartado ou cancelamento |
| Chamadas ICMP confirmadas | 60 |
| Resultados ICMP | 48 `Success`, 1 `TimedOut`, 11 `IcmpError` |
| RTT em sucesso: mínimo / média / máximo | 1,00 / 3,27 / 8,00 ms |
| Wi-Fi conectado | 48 de 60 posições |
| Ocorrências sem conexão | 12 `NotConnected`, código 5023 |
| RSSI nas leituras disponíveis | Mínimo −53,00; média −52,77; máximo −52,00 dBm |
| Fonte RSSI | Consulta principal nas 48 leituras |
| Canal e frequência em cache BSS | Disponíveis nas 48 leituras |
| Mudanças de BSSID observadas | 0 |
| Estado Wi-Fi final, inspecionado localmente | `Connected` |
| Código da coleta | 0 |

O estado final conectado após a interrupção relatada e a presença de posições sem conexão sustentam a retomada da coleta na mesma execução. Não foi calculado o tempo de reassociação nem recebida uma linha do tempo completa desta sessão. As contagens agregadas também não demonstram correspondência amostra a amostra entre cada erro ICMP e cada leitura Wi-Fi desconectada.

Nas duas sessões de interrupção, `NativeError` sem código numérico apareceu para os resultados ICMP sem sucesso. No código atual, um `TimedOut` também gera essa categoria. Esses registros não determinam sozinhos a causa física de uma falha.

## Permissão de localização negada

Na execução do caso de bloqueio de localização, o responsável forneceu a saída do coletor e do analisador local:

| Verificação | Resultado relatado |
|---|---|
| Duração monotônica | 60,0154 s |
| Posições despachadas | 60 de 60; sem atraso descartado ou cancelamento |
| Chamadas ICMP confirmadas | 60 |
| Resultados ICMP | 58 `Success`, 2 `TimedOut` |
| RTT em sucesso: mínimo / média / máximo | 3,00 / 4,33 / 21,00 ms |
| Leituras Wi-Fi conectadas válidas | 0 de 60 |
| RSSI, canal e frequência em cache | Nenhum valor registrado |
| Acesso negado | 57 posições com `PermissionDenied`, código 5, em `WlanQueryInterface:CurrentConnection` |
| Leitura fora do orçamento | 1 posição com `ObservationTimeout`; limite de 750 ms |
| Leitura anterior ainda pendente | 2 posições com `Busy` |
| Código da coleta | 3 |
| Persistência | JSON salvo e lido pelo analisador local |

O bloqueio foi representado como indisponibilidade de observação, com o código nativo preservado. A ausência de leituras Wi-Fi válidas não equivale a concluir que o rádio estava desconectado: o coletor não conseguiu consultar a conexão, enquanto o ICMP teve respostas em 58 tentativas. A causa dos dois timeouts ICMP não foi determinada.

No código atual, `ObservationTimeout` significa que a espera do consumidor excedeu 750 ms; a chamada nativa pode continuar em andamento. `Busy` indica que uma leitura anterior ainda ocupava o monitor, impedindo outra consulta nativa simultânea. Os resultados tardios não são reaproveitados como uma amostra nova. O resumo não permite atribuir a demora a uma janela de consentimento, ao driver ou a outra causa específica.

O código 3 é o retorno previsto para uma sessão salva sem nenhuma leitura Wi-Fi conectada válida. O teste evidencia o tratamento da permissão negada e a persistência do resultado nesse ambiente. Não demonstra suporte ausente do driver para RSSI/canal/frequência, pois o bloqueio ocorreu antes dessas consultas. A retomada após restaurar o consentimento não foi examinada nesta sessão. A configuração anterior deve ser restaurada antes dos próximos testes.

## Aceite parcial e limites

Evidenciados neste ambiente: comparação de duas sessões reais, persistência e leitura de sessão cancelada, detecção de desconexão, retomada após reconexão e bloqueio por permissão com salvamento de uma sessão sem métricas Wi-Fi. O comportamento do RSSI foi examinado em locais diferentes e houve variação em outras execuções; a resposta à distância e a precisão/calibração continuam inconclusivas.

Permanecem pendentes a versão do driver, os campos sem suporte aplicáveis ao ambiente, a rejeição de uma rota por outra interface e a decisão final de aceite da Fase 0. Roaming e mudança de SSID dependem de um cenário físico apropriado; nenhuma mudança de BSSID foi observada nas sessões com leituras válidas de conexão. A alternativa RSSI não foi utilizada e seu suporte nesse adaptador continua sem comprovação.

Os JSONs brutos desta rodada permanecem locais. Esta análise dos resumos não substitui uma inspeção integral desses arquivos nem certifica o caminho físico dos pacotes. Consulte o [roteiro de validação](windows-validation.md); nenhuma interface WPF foi iniciada.
