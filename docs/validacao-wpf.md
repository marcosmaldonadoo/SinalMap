# Validação do piloto WPF técnico

Escopo autorizado pelo responsável em 2026-10-20 após a revisão da proposta e a coleta complementar da Fase 0. O aceite inclui os limites do [parecer](parecer-fase-0.md); os cenários físicos ausentes continuam pendentes.

## Execução

```powershell
dotnet build SinalMap.sln -c Release
dotnet test SinalMap.sln -c Release --no-build --no-restore
dotnet run --project src/SinalMap.Desktop.Wpf -c Release --no-build
```

O aplicativo usa .NET 10 Desktop e Windows. A pasta padrão é `Documentos/SinalMap/results`; a caixa de texto e o diálogo permitem mudar esse caminho. A revisão de monitoramento contínuo sugere o gateway IPv4 da interface selecionada; confira o destino antes do teste. Selecione explicitamente uma interface conectada quando houver mais de uma.

## Comportamento entregue

- A apresentação usa `INotifyPropertyChanged` e `ICommand`, sem biblioteca de MVVM ou dependência de WPF. Os diálogos pertencem à janela. As continuações que atualizam bindings preservam o contexto da thread gráfica; aquisição e gravação executam fora dela.
- Uma operação impede início, leitura ou atualização concorrentes. A barra indica atividade, sem porcentagens fictícias. A aba Sinal exibe as leituras reais concluídas durante a coleta; resumo e tabela completa aparecem após o retorno do coletor. O [registro do sinal ao vivo](sinal-ao-vivo.md) descreve a extensão solicitada após o primeiro piloto.
- `Cancelar e salvar parcial` cancela a aquisição e aguarda suas operações. A gravação usa token independente. Fechar a janela segue o mesmo fluxo e só encerra depois da gravação.
- Falha de gravação mantém a mesma sessão em memória, bloqueia novas coletas/abertura e permite escolher outra pasta e salvar novamente. A janela recusa fechar com dados pendentes; descarte exige confirmação. Encerramento forçado ou queda de energia continuam fora da recuperação.
- A análise de um JSON usa o leitor e os critérios do console. As amostras apresentam RTT somente em sucesso, indisponibilidade explícita, fonte RSSI reconhecida e frequência em cache somente quando a origem corresponde ao coletor.
- Contextos variáveis/indeterminados, métricas disponíveis e ocorrências por tipo/código ficam explícitos. O resumo e as amostras não mostram SSID, BSSID, IP, GUID nem mensagens livres do arquivo. O caminho local e a descrição do adaptador podem aparecer na janela; o JSON original conserva identificadores.
- O formato JSON, a agenda, o timeout, as APIs e a checagem de rota são os existentes. A janela não altera permissões, rádio ou rotas.

## Verificações automatizadas

Ambiente local: Windows x64, SDK .NET 10.0.402, runtime .NET 10.0.13. Compilação Release da solução com 0 avisos e 0 erros; 173 testes aprovados (153 anteriores e 20 novos casos), sem falhas ou ignorados.

Os novos testes simulados cobrem validação do destino, múltiplas interfaces, reentrada de comando, início duplicado, cancelamento, fechamento durante aquisição/gravação, falha de armazenamento, recuperação em outra pasta, confirmação de descarte, abertura local, arquivo inválido, métricas ausentes, permissão negada, rota rejeitada, erro ICMP, contexto variável e privacidade da projeção. Eles não comprovam esses cenários no rádio real.

O CI foi configurado para compilar toda a solução no Windows e somente o projeto de testes com suas dependências no Linux. A execução remota e a validação Linux desta revisão ainda não foram realizadas.

## Roteiro de inspeção no Windows

Inspeção executada em 2026-10-20, nesta máquina Windows x64 com MediaTek Wi-Fi 6 MT7921, driver Wi-Fi 3.5.0.1349 e runtime .NET 10.0.13:

- Janela aberta, adaptador conectado reconhecido, pasta e destino informados pelos campos; aquisição assíncrona com controles de início/abertura indisponíveis durante a operação.
- Uma coleta completa pela janela: 60 posições despachadas, 60 respostas ICMP e 60 leituras Wi-Fi, sem descarte por atraso ou ocorrências. RTT mínimo 3 ms, média 3,48 ms e máximo 6 ms. RSSI −60 dBm nas 60 posições, fonte principal; canal e frequência em cache presentes em todas.
- O JSON dessa coleta foi examinado com `--analyze`: código 0, resumo coerente com a janela, contexto único nos registros e SHA-256 preservado após a análise.
- Cancelamento pelo botão: 28 posições despachadas e 32 canceladas antes do despacho, 28 respostas ICMP, arquivo parcial salvo com `cancelled=true`.
- Fechamento pelo botão da janela durante outra aquisição: a janela encerrou após salvar uma sessão parcial, com 28 posições despachadas, 32 canceladas antes do despacho e 28 respostas ICMP.
- Recusa de início com destino vazio conferida na versão final. Abertura de JSON pelo diálogo nativo conferida, com os mesmos 60 registros da coleta completa; o campo de destino permaneceu vazio durante a análise.
- Resumo, contexto e tabela de amostras inspecionados; os JSONs permanecem locais em `results/`, ignorados pelo Git. Identificadores e capturas com caminhos privados não foram incluídos no repositório.

