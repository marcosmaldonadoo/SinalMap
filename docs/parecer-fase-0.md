# Parecer técnico da Fase 0

Data do parecer: 2026-10-18. **Atualização em 2026-10-20: alcance e limites aceitos pelo responsável para o piloto WPF técnico, após revisão da proposta e nova coleta física.** O aceite não aprova cenários não exercitados nem certifica site survey ou precisão de RF.

## Conclusão proposta

A evidência é suficiente para propor uma janela WPF técnica que reutilize a coleta existente, mantendo os limites deste parecer. A conclusão vale para a prova de conceito no ambiente examinado; não representa certificação de RF, suporte universal de drivers ou validação de site survey completo. Nenhum código de interface gráfica foi implementado nesta etapa.

O merge original deste documento registrou o parecer para revisão. Em 2026-10-20, o responsável solicitou o avanço para a [proposta concreta da próxima fase](proposta-fase-1-wpf.md), conforme a regra do [AGENTS.md](../AGENTS.md), após revisar a proposta e executar uma nova coleta física. O registro dessa coleta está no [relatório de validação](validation-report.md).

## Origem da evidência

- [Coleta estável](validacao-coleta-estavel.md): um JSON real recebido e examinado integralmente quanto a estrutura, tempos, valores, fontes, rota e erros.
- [Rodada complementar](validacao-manual-complementar.md): resumos do analisador/comparador, códigos de saída e inspeções locais fornecidos pelo responsável. Esses JSONs brutos permaneceram na máquina do responsável e não foram recebidos nesta análise.
- [Validação automatizada](validation-report.md): compilação e testes com adaptadores/buffers simulados, em Linux e Windows. Os testes não substituem rádio físico.

O ambiente relatado é Windows 11 Pro, MediaTek Wi-Fi 6 MT7921 e driver 3.5.0.1349, com .NET 10. A versão/build do Windows foi identificada privadamente por CIM e permanece omitida no resumo público. A matriz de compatibilidade ainda contém um único perfil físico.

## O que foi demonstrado

| Caso | Evidência principal |
|---|---|
| Coleta e persistência | Sessões reais de 60 posições, ICMP assíncrono, métricas com fontes e arquivos lidos pelo analisador |
| Temporização | Agenda monotônica examinada no primeiro JSON; resumos posteriores sem posições descartadas por atraso |
| Comparação | Duas sessões reais com contextos iguais; diferenças calculadas sem preencher valores ausentes |
| Cancelamento | 11 posições despachadas, 49 canceladas antes do despacho, arquivo parcial legível e saída 130 |
| Desconexão | 13 posições conectadas e 47 desconectadas, confirmadas por inspeção local das sequências |
| Reconexão | Interrupção relatada, 12 posições sem conexão, 48 conectadas e estado final `Connected` |
| Localização negada | Código 5 em 57 posições, 1 timeout de leitura e 2 posições ocupadas; nenhuma métrica Wi-Fi, arquivo salvo e saída 3 |
| Rota por outra interface | Ethernet escolhida, 60 `RouteRejected`/`RouteMismatch`, índices distintos e `sendAttempted=false`; 60 leituras Wi-Fi preservadas |
| Dados não fabricados | Campos ausentes por falha/cancelamento não foram preenchidos; testes verificam ausência de estimativas e RTT nulo em falhas |
| Recursos nativos | Testes simulados de ABI, buffers inválidos, erros, liberação e propriedade de handles durante chamadas pendentes |

Os 136 testes existentes também cobrem campos não suportados, bloqueio do transporte por rota incompatível, não sobrescrita de arquivos e privacidade dos relatórios. O estado do CI do commit integrado deve ser consultado no PR correspondente.

## Limites que acompanham a conclusão

1. RSSI constante nas duas sessões comparadas e variável em outras execuções. A precisão, a calibração, a taxa de atualização do driver e a resposta à distância não foram comprovadas.
2. Frequência identificada como cache BSS. Uma consulta por segundo não garante uma nova recepção por segundo. Não há conversão de qualidade percentual em RSSI ou derivação de canal/frequência como dados medidos.
3. Os campos principais estavam disponíveis no adaptador. Não houve caso físico específico de campo sem suporte. Na evidência original deste parecer, a alternativa RSSI e MLO tinham somente simulação. Posteriormente, a [coleta do sinal ao vivo de 2026-10-20](sinal-ao-vivo.md) registrou seis valores da alternativa no mesmo perfil, preservando o erro 27 da consulta principal. MLO continua sem comprovação física.
4. Nenhum roaming ou troca de SSID foi exercitado em hardware. Zero eventos de BSSID não comprova ausência de transições entre as leituras.
5. O caso de rota usou Ethernet; VPNs específicas continuam sem ensaio físico. A consulta prévia de rota não fixa o endereço de origem nem elimina a corrida antes do envio. Não houve captura de pacotes.
6. Ocorreram timeouts reais, mas não foi feito um ensaio separado com um host conhecido que bloqueie ICMP. Os registros não identificam a causa das perdas como interferência ou sinal fraco.
7. A retomada após reconexão foi observada pelo procedimento relatado e pelo estado final; não foi calculado tempo de reassociação. Mudanças de consentimento no mesmo processo, remoção de adaptador e encerramento forçado continuam sem ensaio físico específico.
8. O tratamento de arquivo existente foi testado automaticamente, inclusive no Windows; não foi repetido manualmente nesta rodada. Queda de energia e recuperação após encerramento forçado estão fora da prova entregue.
9. O analisador produz estatísticas globais da sessão, sem separar SSIDs/BSSIDs. Em sessões com contexto variável, essas médias não representam uma única conexão estável. A janela proposta precisa tornar essa condição visível ao apresentar o resumo.

Esses casos continuam identificados como não exercitados ou não comprovados. Aceitar o parecer significa aceitar esses limites para uma interface técnica limitada, não marcar os testes ausentes como aprovados.

## Decisão registrada em 2026-10-20

- [x] Evidências recebidas e limites consolidados, sem identificadores reais de rede no repositório.
- [x] Responsável aceita o alcance da Fase 0 e os casos físicos não exercitados para o piloto técnico.
- [x] Responsável aprova o escopo e os riscos da janela WPF proposta.
- [x] Aceite registrado e escopo vigente atualizado em `AGENTS.md` para implementar a próxima fase.

O escopo vigente inclui o console e a janela WPF técnica, conforme `AGENTS.md`. Plantas, pontos e mapas continuam fora do incremento aprovado.
