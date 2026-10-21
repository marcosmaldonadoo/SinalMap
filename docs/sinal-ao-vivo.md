# Sinal Wi-Fi em dBm e histórico ao vivo

Extensão da janela técnica solicitada pelo responsável em 2026-10-20, com foco na leitura de sinal exemplificada como `−60 dBm`.

## Medição e apresentação

A aba **Sinal** mostra a última leitura de RSSI da interface selecionada, em dBm, sua fonte, a posição e um gráfico das leituras concluídas. O número não é um alvo ou valor de demonstração: pode permanecer constante ou variar conforme o driver. Não há conversão de qualidade percentual em RSSI.

O monitoramento contínuo solicitado posteriormente inicia ao abrir a janela e selecionar uma interface conectada. Usa as mesmas consultas Wi-Fi, aproximadamente uma vez por segundo, sem destino IPv4, ICMP ou gravação. Mantém até 60 observações dos últimos 60 s, com o eixo relativo ao início dessa janela recente. O horário mostrado é o término da consulta do Windows; não comprova nova recepção pelo rádio. A fonte utilizada e os motivos de ausência ficam explícitos, sem manter um valor antigo como leitura atual.

O monitor é cancelado e aguardado antes de iniciar coleta ou abrir um arquivo; a troca de interface descarta conclusões tardias da anterior. A leitura nativa mantém seu orçamento de 750 ms e seu bloqueio contra sobreposição, inclusive se o RPC retornar após o prazo. Ao fechar, a janela cancela o monitor. Resultados completos e arquivos abertos permanecem no modo histórico; **Sinal ao vivo** retoma o monitor sem teste, limpando somente a apresentação do resultado anterior.

Continuam valendo as fontes em dBm já existentes: `WlanQueryInterface:rssi` e `realtime_connection_quality`, restrita a um único enlace não MLO. A correção posterior do RSSI estático prioriza a consulta em tempo real na janela e usa `WlanScan` com notificações ACM a cada 5 s, também nos testes, conforme autorização do responsável. O pedido pode gerar probe requests e elevar a latência; não altera permissões, rotas ou configurações do rádio. Não há conversão percentual, chamadas a `netsh` ou interpretação de PHY/MIMO/largura de canal. O formato permanece `schemaVersion=1`, com os campos opcionais `activeWifiScanning` e `wifiScans`; arquivos anteriores continuam aceitos. Consulte o [diagnóstico físico e os limites](correcao-rssi-estatico.md).

A sessão mantém 60 posições em uma janela de 60 s, com ICMP para o IPv4 conferido pelo usuário e checagem de rota antes de cada envio. A janela sugere o gateway IPv4 da interface selecionada quando há um único candidato; a sugestão é editável e não inicia tráfego. O ponto de sinal é publicado quando a consulta Wi-Fi termina, sem aguardar ICMP; não representa uma leitura instantânea a cada quadro recebido pelo rádio. A amostra completa continua aguardando ambas as operações antes de ser persistida.

## Integridade do gráfico

- O eixo horizontal representa o tempo planejado da posição, em segundos; os timestamps completos e atrasos continuam no JSON. O eixo vertical usa dBm.
- Cada ponto corresponde a um RSSI conectado de fonte reconhecida. Ausência, falha, desconexão ou fonte não reconhecida produzem uma lacuna, sem valor zero substituto ou reaproveitamento de RSSI anterior.
- A linha não atravessa posições ausentes, mudanças de interface, SSID, BSSID ou fonte. Contexto insuficiente também impede essa união. Retas entre pontos são um recurso visual, não novas medições ou suavização.
- Conclusões fora de ordem são organizadas pela sequência; uma conclusão antiga não substitui o valor da posição mais recente. Posições futuras canceladas não são apresentadas como novas leituras.
- Ao terminar, o cabeçalho passa de **Sinal atual** para **Última leitura registrada**. O histórico é reconstruído a partir da sessão completa, inclusive no cancelamento ou ao abrir JSON.
- O gráfico e o indicador não mostram SSID/BSSID, GUIDs, IPs ou textos livres do arquivo. O JSON original e os campos de configuração da janela continuam contendo identificadores locais.

## Publicação e verificações

`ProbeRunner` aceita canais opcionais distintos para amostras completas (`liveSamples`) e projeções de sinal (`signalUpdates`). A projeção é um `ProbeSample` com Wi-Fi concluído e ICMP nulo, usada somente para apresentação; não é gravada. A publicação usa `TryWrite`, sem aguardar a janela. A apresentação cria um canal sem continuações síncronas, consome na thread gráfica e encerra esse consumo antes de concluir a operação. Canal cheio ou fechado não impede a aquisição; a sessão retornada continua sendo a fonte completa para persistência e histórico final.

Ambiente de verificação: Windows x64, SDK .NET 10.0.402 e runtime .NET 10.0.13. Compilação Release com 0 avisos/erros e **184 testes aprovados**, incluindo 11 novos casos sobre publicação, canal cheio/fechado, atualização antes do fim da sessão, ausência, privacidade, ordem de conclusões e interrupção da linha. Os testes novos usam adaptadores e registros simulados.

A revisão de monitoramento contínuo e gateway tem **200 testes aprovados**, incluindo leitura sem alvo/ICMP/gravação, cancelamento antes da coleta e no fechamento, descarte de resposta tardia na troca de interface, retorno do histórico ao monitor, janela recente limitada, ausência explícita, publicação anterior ao ICMP e descoberta de gateway por GUID, sem escolher arbitrariamente entre múltiplos candidatos. Esses testes são simulados e não comprovam precisão do rádio.

## Inspeção física em 2026-10-20

Executada nesta máquina Windows x64 com MediaTek Wi-Fi 6 MT7921, driver Wi-Fi 3.5.0.1349 e runtime .NET 10.0.13. A aba exibiu RSSI e pontos antes do fim da aquisição. A sessão examinada terminou com 60 posições despachadas, sem descarte por atraso, e 57 valores de RSSI entre −56 e −54 dBm (média descritiva −54,60 dBm).

Houve 51 valores da consulta principal e seis da alternativa de enlace único. Nas seis posições alternativas, o JSON preservou o erro 27 da consulta principal. Essa execução é evidência física da alternativa nesse perfil, sem comprovar MLO ou compatibilidade com outros drivers. As três posições sem métricas tinham uma ocorrência `ObservationTimeout` e duas `Busy`, sem RSSI/canal/frequência preenchidos.

A fonte RSSI variou e o histórico interrompeu a união entre fontes. O ICMP registrou 59 respostas com sucesso e um erro local de rota indisponível; isso não foi apresentado como RSSI zero. O analisador do console concluiu com código 0, confirmou os contadores e o SHA-256 permaneceu igual após a análise. O JSON bruto permanece local em `results/`, ignorado pelo Git.

A frequência de atualização, a calibração e a precisão do RSSI dependem do driver. Constância em `−60 dBm`, por si só, não demonstra erro nem calibração. Este indicador não diagnostica cobertura ou interferência e não faz teste de velocidade.