Os cenários de falha de armazenamento/nova tentativa e fechamento durante gravação pendente estão comprovados por simulação; não foi provocada falha física de disco. A inspeção não repete os cenários de permissões, roaming, MLO, campo sem suporte ou rota alternativa da Fase 0.

A extensão posterior da aba Sinal tem **184 testes aprovados** e uma coleta física própria, com atualização de RSSI durante a aquisição e leituras da consulta alternativa. Consulte o [registro do sinal ao vivo](sinal-ao-vivo.md) para os valores, ausências, fontes e limites dessa execução.

## Aperfeiçoamento visual de 2026-10-20

A revisão organiza a configuração em uma coluna lateral, mantém os comandos de início/cancelamento acessíveis e destaca a última leitura, os extremos registrados e a disponibilidade de RSSI. As abas têm espaçamento uniforme; a tabela aproxima RSSI e RTT das primeiras colunas. O gráfico permite inspecionar uma posição com o mouse, exibindo tempo e leitura, inclusive ausência. O indicador diferencia histórico, coleta ao vivo e espera. Detalhes, fontes e limites continuam disponíveis em uma seção expansível.

Na mesma máquina Windows x64, com SDK .NET 10.0.402 e runtime 10.0.13, a versão final compilou em Release com 0 avisos e 0 erros; os **184 testes** existentes passaram, sem falhas ou ignorados. Essa revisão altera a apresentação, preservando aquisição, salvamento e formato JSON.

Inspeção visual: janela padrão de 1240 × 850 e inicialização temporária no mínimo de 1000 × 700, com leitura e comando de início acessíveis. A expansão dos detalhes no tamanho mínimo foi conferida com rolagem até o final. A inspeção de ponto exibiu tempo e RSSI reais do histórico. A abertura por `Ctrl+O` e diálogo nativo reutilizou uma coleta anterior de 60 posições: 57 com RSSI, mínimo −56 dBm, máximo −54 dBm e última leitura −56 dBm, com lacunas preservadas. Não foi feita nova coleta física para esta revisão visual. Capturas com caminhos locais não foram adicionadas ao repositório.

O roteiro abaixo continua disponível para repetição. A navegação completa por teclado e a seleção pelo diálogo de pasta ainda exigem inspeção específica; o teste local informou a pasta pela caixa de texto.

1. Abrir a janela, conferir seleção da interface e pasta; tentar iniciar com destino inválido e confirmar a recusa.
2. Escolher um IPv4 autorizado e uma pasta local, iniciar e aguardar uma coleta de 60 s. Conferir resumo, amostras, contexto e ocorrências.
3. Analisar o JSON da janela com `SinalMap.Console.Probe --analyze` e comparar contadores, valores e origem das métricas.
4. Iniciar outra sessão e cancelar após alguns segundos; conferir o JSON parcial e as posições canceladas antes do despacho.
5. Iniciar uma sessão e fechar a janela durante a aquisição; confirmar que ela aguarda e salva antes de encerrar.
6. Escolher uma pasta sem escrita em uma sessão de teste, cancelar e conferir que a janela retém os dados; mudar para pasta gravável e tentar salvar novamente. Não alterar permissões do sistema para criar esse cenário.
7. Abrir um JSON salvo pelo diálogo ou `Ctrl+O`; conferir que não ocorre nova aquisição e que o arquivo permanece intacto.
8. Conferir legibilidade, navegação por teclado, rolagem das abas/tabela e redimensionamento até o tamanho mínimo.

## Monitoramento sem teste e sugestão de gateway

A revisão posterior permite acompanhar RSSI ao abrir a janela, sem iniciar coleta, informar destino ou enviar ICMP. O monitor mantém uma janela recente limitada, pausa para coleta/abertura de histórico e é encerrado ao fechar. O gateway IPv4 vem da configuração Windows do mesmo GUID de interface, com destino editável e sem escolha arbitrária entre candidatos distintos. A amostra Wi-Fi da coleta é publicada sem aguardar ICMP, preservando a amostra completa para salvamento.

No mesmo ambiente Windows x64/.NET 10, compilação Release sem avisos/erros e **200 testes aprovados** (16 casos adicionais simulados). A inspeção local da janela mostrou o modo **MONITORANDO**, contagem e horário avançando antes de iniciar qualquer teste e gateway preenchido. Foi uma inspeção passiva de leituras, sem nova sessão de 60 s ou comparação de calibração com outro aplicativo. Os cenários de troca de interface e transição para coleta estão cobertos por simulação; não foram provocados fisicamente nesta revisão.

