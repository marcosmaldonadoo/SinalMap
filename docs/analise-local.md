# Análise local de uma coleta

O comando `--analyze` lê um JSON já salvo e imprime um resumo no console. Funciona em Windows e Linux, sem consultar interfaces Wi-Fi ou enviar ICMP. O arquivo original permanece intacto e nenhum relatório é enviado a serviços externos.

```powershell
dotnet run --project src/SinalMap.Console.Probe -c Release -- --analyze results/ponto-01.json
```

Para um caminho com espaços, use aspas. A análise é uma operação separada da coleta; não combina com `--target`, `--interface`, `--list` ou `--output`.

## Como interpretar o resumo

- Posições despachadas, descartadas por atraso ou canceladas antes do despacho são contadas separadamente.
- A duração utiliza os ticks monotônicos e a frequência declarada no arquivo. Não usa a diferença entre horários UTC.
- Chamadas ICMP confirmadas, tentativas desconhecidas e cada resultado ICMP são apresentados separadamente. Uma posição sem registro ICMP não é uma tentativa desconhecida.
- Mínimo, média e máximo de RTT usam somente respostas com sucesso. RTT de `0 ms` é válido; ausência de RTT não é zero.
- Mínimo, média e máximo de RSSI usam os valores registrados, sem conversão de qualidade percentual ou estimativa. A disponibilidade informa quantos valores existem; as fontes principal, alternativa e outra/não identificada são contadas separadamente.
- Canal e frequência têm contadores próprios. A frequência só é contada como cache BSS quando sua fonte corresponde à usada pelo coletor; outras fontes não recebem esse rótulo automaticamente.
- As mudanças de BSSID são as entradas registradas na sessão. O resumo não determina o instante exato ou a duração do roaming.
- O contexto informa, para interface, SSID em bytes, BSSID, destino ICMP, canal, frequência em cache e fonte RSSI, se há um único valor nos registros considerados, variação ou informação indeterminada. Não imprime os valores desses campos.
- Ocorrências são agrupadas por tipo e código nativo, contando posições afetadas. A mesma categoria/código repetida em vários campos de uma posição conta uma vez. Uma posição pode aparecer em várias categorias.

Quando não há valores de uma métrica, a saída informa essa ausência. As estatísticas são cálculos descritivos dos registros, sem classificação automática de saúde da rede, estimativa de cobertura ou certificação da precisão do driver.

As estatísticas são globais da sessão, sem agrupamento por SSID ou BSSID. O resumo agora avisa quando o contexto varia nos registros; os valores podem abranger redes, APs ou condições de medição diferentes. A indicação é obtida dos snapshots, independentemente da lista de eventos de BSSID. Examine os snapshots e eventos locais antes de interpretar médias como resultado de um ponto de teste. Os dois resumos apresentados por `--compare` também incluem essas indicações.

Para o contexto de rádio, são consideradas somente observações Wi-Fi conectadas. A interface inclui também o GUID do cabeçalho; o destino ICMP vem do cabeçalho da sessão. O SSID é comparado em bytes, sem depender de sua conversão para texto, e formatos equivalentes de BSSID são normalizados. Canal e frequência exigem fontes reconhecidas pelo coletor. A fonte RSSI é examinada somente nas observações que contêm valor de RSSI.

Dois ou mais valores reconhecidos distintos indicam variação, mesmo que existam lacunas adicionais. Se não há valores, ou há valor ausente, inválido ou de fonte não reconhecida sem variação demonstrada, o campo fica indeterminado. Ausência não comprova igualdade nem mudança. Uma observação desconectada ou indisponível não fornece uma identidade antiga como contexto novo; os contadores de conexão e de disponibilidade continuam separados.

Um único valor nos registros considerados não comprova conexão contínua, ausência de roaming entre consultas ou estabilidade física. As médias continuam sendo exibidas como estatísticas descritivas globais; não são alteradas, segmentadas nem convertidas em diagnóstico por esses avisos.

## Privacidade

O resumo omite SSID, BSSID, IPs, GUIDs, identificador da sessão, nome do adaptador, sistema operacional, horários absolutos, caminho do arquivo e textos livres das ocorrências. Nomes técnicos dos tipos de ocorrência e códigos numéricos são mantidos. Fontes não reconhecidas são contadas sem imprimir seu conteúdo.

O JSON original continua contendo identificadores. Executar a análise não o anonimiza nem autoriza sua publicação. A saída padrão contém somente o resumo; erros de leitura não repetem caminhos ou trechos do JSON. O terminal e a CLI do .NET podem registrar o comando digitado, inclusive o caminho informado.

## Formato e limites

São aceitos arquivos do formato atual (`schemaVersion: 1`), com até 4 MiB e profundidade JSON de até 32 níveis. Campos obrigatórios ausentes, tipos inválidos, enumerações numéricas/desconhecidas, posições nulas ou sequências repetidas são rejeitados. Frequência monotônica inválida, duração negativa e combinações inconsistentes de resultado/RTT também são rejeitadas.

Essas verificações são básicas. O resumo não audita toda a agenda, todos os timestamps de cada operação, a correspondência física de rotas, a calibração ou a autenticidade do arquivo. Um JSON editado manualmente pode conter valores plausíveis que não foram medidos. Os testes reais do [roteiro de validação](windows-validation.md) continuam necessários.

Códigos de saída da análise: `0` arquivo lido e resumido, inclusive quando a sessão original foi cancelada ou contém falhas; `1` erro de leitura, formato incompatível ou dados inconsistentes; `2` argumentos inválidos; `130` análise cancelada pelo usuário. O código `0` não constitui aceite da coleta nem diagnóstico da rede.

Para examinar locais diferentes, use também o comando `--compare` descrito no [guia de comparação de coletas](comparacao-coletas.md). Ele apresenta os dois resumos, verifica os contextos registrados e calcula diferenças somente quando seus requisitos são atendidos. Registre as condições de cada ponto; não exija uma variação predeterminada de RSSI.
