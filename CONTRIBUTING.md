# Contribuindo para a Fase 0

## Idioma da documentação

Toda documentação escrita para o projeto deve estar em **português brasileiro**: README, guias, instruções de execução, registros de decisões, descrições de mudanças e comentários de documentação no código. A versão em inglês será produzida em uma etapa futura, quando solicitada. Não manter versões bilíngues dos guias nesta fase.

Nomes de APIs, identificadores do código, caminhos e comandos devem preservar sua grafia técnica. A tradução explicativa da licença está em `docs/licenca-mit-pt-br.md`; o arquivo `LICENSE` contém o texto padronizado da licença.

## Desenvolvimento e validação

Use .NET 10 e mantenha as dependências Domain ← Application ← Infrastructure.Windows ← Console.Probe. Execute `dotnet build SinalMap.sln -c Release` e `dotnet test SinalMap.sln -c Release` antes de propor mudanças.

Um novo campo precisa de origem, unidade e representação da indisponibilidade. Se o valor for calculado, identifique-o como cálculo, com fórmula e pressupostos; não substitua campos medidos por estimativas. A Fase 0 não inclui esses cálculos.

Erros de APIs nativas precisam preservar operação/código e liberar recursos em todos os caminhos. Use adaptadores simulados para testes determinísticos. Não adicione dados reais de clientes, senhas, SSIDs ou BSSIDs a testes, commits ou logs públicos.

Validação de hardware deve informar Windows, adaptador e driver, métricas suportadas e resultados do roteiro em `docs/windows-validation.md`. Testes simulados aprovados não autorizam afirmar que a coleta foi validada em rádio real. A interface WPF depende desse aceite e não faz parte desta entrega.
