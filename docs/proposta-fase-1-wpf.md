# Proposta da Fase 1: janela WPF técnica

**Estado: escopo aprovado pelo responsável em 2026-10-20; piloto técnico implementado localmente.** Após a revisão desta proposta e uma nova coleta física, o responsável solicitou o avanço, aceitando o alcance e os limites do [parecer da Fase 0](parecer-fase-0.md). Casos físicos ainda não exercitados continuam identificados como pendentes. O [roteiro WPF](validacao-wpf.md) registra as verificações da janela e as pendências de validação.

## Resultado proposto

Uma janela em português para operar a coleta já validada pelo console e inspecionar seus registros locais. Será a primeira superfície gráfica técnica, com a identidade visual existente como referência. Importação de plantas, marcação de pontos e mapas ficam para uma proposta posterior de site survey.

Fluxo previsto:

1. Atualizar a lista e selecionar uma interface Wi-Fi conectada.
2. Informar um IPv4 de destino autorizado e escolher a pasta local de resultados.
3. Iniciar a sessão de 60 segundos ou solicitar cancelamento com salvamento parcial.
4. Após a conclusão/cancelamento, mostrar contadores, resultados ICMP, RTT apenas em sucesso, RSSI disponível, fontes, frequência marcada como cache e ocorrências.
5. Abrir um JSON salvo e exibir sua análise descritiva local.

A janela indicará os estados de operação e de salvamento. O primeiro incremento exibiu as amostras e o resumo depois do retorno de `ProbeRunner`. Em 2026-10-20, o responsável solicitou o valor de sinal em dBm e seu histórico ao vivo; essa extensão publica as amostras concluídas por canal não bloqueante, mantendo a agenda e o formato de dados. Indicadores de atividade não são apresentados como medições recebidas. Contextos variáveis e métricas ausentes ficam explícitos, sem interpretar médias como diagnóstico de cobertura. Consulte os [critérios do sinal ao vivo](sinal-ao-vivo.md).

## Plano de arquivos

| Arquivo ou projeto | Responsabilidade prevista |
|---|---|
| `src/SinalMap.Presentation/SinalMap.Presentation.csproj` | Apresentação em `net10.0`, sem dependência dos controles WPF, para testes com adaptadores simulados |
| `Presentation/ProbeViewModel.cs`, `SessionResultViewModel.cs` | Estado da operação, validação de entrada, cancelamento, persistência e projeção dos resultados |
| `Presentation/AsyncCommand.cs`, `IProbeDialogs.cs` | Comandos assíncronos sem execução duplicada e fronteira testável para escolha de pasta/arquivo e confirmação de descarte |
| `src/SinalMap.Desktop.Wpf/SinalMap.Desktop.Wpf.csproj` | Executável `net10.0-windows`, `UseWPF=true`, referências à apresentação e infraestrutura |
| `Desktop.Wpf/App.xaml`, `App.xaml.cs` | Inicialização e composição com os serviços existentes |
| `Desktop.Wpf/MainWindow.xaml`, `MainWindow.xaml.cs` | Layout, bindings e integração do fechamento da janela com cancelamento/salvamento |
| `Desktop.Wpf/Services/ProbeDialogs.cs` | Adaptador de diálogos nativos para pasta/arquivo locais e confirmação de descarte |
| `tests/SinalMap.Tests/ProbeViewModelTests.cs`, `AsyncCommandTests.cs` | Fluxos de coleta, falhas, cancelamento, salvamento, valores ausentes e prevenção de duplicidade |
| `SinalMap.sln`, `.github/workflows/ci.yml` | Inclusão dos projetos; compilação WPF em Windows e testes dos componentes compartilhados em Windows/Linux |
| `docs/validacao-wpf.md`, `README.md` | Execução e roteiro manual da janela, com estado real de validação |

Os caminhos abreviados `Presentation/` e `Desktop.Wpf/` representam as pastas dos respectivos projetos dentro de `src/`.

## APIs e dependências

Reutilizar `IWifiMonitor`, `IIcmpProbe`, `IProbeClock`, `ISessionStore`, `ProbeRunner`, `SessionAnalyzer`, `JsonSessionReader` e as implementações Windows existentes. O formato `schemaVersion=1`, o agendamento, os limites e a checagem de rota continuam sendo os do coletor atual. Nenhuma nova biblioteca externa de MVVM é necessária para este incremento.

WPF usará XAML, bindings, `INotifyPropertyChanged` e comandos `ICommand`. Diálogos e operações de controles pertencem à camada WPF; a apresentação terá fronteiras substituíveis em teste. Operações de coleta e disco serão aguardadas assincronamente. A afinidade da thread gráfica precisa ser preservada ao atualizar a apresentação.

WPF executa somente em Windows. O CI Windows compilará a solução com o executável gráfico; o Linux continuará compilando e testando os componentes compartilhados e o console, sem executar a janela. A configuração do CI deve distinguir esses alvos de forma explícita.

## Riscos e decisões propostas

| Risco | Tratamento previsto |
|---|---|
| Interface travada ou atualização na thread errada | Reutilizar coleta assíncrona e atualizar bindings na thread gráfica; evitar esperas bloqueantes |
| Clique duplo e duas sessões simultâneas | Comando indisponível durante coleta/salvamento e testes de concorrência do estado |
| Fechar a janela e perder a sessão | Solicitar cancelamento, aguardar salvamento parcial e só então concluir o fechamento; falha de escrita deve permitir recuperação dos dados em memória |
| Acesso negado ou mudança de conexão | Mostrar a ocorrência e a ausência; manter os códigos nativos e as restrições do coletor |
| Sobrescrita ou pasta sem permissão | Arquivo de nome único, gravação existente sem sobrescrita e apresentação de erro com possibilidade de nova tentativa |
| Ausência interpretada como zero ou rede saudável | Campos ausentes explícitos, RTT somente em sucesso, contadores separados e frequência rotulada como cache |
| Exposição de dados ao compartilhar telas/arquivos | Gravação local explícita; documentação de revisão de identificadores antes de publicar evidências |

A interface não configura rotas, rádio ou permissões. Qualquer mudança futura nesses comportamentos ou no formato JSON exigirá revisão própria.

## Verificações para entregar a janela

- Compilação Release da solução em Windows; testes da apresentação e regressão da coleta em Windows/Linux.
- Testes de entradas inválidas antes da coleta, início duplicado, cancelamento, erro de armazenamento com nova tentativa e representação de métricas ausentes.
- Testes com adaptadores simulados para permissão negada, rota rejeitada e erro ICMP, sem fabricar valores para preencher a tela.
- Inspeção manual em Windows da abertura, seleção de interface/destino, uma coleta completa, cancelamento e fechamento com salvamento pendente.
- Conferência do JSON salvo pela janela com o analisador do console, usando os registros reais da execução.

O aceite dessa proposta autoriza o incremento descrito, sujeito a esses testes. A interface definitiva e o planejamento de plantas/mapas serão revisados posteriormente.

## Referências oficiais consultadas

- [Visão geral do WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [Propriedades do SDK de desktop](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop)
- [Modelo de threads do WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)
- [Notificação de alteração de propriedades](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-implement-property-change-notification)
