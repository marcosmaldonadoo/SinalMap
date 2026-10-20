# Validação do piloto WPF técnico

Escopo autorizado pelo responsável em 2026-10-20 após a revisão da proposta e a coleta complementar da Fase 0. O aceite inclui os limites do [parecer](parecer-fase-0.md); os cenários físicos ausentes continuam pendentes.

## Execução

```powershell
dotnet build SinalMap.sln -c Release
dotnet test SinalMap.sln -c Release --no-build --no-restore
dotnet run --project src/SinalMap.Desktop.Wpf -c Release --no-build
```

O aplicativo usa .NET 10 Desktop e Windows. A pasta padrão é `Documentos/SinalMap/results`; a caixa de texto e o diálogo permitem mudar esse caminho. Não há destino predefinido. Selecione explicitamente uma interface conectada quando houver mais de uma e informe um IPv4 autorizado.

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

O roteiro abaixo continua disponível para repetição. O redimensionamento até o mínimo, a navegação completa por teclado e a seleção pelo diálogo de pasta ainda exigem inspeção específica; o teste local informou a pasta pela caixa de texto.

1. Abrir a janela, conferir seleção da interface e pasta; tentar iniciar com destino inválido e confirmar a recusa.
2. Escolher um IPv4 autorizado e uma pasta local, iniciar e aguardar uma coleta de 60 s. Conferir resumo, amostras, contexto e ocorrências.
3. Analisar o JSON da janela com `SinalMap.Console.Probe --analyze` e comparar contadores, valores e origem das métricas.
4. Iniciar outra sessão e cancelar após alguns segundos; conferir o JSON parcial e as posições canceladas antes do despacho.
5. Iniciar uma sessão e fechar a janela durante a aquisição; confirmar que ela aguarda e salva antes de encerrar.
6. Escolher uma pasta sem escrita em uma sessão de teste, cancelar e conferir que a janela retém os dados; mudar para pasta gravável e tentar salvar novamente. Não alterar permissões do sistema para criar esse cenário.
7. Abrir um JSON salvo pelo diálogo ou `Ctrl+O`; conferir que não ocorre nova aquisição e que o arquivo permanece intacto.
8. Conferir legibilidade, navegação por teclado, rolagem das abas/tabela e redimensionamento até o tamanho mínimo.

## Referências

A afinidade de thread segue o [modelo de threads do WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model). O diálogo de pasta usa [OpenFolderDialog](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.openfolderdialog?view=windowsdesktop-10.0).
