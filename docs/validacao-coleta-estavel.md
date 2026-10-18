# Validação de uma coleta estável no Windows

Data da análise: 2026-10-17. O usuário forneceu um JSON real após repetir a coleta. O conteúdo foi analisado integralmente quanto a estrutura, valores, fontes, tempos, rotas e erros. A publicação deste resumo foi autorizada pelo usuário após a remoção das informações sensíveis. O JSON bruto, SSID, BSSID, endereços IP, GUIDs, identificador da sessão, horários exatos da coleta e caminhos locais não são incluídos no repositório.

## Ambiente informado

| Informação | Valor |
|---|---|
| Adaptador | MediaTek Wi-Fi 6 MT7921 Wireless LAN Card |
| Sistema operacional | Windows; build omitido neste resumo público |
| Runtime e SDK | .NET 10; versões detalhadas omitidas neste resumo público |
| Versão do driver | Não informada |

## Resultados observados no arquivo

| Verificação | Resultado |
|---|---|
| Posições | 60, sequências de 0 a 59, todas despachadas |
| Duração monotônica da sessão | 60,0104181 s |
| ICMP | 60 tentativas, 60 respostas com sucesso; nenhum timeout |
| RTT do ICMP | Mínimo 3 ms; média 3,77 ms; mediana 4 ms; máximo 8 ms |
| RSSI | −67 dBm em todas as 60 amostras |
| Fonte do RSSI | `WlanQueryInterface:rssi` em todas as amostras |
| Canal | 64 em todas as amostras, pela consulta nativa de canal |
| Frequência | 5.320.000 kHz, equivalente a 5.320 MHz, pela entrada BSS em cache |
| Identidade da conexão | Uma interface, um SSID e um BSSID ao longo da sessão |
| Erros | Nenhum em posição, Wi-Fi, ICMP ou verificação de rota |
| Rota IPv4 | A interface escolhida pelo Windows correspondeu à selecionada nas 60 verificações prévias |
| Atraso de despacho | Média 6,73 ms; máximo 15,05 ms, abaixo do limite configurado de 250 ms |
| Intervalo entre chamadas ICMP | Média 1.000,09 ms; variação observada de 957,21 a 1.020,96 ms |
| Cancelamento | Não solicitado nesta execução |

Os timestamps de despacho e de início do ICMP são estritamente crescentes. Os horários planejados correspondem aos deslocamentos de 0 a 59 s. As operações começaram e terminaram dentro da sessão, e as verificações de rota precederam as chamadas ICMP. As representações UTC e monotônica foram coerentes ao longo da coleta, sem salto de relógio identificado.

## Origem e limites dos dados

A primeira consulta RSSI funcionou nas 60 amostras. A consulta alternativa **não foi utilizada**, e o erro 27 da execução anterior não reapareceu nesse arquivo. Esta evidência não determina a causa daquele retorno nem comprova o suporte da consulta alternativa no adaptador.

A frequência continua identificada como informação do cache BSS. Foram encontradas 27 marcas de recepção distintas nas 60 consultas; a idade aproximada do registro no término de cada leitura variou de 8 ms a 2,008 s. Repetir uma consulta ao cache não equivale a receber um novo quadro em cada segundo.

O RSSI constante em −67 dBm não é evidência suficiente de um problema. O arquivo não comprova movimento entre pontos ou uma alteração controlada das condições de rádio. Também não permite certificar precisão/calibração do driver. É necessário comparar locais com condições diferentes para verificar a resposta do valor reportado.

A checagem de rota correspondeu ao adaptador selecionado antes dos pings. Não foi realizada captura de pacotes para comprovar o caminho físico de cada envio.

## Aceite desta análise

**Aprovada a coleta básica em uma sessão de 60 segundos nesse adaptador:** valores presentes, fontes explícitas, persistência legível, identidade estável, ICMP e temporização coerentes.

Uma [rodada posterior de validação manual](validacao-manual-complementar.md) acrescentou evidência complementar por resumos e inspeções locais relatadas. Seus JSONs brutos não foram recebidos nesta análise; a evidência tem limites distintos do arquivo integral examinado neste documento. O estado dos casos e as pendências atuais estão no [roteiro de validação](windows-validation.md). A implementação permanece na Fase 0; nenhuma interface WPF foi iniciada.
