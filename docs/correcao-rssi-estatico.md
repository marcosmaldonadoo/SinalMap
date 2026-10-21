# Correção do RSSI estático

## Diagnóstico e decisão

Em 2026-10-20, o responsável relatou RSSI constante em −66 dBm mesmo ao movimentar o notebook. A comparação direta das consultas nativas, nesta máquina Windows x64 com MediaTek Wi-Fi 6 MT7921, manteve −66 nas duas fontes por 35 leituras. Outra inspeção mostrou qualidade percentual variável e RSSI constante também no cache BSS; qualidade não foi convertida em dBm.

Uma única solicitação `WlanScan`, aceita com código 0, foi seguida por valores −68/−69 na consulta em tempo real e atualização posterior da consulta tradicional. Esse teste isolou a insuficiência de apenas repetir a consulta nesse perfil de driver. Não comprova que todo RSSI constante é obsoleto nem estabelece calibração.

O responsável autorizou varreduras a cada 5 s também na coleta de 60 s, aceitando que podem afetar o RTT, desde que isso seja registrado. A janela usa essa política; a coleta padrão do console conserva sua política anterior, sem iniciar varreduras.

## Comportamento

- Leituras de RSSI continuam aproximadamente a cada segundo. A atualização por varredura ocorre em uma tarefa separada, com pedidos espaçados por pelo menos 5 s. Ela não bloqueia a agenda ICMP.
- A janela prioriza `realtime_connection_quality.linksInfo[0].lRssi`, um valor em dBm. A consulta tradicional permanece como compatibilidade quando a API em tempo real não tem suporte ou falha com erro nativo compatível. Falhas de acesso, buffers inválidos, MLO e enlaces ambíguos não são contornados pela compatibilidade.
- O cliente registra notificações ACM antes do pedido, filtra a interface e aguarda `scan_complete` ou `scan_fail` por até 4 s. Aceitar o pedido não é apresentado como conclusão. A confirmação observada pertence à interface; a API não fornece um identificador para distinguir pedidos de outros aplicativos.
- O orçamento externo é de 5 s. Se um RPC nativo ultrapassa esse prazo, o worker mantém a propriedade do handle e do callback até retornar, e outra varredura é recusada enquanto ele permanecer ativo.
- O cancelamento impede novos pedidos. Uma varredura já aceita pelo rádio é aguardada até notificação ou prazo; a janela pode levar alguns segundos para iniciar a coleta, salvar ou fechar. Callbacks não fecham handles nem desregistram a si próprios.
- A interface mostra o estado da varredura e o risco de RSSI desatualizado quando não há confirmação. O sinal continua sendo o valor retornado pelo Windows, sem interpolar ausências, converter porcentagem, suavizar ou criar variações.

## Registro e análise

O JSON mantém `schemaVersion=1` e acrescenta propriedades opcionais à sessão: `activeWifiScanning` identifica a política; `wifiScans` conserva início, término, conclusão e eventual ocorrência/código de cada operação que retornou. Cancelamento antes do envio não cria um evento de sucesso. Timeout, recusa e falha ficam distintos de conclusão. Os eventos não contêm SSID, BSSID ou destinos adicionais.

O término da sessão inclui a espera final da varredura; a janela planejada de aquisição permanece em 60 s. Resumo WPF e análise do console avisam que varreduras podem elevar o RTT. A comparação não calcula diferenças de médias entre sessões com políticas diferentes de varredura. Leitores antigos podem ignorar as novas propriedades e não exibir o aviso; use esta versão para analisar essas coletas.

## Verificação

Windows x64, SDK .NET 10.0.402, runtime .NET 10.0.13. Compilação final Release sem avisos/erros e 218 testes aprovados. Os casos adicionais simulam notificação da interface correta, registro/limpeza de callbacks, negação de acesso, falha com código WLAN, timeout, cancelamento, sobreposição, consulta tradicional fixa com RSSI em tempo real variável, compatibilidade, MLO, buffer inválido e metadados de varredura. Incluem a preservação do handle/callback após timeout de RPC e a espera de uma varredura aceita antes de iniciar a coleta, sem misturar seu evento com a nova sessão. Testes simulados não demonstram recepção no rádio.

Na inspeção passiva da janela corrigida, o valor e o traçado variaram, com mínimo observado −70 dBm e máximo −61 dBm em um dos instantes inspecionados. A precisão absoluta não foi comparada com instrumento calibrado. As capturas e os arquivos brutos permanecem locais e não integram o repositório.

A coleta seguinte pela janela teve 60 posições despachadas, nenhum descarte por atraso e 60 valores de RSSI da consulta em tempo real, entre −69 e −24 dBm, média descritiva −53,57 dBm. Foram registradas 13 operações de varredura com conclusão confirmada e uma mudança observada de BSSID; a linha foi interrompida nessa transição. A duração total foi de 62,97 s, incluindo a espera final da varredura. Houve 58 respostas ICMP com sucesso, um timeout e um erro local de rota; RTT em sucesso entre 5 e 105 ms, média 25,52 ms. Frequência BSS em cache estava disponível em 56 posições; as quatro ausências conservaram `BssEntryMissing`.

A análise do console concluiu com código 0 e exibiu o aviso de possível impacto das varreduras no RTT. O responsável confirmou que, ao se movimentar, observou grandes alterações de sinal e considerou o comportamento adequado. Essa é validação física de atualização neste adaptador/driver, sem afirmar calibração ou compatibilidade universal. O JSON não foi incluído no repositório.

## Referências

- [WlanScan: conclusão assíncrona, período de 4 s e impacto sobre latência](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanscan).
- [Notificações ACM e cuidados ao desregistrar callbacks](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanregisternotification).
- [RSSI em dBm na informação de enlace em tempo real](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/ns-wlanapi-wlan_realtime_connection_quality_link_info).
