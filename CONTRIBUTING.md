# Contribuindo para o SinalMap

## Idioma da documentação

Toda documentação escrita para o projeto deve estar em **português brasileiro**: README, guias, instruções de execução, registros de decisões, descrições de mudanças e comentários de documentação no código. A versão em inglês será produzida em uma etapa futura, quando solicitada. Não manter versões bilíngues dos guias nesta fase.

Nomes de APIs, identificadores do código, caminhos e comandos devem preservar sua grafia técnica. A tradução explicativa da licença está em `docs/licenca-mit-pt-br.md`; o arquivo `LICENSE` contém o texto padronizado da licença.

## Desenvolvimento e validação

Use .NET 10. Domain é compartilhado por Application; Infrastructure.Windows e Presentation dependem de Application. Console.Probe usa a infraestrutura; Desktop.Wpf compõe infraestrutura e apresentação. Presentation não deve depender dos controles WPF. Execute `dotnet build SinalMap.sln -c Release` e `dotnet test SinalMap.sln -c Release` no Windows antes de propor mudanças. No Linux, use `tests/SinalMap.Tests/SinalMap.Tests.csproj` como alvo de restauração, compilação e testes.

Um novo campo precisa de origem, unidade e representação da indisponibilidade. Se o valor for calculado, identifique-o como cálculo, com fórmula e pressupostos; não substitua campos medidos por estimativas. A Fase 0 não inclui esses cálculos.

Erros de APIs nativas precisam preservar operação/código e liberar recursos em todos os caminhos. Use adaptadores simulados para testes determinísticos. Não adicione dados reais de clientes, senhas, SSIDs ou BSSIDs a testes, commits ou logs públicos.

Validação de hardware deve informar Windows, adaptador e driver, métricas suportadas e resultados do roteiro em `docs/windows-validation.md`. Testes simulados aprovados não autorizam afirmar que a coleta foi validada em rádio real. O piloto WPF técnico foi autorizado em 2026-10-20; suas verificações ficam em `docs/validacao-wpf.md`. Plantas e mapas permanecem fora do escopo.
