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
- Ocorrências são agrupadas por tipo e código nativo, contando posições afetadas. A mesma categoria/código repetida em vários campos de uma posição conta uma vez. Uma posição pode aparecer em várias categorias.

Quando não há valores de uma métrica, a saída informa essa ausência. As estatísticas são cálculos descritivos dos registros, sem classificação automática de saúde da rede, estimativa de cobertura ou certificação da precisão do driver.

## Privacidade

O resumo omite SSID, BSSID, IPs, GUIDs, identificador da sessão, nome do adaptador, sistema operacional, horários absolutos, caminho do arquivo e textos livres das ocorrências. Nomes técnicos dos tipos de ocorrência e códigos numéricos são mantidos. Fontes não reconhecidas são contadas sem imprimir seu conteúdo.

O JSON original continua contendo identificadores. Executar a análise não o anonimiza nem autoriza sua publicação. A saída padrão contém somente o resumo; erros de leitura não repetem caminhos ou trechos do JSON. O terminal e a CLI do .NET podem registrar o comando digitado, inclusive o caminho informado.

## Formato e limites

São aceitos arquivos do formato atual (`schemaVersion: 1`), com até 4 MiB e profundidade JSON de até 32 níveis. Campos obrigatórios ausentes, tipos inválidos, enumerações numéricas/desconhecidas, posições nulas ou sequências repetidas são rejeitados. Frequência monotônica inválida, duração negativa e combinações inconsistentes de resultado/RTT também são rejeitadas.

Essas verificações são básicas. O resumo não audita toda a agenda, todos os timestamps de cada operação, a correspondência física de rotas, a calibração ou a autenticidade do arquivo. Um JSON editado manualmente pode conter valores plausíveis que não foram medidos. Os testes reais do [roteiro de validação](windows-validation.md) continuam necessários.

Códigos de saída da análise: `0` arquivo lido e resumido, inclusive quando a sessão original foi cancelada ou contém falhas; `1` erro de leitura, formato incompatível ou dados inconsistentes; `2` argumentos inválidos; `130` análise cancelada pelo usuário. O código `0` não constitui aceite da coleta nem diagnóstico da rede.

Para examinar locais diferentes, use também o comando `--compare` descrito no [guia de comparação de coletas](comparacao-coletas.md). Ele apresenta os dois resumos, verifica os contextos registrados e calcula diferenças somente quando seus requisitos são atendidos. Registre as condições de cada ponto; não exija uma variação predeterminada de RSSI.
