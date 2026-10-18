# Evidência de validação automatizada — Fase 0

Data: 2026-10-17.

Ambiente desta implementação: Ubuntu 24.04, linux-x64; SDK .NET 10.0.402 e runtime .NET 10.0.13.

## Resultados executados

- Solução completa em Release: compilação aprovada, **0 avisos e 0 erros**.
- xUnit após acrescentar a comparação local: **136 testes aprovados, 0 falhas e 0 ignorados** (99 anteriores e 37 novos).
- Console `--help`: código de saída 0, instruções exibidas.
- Console `--list` em Linux: código de saída 2 e explicação da exigência do Windows, sem tentar carregar `wlanapi.dll`.
- Destino em formato hexadecimal: código de saída 2 e rejeição antes da coleta.
- Console `--analyze` em Linux com o JSON real já fornecido: código 0, estatísticas coerentes com a análise anterior e nenhuma correspondência com os 432 valores privados conferidos. O arquivo e seus identificadores permanecem fora do repositório.
- Console `--analyze` sem caminho: código 2. Os testes também verificam erros de leitura/formato e cancelamento da análise.
- Console `--compare` em Linux com arquivos sintéticos exclusivos de teste: código 0, diferenças e unidades conferidas, contextos incompatíveis representados, identificadores omitidos e bytes dos arquivos preservados. A mesma sessão informada duas vezes retorna código 1; argumentos incompletos retornam 2.

O CI também foi executado no GitHub para o commit `6df6dac93c9f6545104b91dfe5274825a32056f3`: compilação e testes simulados aprovados em **Linux e Windows**. No Windows, os logs confirmam **0 avisos, 0 erros e 56 testes aprovados**. A evidência está na [execução do PR](https://github.com/marcosmaldonadoo/SinalMap/actions/runs/39049062817).

A atualização de compatibilidade RSSI, commit `23941e67b6300981b98d70a8831ec7602d821ee6`, também teve compilação e testes aprovados em Linux e Windows: [execução do CI](https://github.com/marcosmaldonadoo/SinalMap/actions/runs/39055778918).

Comandos usados para compilação e testes neste ambiente:

```text
dotnet build SinalMap.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test SinalMap.sln -c Release --no-build --no-restore -m:1 --logger trx
```

As opções de processo único/compilador isolado foram usadas por compatibilidade com o ambiente de execução. Não alteram o código do produto.

## Cobertura de comportamento

Os testes verificam agenda absoluta de 60 posições, conclusão da janela, atraso e descarte sem rajada, operações assíncronas pendentes, mudanças do relógio civil, cancelamento parcial, erros de adaptadores, mudanças de BSSID e distinção de SSIDs, rota incompatível sem envio, RTT nulo em falha e RTT de zero válido em sucesso.

A fronteira WLAN é substituída por uma implementação que aloca buffers nativos simulados. São exercitados layouts ABI, listas vazias, retorno nulo, dados truncados, contagens incompatíveis com o tamanho BSS, SSID inválido em tamanho ou não UTF-8, campo sem suporte, permissões negadas, mudança durante a leitura, propriedade do handle durante timeout e liberação dos recursos.

A atualização RSSI acrescenta testes do retorno 27, da alternativa oficial com um único enlace, da origem do dado, de falhas simultâneas, de buffers e contagens inválidas, da restrição em MLO/vários enlaces e do descarte em mudança de conexão. O suporte da alternativa no adaptador real continua pendente de nova coleta.

O armazenamento é testado quanto a serialização de valores nulos, metadados monotônicos, posições, recusa de sobrescrita e limpeza em cancelamento da escrita. A seleção de interface e a rejeição de destinos ambíguos também são verificadas.

A análise local acrescenta testes de RTT zero e ausente, exclusão de falhas da média de latência, estatísticas de RSSI e suas fontes, tentativa ICMP desconhecida, rota rejeitada e contagem de posições por ocorrência sem duplicação. O leitor e o comando são exercitados com arquivos inválidos, campos obrigatórios ausentes, enumerações inválidas, posição nula, limite de tamanho, cancelamento e preservação dos bytes originais. Os testes de privacidade verificam a omissão de identificadores, caminhos e textos livres.

A comparação acrescenta testes das diferenças B − A, da unidade dB para diferença de RSSI, de RTT zero, de valores ausentes e de contextos distintos, desconhecidos ou variáveis. São exercitados SSID em bytes não UTF-8, formatos equivalentes de BSSID, fontes diferentes/não reconhecidas, sessões duplicadas, leitura inválida do segundo arquivo sem saída parcial, cancelamento e privacidade dos dois arquivos. Esses casos usam adaptadores e registros simulados; não constituem duas novas coletas físicas.

## O que esta evidência não valida

Não foi feita coleta RF neste ambiente Linux. O usuário forneceu um JSON de uma execução real no Windows, e sua análise confirmou a coleta básica com 60 respostas ICMP, RSSI de −67 dBm, canal e frequência em todas as amostras, sem erros registrados. A fonte RSSI foi a consulta principal, e a alternativa não foi utilizada: consulte a [validação da coleta estável](validacao-coleta-estavel.md).

A [validação manual complementar](validacao-manual-complementar.md) registra evidência fornecida posteriormente pelo responsável no Windows, com os casos executados e seus limites. Essa rodada foi examinada pelos resumos e inspeções locais relatados, sem recebimento dos JSONs brutos; não foi executada no ambiente Linux nem substituída pelos testes simulados.

A precisão/calibração do driver, a resposta do RSSI à mudança física de local, roaming físico e caminho efetivo dos pacotes continuam dependendo dos casos de validação. As pendências atuais constam no roteiro manual. O [registro da primeira execução](primeira-coleta-windows.md) preserva o contexto do retorno 27, que não reapareceu no arquivo analisado.

Consulte as verificações do PR para o estado do commit mais recente. Mesmo com o CI aprovado em Linux e Windows, o [roteiro manual no Windows](windows-validation.md) continua obrigatório antes de iniciar WPF.