## Distribuição 1.0.0

Os pacotes portáteis Windows x64 foram publicados localmente com .NET 10.0.13 incluído, extraídos e inspecionados. O executável WPF extraído abriu e exibiu leituras/varreduras; o console executou `--help` com código 0. A versão de arquivo é 1.0.0.0 e a versão de produto é 1.0.0. O conteúdo desktop do pacote final é idêntico ao que foi inspecionado. Os ZIPs não incluem coletas, código-fonte ou PDBs; seus hashes SHA-256 acompanham a release. A compilação final da solução teve 0 avisos/erros e os 218 testes passaram no Windows local.

## Revisão de UI/UX de 2026-10-21

A configuração foi compactada, com destino validado junto ao campo e pasta de resultados expansível. O cabeçalho diferencia acompanhamento ao vivo, operação e histórico. A visão geral destaca RSSI e extremos; as demais abas aparecem quando há resultado. O progresso da coleta representa posições processadas, incluindo descarte por atraso e excluindo posições futuras canceladas, sem estimar progresso temporal. Os comandos de coleta e salvamento parcial permanecem fora da área rolável.

O gráfico usa divisões regulares na escala de dBm, preenchimento separado por segmento e seleção por mouse ou teclado (`←`, `→`, `Home`, `End`, `Esc`). Ausências, mudança de contexto e recuo de tempo continuam interrompendo a linha e o preenchimento. Os detalhes técnicos e as mensagens de varredura ficam disponíveis em uma seção expansível. A tabela mantém posição, RSSI e RTT fixos na rolagem horizontal; as tabelas permitem copiar a seleção com os cabeçalhos.

Verificação no Windows x64, SDK .NET 10.0.402 e runtime 10.0.13:

- Compilação Release da solução sem avisos ou erros e testes simulados: **221 aprovados**, sem falhas ou ignorados. Os casos novos verificam progresso por posições, correção do destino sem enviar pacotes e espera sem leituras quando não há interface conectada; os testes existentes passaram a conferir os estados ao vivo/histórico e o bloqueio do comando com destino inválido. A validação de endereço continua usando os mesmos critérios compartilhados pelo coletor.
- Inspeção na janela padrão: acompanhamento passivo, seletor de adaptador, mensagem junto ao IPv4 inválido e abertura por `Ctrl+O`/diálogo nativo. Um JSON físico anterior foi carregado com 60 posições, RSSI entre −69 e −24 dBm, preservando a interrupção na mudança de contexto. Mouse, `Home`, `→` e `End` exibiram posições e valores correspondentes ao arquivo.
- Renderização local dos componentes WPF com área de 1280 × 800 e 1000 × 640, usando o mesmo JSON anterior, nome genérico de adaptador e portas de aquisição/gravação bloqueadas. Foram inspecionados visão geral, resumo, amostras e detalhes expandidos, incluindo rolagem até o final. Essa verificação de layout não equivale a redimensionar a janela nativa ou comprovar o rádio.
- A captura da janela ficou branca com aceleração gráfica tanto na revisão quanto no executável publicado v1.0.0. A inspeção visual usou renderização por software no processo. A opção `--software-rendering` permite repetir essa alternativa sem alterar o Windows; o modo padrão continua gerenciado pelo WPF. A causa da falha de aceleração não foi determinada.

Não foi realizada nova coleta física de 60 segundos nesta revisão. A inspeção nativa do diálogo de pasta, o redimensionamento e o percurso completo por teclado continuam pendentes; a automação não conseguiu retomar a janela minimizada (`window is minimized`). A renderização final dos componentes não registrou erros de binding. A compilação final usou saída isolada em `artifacts/ui-ux-final` porque a janela aberta mantinha os binários anteriores em uso. Os arquivos e imagens da verificação ficaram locais, fora do controle de versão. O formato JSON, as consultas Wi-Fi, o ICMP e a cadência de varredura não mudaram. Os pacotes já publicados da v1.0.0 permanecem os mesmos.

## Padronização complementar da interface em 2026-10-21

A janela passou a usar uma paleta compartilhada em `Themes/MeasurementTokens.xaml`, com foco ciano, visor grafite e campos levemente rebaixados. A coluna de configuração acompanha o fundo da janela. Controles têm altura mínima de 44 unidades lógicas; textos numéricos usam Consolas, com números tabulares e alinhamento nas tabelas. O cabeçalho diferencia operação, monitoramento e análise local, inclusive na cor do indicador, acompanhada do estado por escrito.

