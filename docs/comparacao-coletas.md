# Comparar duas coletas locais

O comando `--compare` lê dois JSONs salvos, apresenta seus resumos e compara os contextos registrados. Funciona em Windows e Linux, preserva os arquivos e não consulta Wi-Fi nem envia ICMP.

```powershell
dotnet run --project src/SinalMap.Console.Probe -c Release -- --compare results/ponto-a.json results/ponto-b.json
```

O primeiro arquivo corresponde à coleta **A**, e o segundo à **B**. Os nomes e caminhos não aparecem no relatório. Use aspas nos caminhos com espaços. `--compare` é uma operação separada: não combina com `--analyze`, `--target`, `--interface`, `--list` ou `--output`.

## Contextos e diferenças

O relatório compara interface, SSID em bytes, BSSID, destino IPv4, canal, frequência em cache BSS e fonte RSSI. Mostra somente se cada contexto é igual, diferente, indeterminado ou se variou dentro de pelo menos uma coleta.

A comparação usa as observações Wi-Fi em estado conectado. Uma informação ausente ou não reconhecida não é considerada igual a outra informação ausente. Variações conhecidas dentro de uma coleta são indicadas, mesmo que também existam lacunas. Estados desconectados/indisponíveis continuam visíveis nos contadores de cada resumo; identidade igual nas amostras não prova continuidade da conexão entre elas.

| Diferença | Condições para calcular | Unidade e interpretação |
|---|---|---|
| Média de RTT | Mesma interface e SSID registrados; mesmo destino IPv4 canônico; pelo menos uma resposta com sucesso em cada coleta | Milissegundos, **B − A**. Valor positivo representa maior RTT médio registrado em B |
| Média de RSSI | Mesma interface, SSID e BSSID registrados; mesmo canal com fonte nativa reconhecida; mesma frequência com fonte de cache BSS reconhecida; mesma fonte RSSI reconhecida e uniforme; pelo menos um RSSI em cada coleta | **dB**, **B − A**. Os valores originais continuam em dBm. Valor negativo representa uma média de RSSI menor em B |

Se os requisitos não forem atendidos, a diferença aparece como **não calculada**, mantendo disponíveis os resumos individuais. Fontes RSSI principal e alternativa distintas ou misturadas não são tratadas como equivalentes automaticamente. Canal/frequência com fonte desconhecida não tornam os contextos conhecidos.

SSID é comparado pelos bytes hexadecimais, inclusive quando não pode ser representado em UTF-8; não se presume igualdade a partir do texto exibido. GUID vazio, SSID vazio/inválido, BSSID inválido ou zerado e destino não canônico tornam o respectivo contexto indeterminado. A comparação de interface também confere os GUIDs das observações conectadas contra o cabeçalho.

Os dois arquivos precisam ter identificadores de sessão não vazios e diferentes. Copiar ou renomear uma coleta não cria uma segunda sessão válida para esta comparação. Não altere identificadores para simular testes.

## Limites de interpretação

As diferenças são estatísticas descritivas dos registros. Não isolam o efeito do local: horário, carga, interferência, mudanças no AP e outras condições também podem mudar os resultados. BSSID diferente não impede a diferença de RTT quando os requisitos dessa métrica são atendidos; o contexto diferente permanece indicado.

SSID e destino IPv4 iguais não comprovam que a infraestrutura ou o caminho de rede tenham permanecido os mesmos. Os indicadores comparam campos registrados, sem identificar globalmente uma rede ou um host.

Compare os tamanhos das amostras, durações, cancelamentos, falhas e fontes. As coletas podem ter durações e quantidades de valores diferentes. O programa não gera nota de qualidade, perda de pacotes estimada, previsão de cobertura ou teste de significância estatística. Uma diferença zero não prova equivalência de condições.

A frequência continua sendo informação em cache, sem garantia de uma recepção nova por segundo. O comando não verifica autenticidade dos arquivos, captura física de pacotes, calibração do driver nem movimento real entre pontos. Os testes do [roteiro no Windows](windows-validation.md) continuam necessários.

## Privacidade e códigos de saída

O resultado segue as regras de privacidade do [resumo local](analise-local.md): omite identificadores, horários absolutos, caminhos, nomes do ambiente e textos livres. Informações de identidade são usadas apenas em memória para comparar os contextos. Os JSONs originais continuam contendo seus identificadores; a comparação não os anonimiza.

Cada arquivo está sujeito aos limites e verificações do leitor da análise local: formato 1, até 4 MiB e profundidade de até 32 níveis. Os dois são lidos e examinados antes de imprimir o relatório. Um segundo arquivo inválido não produz um resumo parcial do primeiro.

Códigos de saída: `0` comparação concluída, inclusive com diferenças não calculadas ou sessões originalmente canceladas; `1` erro de leitura/dados ou arquivos da mesma sessão; `2` argumentos inválidos; `130` comparação cancelada. O código `0` não aprova a coleta nem o avanço à interface gráfica.
