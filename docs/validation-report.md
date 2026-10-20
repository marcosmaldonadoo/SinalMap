# Evidência de validação automatizada — Fase 0

O incremento WPF autorizado em 2026-10-20 tem compilação, testes e inspeção física registrados em [validação do piloto WPF](validacao-wpf.md). Os resultados abaixo preservam a evolução da Fase 0.

Data: 2026-10-17.

Ambiente desta implementação: Ubuntu 24.04, linux-x64; SDK .NET 10.0.402 e runtime .NET 10.0.13.

## Atualização de 2026-10-20 — contexto no resumo local

Ambiente desta atualização: Windows x64; SDK .NET 10.0.402 e runtime .NET 10.0.13. A mudança permanece na Fase 0 e acrescenta indicações de contexto variável ou indeterminado aos resumos de `--analyze` e `--compare`.

- `dotnet build SinalMap.sln -c Release --no-restore`: aprovado, **0 avisos e 0 erros**.
- `dotnet test SinalMap.sln -c Release --no-build --no-restore`: **153 testes aprovados, 0 falhas e 0 ignorados**, incluindo os 136 anteriores e 17 novos casos.
- Os novos testes usam registros simulados para variação de interface, SSID, BSSID, canal, frequência e fonte RSSI; valores ausentes/não reconhecidos; SSID não UTF-8; formatos equivalentes de BSSID; observações desconectadas; e ausência de RSSI.
- Os testes dos comandos verificam os avisos, a preservação das estatísticas, a omissão de ambas as identidades de rede e a preservação do JSON original. A regressão cobre os critérios já existentes para calcular diferenças entre sessões.

Não foi executado teste em Linux nesta atualização. O formato JSON, o agendamento e o tráfego ICMP permanecem os existentes. As evidências abaixo de 2026-10-17 são históricas e mantêm seus próprios ambientes e datas.

### Coleta física complementar em 2026-10-20

Após os testes automatizados, foi executada uma coleta real de 60 segundos nesta máquina Windows x64, com MediaTek Wi-Fi 6 MT7921 e driver Wi-Fi 3.5.0.1349, usando o gateway IPv4 verificado da interface conectada. O executável Release utilizou .NET 10.0.13.

- Coleta concluída com código 0; 60 posições despachadas e 60 sequências distintas, sem descarte por atraso ou cancelamento.
- 60 leituras Wi-Fi conectadas e 60 respostas ICMP com sucesso, sem tentativa desconhecida ou ocorrência registrada.
- RTT: mínimo 2 ms, média 3,23 ms e máximo 5 ms.
- RSSI: −60 dBm nas 60 posições, com fonte principal; canal e frequência em cache disponíveis nas 60 posições.
- `--analyze` concluído com código 0; todos os campos de contexto indicaram um único valor nos registros considerados, sem mudanças de BSSID registradas.
- Hash SHA-256 do arquivo antes e depois da análise conferido: bytes preservados. O JSON permanece local em `results/`, ignorado pelo Git; identificadores de rede e o arquivo bruto não integram esta evidência pública.

Esta execução exercitou a coleta e a apresentação do resumo de contexto em hardware real, no caso sem variação registrada. Os avisos de contexto variável ou indeterminado continuam verificados por testes simulados nesta atualização. RSSI constante não comprova calibração, resposta à distância ou ausência de mudanças entre consultas. Não foram repetidos os cenários físicos de cancelamento, permissão negada, reconexão ou rejeição de rota; as pendências do roteiro manual permanecem.

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

Consulte as verificações do PR para o estado do commit mais recente. O [parecer da Fase 0](parecer-fase-0.md) consolida a evidência real e simulada e mantém o aceite do responsável como requisito para o avanço de fase. O [roteiro manual no Windows](windows-validation.md) preserva os casos não exercitados; o CI aprovado não os transforma em testes físicos realizados.
