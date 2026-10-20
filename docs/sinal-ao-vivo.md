# Sinal Wi-Fi em dBm e histórico ao vivo

Extensão da janela técnica solicitada pelo responsável em 2026-10-20, com foco na leitura de sinal exemplificada como `−60 dBm`.

## Medição e apresentação

A aba **Sinal** mostra a última leitura de RSSI da interface selecionada, em dBm, sua fonte, a posição e um gráfico das leituras concluídas. O número não é um alvo ou valor de demonstração: pode permanecer constante ou variar conforme o driver. Não há conversão de qualidade percentual em RSSI.

Continuam valendo as fontes já existentes: `WlanQueryInterface:rssi` e a alternativa restrita a um único enlace não MLO. Não foram adicionadas novas consultas nativas, chamadas a `netsh`, mudanças de permissões, tráfego externo ou interpretação de PHY/MIMO/largura de canal neste incremento de sinal. O formato JSON permanece `schemaVersion=1`, sem novos campos obrigatórios. Arquivos anteriores podem ser abertos para ver o histórico.

A sessão mantém 60 posições em uma janela de 60 s, com ICMP para o IPv4 explicitamente informado e checagem de rota antes de cada envio. O ponto é publicado quando as operações daquela posição terminam; não representa uma leitura instantânea a cada quadro recebido pelo rádio.

## Integridade do gráfico

- O eixo horizontal representa o tempo planejado da posição, em segundos; os timestamps completos e atrasos continuam no JSON. O eixo vertical usa dBm.
- Cada ponto corresponde a um RSSI conectado de fonte reconhecida. Ausência, falha, desconexão ou fonte não reconhecida produzem uma lacuna, sem valor zero substituto ou reaproveitamento de RSSI anterior.
- A linha não atravessa posições ausentes, mudanças de interface, SSID, BSSID ou fonte. Contexto insuficiente também impede essa união. Retas entre pontos são um recurso visual, não novas medições ou suavização.
- Conclusões fora de ordem são organizadas pela sequência; uma conclusão antiga não substitui o valor da posição mais recente. Posições futuras canceladas não são apresentadas como novas leituras.
- Ao terminar, o cabeçalho passa de **Sinal atual** para **Última leitura registrada**. O histórico é reconstruído a partir da sessão completa, inclusive no cancelamento ou ao abrir JSON.
- O gráfico e o indicador não mostram SSID/BSSID, GUIDs, IPs ou textos livres do arquivo. O JSON original e os campos de configuração da janela continuam contendo identificadores locais.

## Publicação e verificações

`ProbeRunner` aceita um `ChannelWriter<ProbeSample>` opcional. A publicação usa `TryWrite`, sem aguardar a janela. A apresentação cria um canal sem continuações síncronas, consome na thread gráfica e encerra esse consumo antes de concluir a operação. Canal cheio ou fechado não impede a aquisição; a sessão retornada continua sendo a fonte completa para persistência e histórico final.

Ambiente de verificação: Windows x64, SDK .NET 10.0.402 e runtime .NET 10.0.13. Compilação Release com 0 avisos/erros e **184 testes aprovados**, incluindo 11 novos casos sobre publicação, canal cheio/fechado, atualização antes do fim da sessão, ausência, privacidade, ordem de conclusões e interrupção da linha. Os testes novos usam adaptadores e registros simulados.

## Inspeção física em 2026-10-20

Executada nesta máquina Windows x64 com MediaTek Wi-Fi 6 MT7921, driver Wi-Fi 3.5.0.1349 e runtime .NET 10.0.13. A aba exibiu RSSI e pontos antes do fim da aquisição. A sessão examinada terminou com 60 posições despachadas, sem descarte por atraso, e 57 valores de RSSI entre −56 e −54 dBm (média descritiva −54,60 dBm).

Houve 51 valores da consulta principal e seis da alternativa de enlace único. Nas seis posições alternativas, o JSON preservou o erro 27 da consulta principal. Essa execução é evidência física da alternativa nesse perfil, sem comprovar MLO ou compatibilidade com outros drivers. As três posições sem métricas tinham uma ocorrência `ObservationTimeout` e duas `Busy`, sem RSSI/canal/frequência preenchidos.

A fonte RSSI variou e o histórico interrompeu a união entre fontes. O ICMP registrou 59 respostas com sucesso e um erro local de rota indisponível; isso não foi apresentado como RSSI zero. O analisador do console concluiu com código 0, confirmou os contadores e o SHA-256 permaneceu igual após a análise. O JSON bruto permanece local em `results/`, ignorado pelo Git.

A frequência de atualização, a calibração e a precisão do RSSI dependem do driver. Constância em `−60 dBm`, por si só, não demonstra erro nem calibração. Este indicador não diagnostica cobertura ou interferência e não faz teste de velocidade.
