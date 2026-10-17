# Primeira execução relatada no Windows

Data: 2026-10-17.

Origem: resumo do console fornecido pelo usuário, com SDK .NET 10.0.402 e adaptador **MediaTek Wi-Fi 6 MT7921 Wireless LAN Card**. O arquivo JSON citado pelo console não foi anexado nem inspecionado. Identificadores de rede, GUID, IP e caminhos pessoais foram omitidos deste registro.

| Informação no resumo | Resultado relatado |
|---|---|
| Posições planejadas | 60 |
| Chamadas ICMP | 60 |
| Respostas ICMP com sucesso | 60 |
| Leituras da conexão Wi-Fi | 60 |
| Mudanças de BSSID observadas | 0 |
| Persistência | O console informou a criação de um JSON local |
| Consulta RSSI principal | Foi registrado ao menos um retorno de código 27 |

O resumo confirma uma execução que completou a agenda e obteve respostas ICMP e leituras da conexão. Ele não informa quantas amostras têm RSSI, canal ou frequência disponíveis, nem os valores individuais de latência. Zero mudanças de BSSID significa apenas que nenhuma mudança foi observada nas amostras.

O código 27 foi retornado na operação `WlanQueryInterface:Rssi`. A descrição genérica do Windows sobre setor não determina a causa nessa consulta WLAN e, isoladamente, não comprova um defeito no disco. Não foi concluído que o adaptador nunca oferece RSSI ou que o recurso é permanentemente incompatível.

## Ajuste de compatibilidade

Foi acrescentada uma tentativa de RSSI por `wlan_intf_opcode_realtime_connection_quality` após erros nativos ou falta de suporte da primeira consulta. Ela utiliza exclusivamente o `lRssi` retornado pelo driver, com fonte identificada, em conexão não MLO de enlace único. Não utiliza o RSSI do cache BSS nem converte percentuais em dBm. Ambas as tentativas podem falhar; nesse caso, o campo permanece nulo com os motivos.

O console também passou a contar separadamente as posições com RSSI, canal e frequência em cache. A compatibilidade da alternativa com esse adaptador ainda depende de uma nova execução; testes simulados não comprovam esse suporte.

## Continuação da validação

Repetir a coleta após atualizar a branch. Examinar os contadores e os campos `rssiDbm`, `rssiSource` e `issues` no JSON local. Registrar versão/build do Windows e versão do driver quando forem conhecidas. Os demais casos do [roteiro de validação](windows-validation.md), incluindo cancelamento, desconexão e roteamento, continuam pendentes.