A leitura principal separa o número da unidade dBm e inclui uma régua com marcador do mesmo RSSI válido. A escala se amplia quando necessário; não há conversão de qualidade, categorias de cobertura ou reaproveitamento de um marcador anterior quando falta RSSI. Os extremos ficam em uma linha secundária. O gráfico mantém as lacunas e usa as mesmas cores do visor. A troca de histórico reposiciona a rolagem no topo; amostras recebidas preservam a inspeção. O aviso de salvamento pendente oferece escolha de pasta e nova tentativa diretamente, sem exibir o comando de nova coleta nessa condição.

No mesmo ambiente Windows x64/.NET 10: solução compilada em Release sem avisos ou erros e **221 testes aprovados**, sem falhas ou ignorados. As verificações existentes passaram a conferir também a ausência de valor numérico/marcador, a projeção do RSSI exato e a revisão de histórico estável durante atualizações. A saída final desta revisão fica em `artifacts/interface-design-final`.

Foram renderizados os componentes WPF por software com áreas de 1280 × 800 e 1000 × 640. A inspeção cobriu visão geral, tabela, resumo, detalhes com rolagem, falta de RSSI, destino inválido, progresso e falha de salvamento. Os estados de erro, ausência e operação foram reconstruídos somente no verificador local, com portas de rede/gravação bloqueadas e dados de uma coleta anterior; não representam novas medições ou falhas físicas de disco. Nenhum erro de binding foi registrado. Os limites da inspeção nativa descritos acima continuam vigentes.

## Exibição do SSID em 2026-10-21

A coluna de configuração passou a mostrar **Rede conectada · SSID**, usando o nome textual das consultas Wi-Fi já existentes durante o monitor e a coleta. Não há consulta adicional nem mudança da cadência, das permissões ou do JSON. Troca de interface limpa a observação anterior; respostas canceladas ou de outra interface não fornecem o nome. Leituras de posições anteriores não substituem a identidade de uma posição mais recente da coleta. Desconexão e indisponibilidade retiram o nome anterior; SSID ausente, vazio ou sem representação textual não é reconstruído a partir dos bytes. Ao analisar um arquivo, o campo informa que o monitor está pausado.

Verificação no Windows x64, SDK .NET 10.0.402: compilação Release sem avisos/erros e **226 testes simulados aprovados**, sem falhas ou ignorados. A cobertura inclui nome durante monitor/coleta, mudança de SSID, ausência, desconexão, troca de interface com resposta atrasada e pausa para histórico. Os componentes WPF foram renderizados em 1280 × 800 e 1000 × 640 com SSID fictício e sem erros de binding. Não foi realizada nova coleta física; a renderização local não comprova o comportamento do adaptador real. Saída local em `artifacts/ssid-final` e imagens em `artifacts/ssid-preview`, fora do controle de versão.

## Distribuição 1.1.0

Após a solicitação do responsável para testar o executável, a janela atual foi aberta no Windows real. O SSID ficou visível, o gateway foi sugerido e a contagem avançou de 7 para 18 leituras no monitor, com extremos de −67 e −64 dBm. Essa inspeção foi passiva, sem ICMP, gravação ou nova coleta de 60 s; não comprova calibração.

Os pacotes 1.1.0 foram gerados com .NET 10.0.13 incluído, extraídos e comparados por SHA-256 com os arquivos de publicação. Foram conferidos versão de produto 1.1.0, versão de arquivo 1.1.0.0, licenças, guia e notas. O ZIP desktop contém 406 arquivos; o ZIP de console, 197. Não contêm PDBs, fontes, coletas ou ferramentas locais de desenvolvimento; os JSONs incluídos são somente manifests de dependências/runtime do .NET. Os hashes dos dois ZIPs acompanham a release.

O console extraído executou `--help` com código 0. O aplicativo WPF extraído abriu no modo padrão e mostrou SSID, gateway sugerido e leitura do adaptador; não foi necessário `--software-rendering` nessa inspeção. A falha de aceleração observada antes não foi diagnosticada nem é considerada corrigida. A solução 1.1.0 foi compilada em Release, em saída isolada `artifacts/release-1.1.0`, sem avisos/erros, com **226 testes simulados aprovados**, sem falhas ou ignorados. Permanecem os limites de validação e os cenários físicos pendentes descritos acima.

## Referências

A correção posterior do RSSI estático foi validada com participação do responsável durante deslocamento: 60 leituras entre −69 e −24 dBm e 13 varreduras confirmadas, com aviso de possível impacto no RTT preservado no JSON e na análise. Compilação final sem avisos/erros e **218 testes aprovados**. O responsável confirmou a variação durante o movimento. Consulte o [diagnóstico completo, limites e política autorizada de varredura](correcao-rssi-estatico.md).

A afinidade de thread segue o [modelo de threads do WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model). O diálogo de pasta usa [OpenFolderDialog](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.openfolderdialog?view=windowsdesktop-10.0).
