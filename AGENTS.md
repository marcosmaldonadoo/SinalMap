# Orientações para trabalhar no SinalMap

## Idioma

Escreva toda documentação em português brasileiro, incluindo README, guias, instruções, registros de decisões, descrições de mudanças e comentários de documentação no código. A versão em inglês está prevista para uma etapa futura e depende de uma solicitação do responsável pelo projeto. Preserve a grafia dos nomes de APIs, identificadores, caminhos e comandos.

## Escopo atual

Em 2026-10-20, após revisar a proposta e executar nova coleta física, o responsável autorizou o avanço para a janela WPF técnica de `docs/proposta-fase-1-wpf.md`, aceitando os limites do parecer da Fase 0. O escopo atual inclui o console existente e essa janela: seleção de interface/destino, coleta, cancelamento com salvamento parcial e análise local de JSON. Plantas, pontos de survey e mapas de calor continuam fora do escopo. Casos físicos ainda não exercitados permanecem pendentes; o aceite não os transforma em testes aprovados.

## Integridade das observações

A extensão de 2026-10-20 solicitada pelo responsável inclui o valor de RSSI em dBm e seu gráfico ao vivo na janela. A publicação de amostras não deve bloquear a agenda; ausência e mudança de contexto precisam interromper a linha. Continuam vigentes a sessão de 60 segundos e as consultas/fontes já existentes.

A solicitação posterior inclui monitoramento contínuo de RSSI sem iniciar um teste, sem ICMP nem gravação, e sugestão do gateway IPv4 da interface selecionada. O monitor pausa antes de coleta ou abertura de histórico; a sugestão permanece editável e não substitui um destino alterado pelo usuário.

Após diagnóstico físico de RSSI estático em 2026-10-20, o responsável autorizou varredura Wi-Fi a cada 5 s também nos testes de 60 s, com registro de que o RTT pode ser afetado. A janela prioriza o RSSI da consulta em tempo real para um único enlace não MLO, mantendo a consulta tradicional como compatibilidade. Preservar os eventos de varredura e o aviso na sessão; sucesso do pedido não equivale a conclusão confirmada. O console não inicia varreduras na coleta padrão.

Não inventar medições, canais ou frequências. Valores não retornados ou não suportados devem permanecer nulos com o motivo registrado. Não converter qualidade percentual em RSSI medido. Identificar dados de cache e preservar os códigos de erro das APIs nativas. Testes simulados não comprovam recepção Wi-Fi em hardware real.

## Verificação

Compilar a solução em Release e executar os testes apropriados antes de entregar mudanças de código. Registrar o ambiente e distinguir testes simulados de coleta real. Não incluir resultados de clientes ou identificadores reais de rede em commits.

## Integração de alterações

O responsável autoriza o merge dos PRs quando o agente considerar a integração necessária, após revisar as mudanças e concluir as verificações pertinentes. Essa autorização dispensa nova confirmação para cada merge. Continuam valendo o escopo da fase atual e a proteção dos dados sensíveis.

Conduzir implementações, revisões e verificações rotineiras de forma autônoma. Solicitar a revisão do responsável antes de integrar grandes mudanças que possam causar problemas, como alterações incompatíveis no formato de dados, mudanças nas permissões ou no comportamento de rede e avanço de fase. Testes físicos que dependem do adaptador do usuário continuam exigindo sua participação; resultados simulados não os substituem.
