# Orientações para trabalhar no SinalMap

## Idioma

Escreva toda documentação em português brasileiro, incluindo README, guias, instruções, registros de decisões, descrições de mudanças e comentários de documentação no código. A versão em inglês está prevista para uma etapa futura e depende de uma solicitação do responsável pelo projeto. Preserve a grafia dos nomes de APIs, identificadores, caminhos e comandos.

## Escopo atual

A entrega atual é exclusivamente a Fase 0: prova de coleta da rede Wi-Fi conectada em console. Não implementar WPF, plantas ou mapas de calor antes da validação manual documentada em `docs/windows-validation.md`.

## Integridade das observações

Não inventar medições, canais ou frequências. Valores não retornados ou não suportados devem permanecer nulos com o motivo registrado. Não converter qualidade percentual em RSSI medido. Identificar dados de cache e preservar os códigos de erro das APIs nativas. Testes simulados não comprovam recepção Wi-Fi em hardware real.

## Verificação

Compilar a solução em Release e executar os testes apropriados antes de entregar mudanças de código. Registrar o ambiente e distinguir testes simulados de coleta real. Não incluir resultados de clientes ou identificadores reais de rede em commits.

## Integração de alterações

O responsável autoriza o merge dos PRs quando o agente considerar a integração necessária, após revisar as mudanças e concluir as verificações pertinentes. Essa autorização dispensa nova confirmação para cada merge. Continuam valendo o escopo da fase atual e a proteção dos dados sensíveis.

Conduzir implementações, revisões e verificações rotineiras de forma autônoma. Solicitar a revisão do responsável antes de integrar grandes mudanças que possam causar problemas, como alterações incompatíveis no formato de dados, mudanças nas permissões ou no comportamento de rede e avanço de fase. Testes físicos que dependem do adaptador do usuário continuam exigindo sua participação; resultados simulados não os substituem.
