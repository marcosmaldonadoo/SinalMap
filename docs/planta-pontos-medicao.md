# Planta da loja e pontos de medição

## Escopo aprovado em 2026-10-22

Em 2026-10-22, o responsável aprovou a integração da planta da loja e do feedback visual das medições reais, com distribuição na versão 1.2.0. O fluxo permite importar a foto da planta, indicar onde fica o roteador, escolher pontos e vincular as novas coletas a esses locais. Essa aprovação mantém os limites físicos de validação descritos neste guia; casos ainda não exercitados não passam a ser considerados aprovados.

Na versão gráfica 1.2.0, abra `SinalMap-v1.2.0-win-x64.exe`, disponível na [release oficial](https://github.com/marcosmaldonadoo/SinalMap/releases/tag/v1.2.0). O executável inclui o .NET necessário e dispensa a extração de um ZIP. Consulte o [guia de execução](como-executar.md) para pré-requisitos e verificação do download.

## Usar a planta

1. Abra a aplicação e confira a interface Wi-Fi, o destino IPv4 autorizado e a pasta dos JSONs na janela principal.
2. Clique em **Planta da loja** e em **Importar foto…**. Escolha uma imagem PNG ou JPEG local, com até 20 MiB. A imagem deve ter até 12000 pixels em cada dimensão e 40 milhões de pixels no total. Corrija a orientação no arquivo antes de importar se a foto estiver girada.
3. Clique na posição do roteador na imagem. O marcador **R** indica essa referência manual. **Marcar roteador na planta** permite ajustá-la depois.
4. Clique em **+ Novo ponto** e depois no local onde será feita a medição. Dê um nome ao ponto, por exemplo “Caixa” ou “Estoque”. Os círculos numerados e a lista permitem alternar os pontos.
5. Vá fisicamente até o ponto selecionado com o notebook e clique em **Medir neste ponto · 60 s**. Na primeira coleta, escolha onde salvar o projeto. Permaneça no local durante a medição. O sinal é obtido do adaptador do notebook, nas mesmas condições da coleta existente.
6. Ao terminar, o JSON original fica na pasta de resultados e seu vínculo é salvo no projeto da planta. A área **Última coleta neste ponto** mostra a média e a quantidade de leituras válidas, ou um traço quando não houve RSSI válido. Uma coleta cancelada e salva fica identificada como parcial.
7. Para outro local, crie um novo ponto. Um ponto que já tem coletas não pode ser reposicionado; cada vínculo preserva o nome e as coordenadas do ponto e do roteador usados naquele momento.

O teclado também permite marcar a planta: coloque o foco na imagem, use as setas e confirme com `Enter`. `Shift` permite ajustes menores; `Home` centraliza o cursor e `Esc` cancela a marcação. No modo de seleção, setas, `Home` e `End` alternam os pontos. `Ctrl+S` salva o projeto.

## Feedback visual das medições reais

Após avaliar a referência [Simulador de Cobertura Wi-Fi](https://simulador.talentosdefibra.com.br/), o responsável escolheu **medições reais com cores e valores na planta**. Cada círculo recebe a cor correspondente ao RSSI médio da última coleta salva naquele ponto, com o valor em dBm junto ao marcador. A legenda usa estas faixas visuais:

| Cor | RSSI médio registrado |
| --- | --- |
| Verde | Maior ou igual a −50 dBm |
| Verde claro | De −60 até menos de −50 dBm |
| Amarelo | De −70 até menos de −60 dBm |
| Laranja | De −80 até menos de −70 dBm |
| Vermelho | Menor que −80 dBm |

As faixas usam o valor completo antes do arredondamento visual. São referências de intensidade; não garantem estabilidade, velocidade ou qualidade de aplicações. A planta não recebe estimativas entre os pontos, nem halos que representem alcance do roteador.

Um ponto sem coleta fica neutro e mostra **Não medido**. Uma coleta sem RSSI válido mostra **Sem RSSI**, sem reaproveitar um valor anterior ou interpretar ausência de dados como ausência de sinal. Se a marcação do roteador ou do ponto mudou depois da coleta, o resultado anterior fica neutro e identificado como **Anterior**. Coletas parciais são indicadas nos detalhes e etiquetas, quando há espaço.

Durante a aquisição, o ponto ativo mostra **Ao vivo** com a leitura atual do Windows, separada da média registrada. Esse valor é temporário, não cria vínculo e não altera o resultado salvo até a gravação do JSON. Depois de concluir ou cancelar e salvar, o marcador passa a usar o resultado da coleta. Os outros pontos mantêm suas médias anteriores.

**Mostrar valores** permite ocultar as etiquetas preservando as cores dos marcadores. Passe o mouse para ver nome, valor e quantidade de leituras. Etiquetas também selecionam o ponto; quando pontos próximos impedem exibir todas sem sobreposição, o valor continua no hover e na lista lateral. O ponto selecionado e o ponto em coleta têm prioridade visual. As cores são acompanhadas de números e texto para permitir a interpretação sem depender apenas da cor.

## Arquivos e recuperação

O arquivo `*.sinalmap-planta.json` tem formato próprio, com `schemaVersion=1`, separado de `ProbeSession`. Ele inclui a imagem em base64, as marcações normalizadas entre zero e um, os vínculos por ID de sessão e os caminhos absolutos dos JSONs originais. A foto original não é modificada. A planta pode ser reaberta mesmo se o arquivo original da foto for movido; os JSONs de coleta continuam separados e devem ser preservados na pasta indicada. O resumo registrado no projeto não substitui a análise do JSON completo.

**Salvar projeto** e **Salvar como…** gravam primeiro em um arquivo temporário no mesmo diretório e só então substituem o destino escolhido. O formato de projeto exige a terminação `.sinalmap-planta.json` para evitar substituir por engano um JSON comum de coleta. O projeto admite até 500 pontos, 5000 vínculos e um arquivo de até 40 MiB. Esses limites são verificados ao abrir e salvar.

Abra e edite cada projeto em uma única instância por vez. Não há detecção de conflito entre duas cópias abertas: uma gravação posterior pode substituir marcações e vínculos salvos pela outra instância. Os JSONs originais de coleta permanecem preservados.

Antes de medir, o projeto e as posições são salvos. Durante a aquisição ou enquanto houver um JSON pendente, edição, troca da imagem e fechamento da planta ficam bloqueados. **Parar e salvar parcial** usa o cancelamento já existente. Se a gravação do JSON falhar, a janela permite escolher outra pasta, tentar salvar a coleta novamente ou descartá-la com confirmação. O vínculo só é criado após a gravação do JSON.

O fechamento da janela principal também respeita uma operação em andamento na planta, mesmo quando o projeto não apresenta alterações por salvar. Conclua a operação ou resolva a pendência indicada antes de sair.

Se o JSON for salvo e depois a gravação do projeto falhar, o arquivo de coleta continua salvo e o vínculo fica em memória. Use **Salvar projeto** ou **Salvar como…** para recuperar a anotação. Essa tentativa não dispara nova aquisição nem regrava o JSON. Fechar um projeto alterado exige escolher entre salvar, descartar as alterações da planta ou continuar editando. Os JSONs já gravados não são apagados pelo descarte de alterações da planta.

Plantas de lojas podem conter informações privadas. O projeto inclui a imagem e referências locais; revise esses arquivos antes de compartilhá-los. Arquivos de teste, plantas, coletas e ferramentas locais de desenvolvimento permanecem fora dos commits.

## Significado das marcações

A posição é informada manualmente: o programa não localiza o notebook, não verifica se o operador está no ponto nem identifica automaticamente o roteador desenhado pelo BSSID. A imagem não tem escala calibrada; não há cálculo de distância em metros, propagação, cobertura ou mapa de calor. Uma média pode reunir mudanças de conexão; consulte o JSON para examinar contexto, lacunas e ocorrências.

O monitor ao vivo e a abertura de um JSON anterior não criam vínculos com a planta. Para associar uma nova coleta, use **Medir neste ponto · 60 s** na janela da planta. As consultas de rádio, permissões, varreduras, destino ICMP e formato original de coleta não foram alterados. O aviso de possível impacto das varreduras no RTT continua vigente.

## Verificação inicial da planta

Na verificação inicial, o ambiente foi Windows x64, SDK .NET 10.0.402 e runtime .NET 10.0.13. A solução compilou em Release sem avisos ou erros e passou em **305 testes automatizados simulados**, sem falhas ou ignorados. Os binários dessa revisão ficaram em `artifacts/floor-plan-final`; o verificador e as imagens, em `artifacts/floor-plan-validation` e `artifacts/floor-plan-preview`, fora do controle de versão.

Foram exercitados geometria normalizada, margens da imagem, redimensionamento, validação de imagens/projetos, persistência atômica, cancelamento, recuperação de falhas, proteção dos JSONs, fontes RSSI desconhecidas e limites. O ciclo de importação, marcação, aquisição simulada, vínculo, salvamento e reabertura foi executado em um verificador WPF local com portas de rede simuladas. A inspeção visual em 1280 × 800 e 1000 × 640 não registrou erros de binding. Uma falha de seleção ao renomear pontos foi encontrada na renderização com controles reais e corrigida.

Na janela nativa, foram conferidos abertura pelo botão da aplicação, diálogo de importação, carregamento de PNG fictício, clique no roteador, criação/seleção de ponto e gravação pelo diálogo de salvamento. O monitor da janela principal continuou ativo; não foi iniciado teste ICMP nesse percurso. A automação exigiu coordenadas para os diálogos nativos porque seus índices de acessibilidade não eram endereçáveis; a importação e a gravação foram concluídas.

Não foi realizada coleta física de 60 s vinculada à planta real da loja. Permanecem pendentes a conferência das posições pelo responsável, o percurso físico pelos pontos, a aquisição em cada local e a navegação nativa completa por teclado. A validação simulada não comprova a recepção do rádio nem a correspondência da posição física.

## Verificação complementar do feedback visual

No mesmo ambiente Windows x64/.NET 10, a revisão do feedback foi compilada em Release sem avisos/erros e passou em **335 testes simulados**, sem falhas ou ignorados. Os testes novos verificam os limites e frações das faixas, ausência de RSSI, coleta mais recente por horário, desempate consistente, coletas parciais, marcações alteradas, reabertura, seleção por ID, preferências visuais e leitura temporária associada somente ao ponto em coleta. O JSON original e o projeto de planta conservam os formatos existentes.

O verificador WPF em `artifacts/floor-plan-validation` foi executado com planta fictícia, portas de rádio/ICMP simuladas e saídas locais em `artifacts/floor-feedback-preview`. Foram conferidos sete pontos com as cinco faixas, um ponto sem RSSI e outro não medido, além de ocultação de etiquetas, coleta ao vivo temporária e retorno à média salva. A renderização em 1280 × 800 e 1000 × 640 terminou sem erros de binding. Os valores dessas imagens são sintéticos, não resultados físicos. A solução final usa saída isolada em `artifacts/floor-feedback-final`.

Na aplicação nativa recompilada, foram conferidos reabertura do projeto fictício, lista de resultados, seleção por círculo e etiqueta, troca de foco e ocultação/restauração dos valores. O primeiro clique em uma etiqueta após reabrir podia apenas focar a planta; o alvo agora é resolvido antes da mudança de foco e as regiões de clique são preservadas até o próximo desenho. A seleção respondeu no primeiro clique nos percursos repetidos. Um diagnóstico adicional do componente confirmou sete regiões de etiquetas, preservação após foco e reconhecimento do alvo entre a invalidação de uma propriedade visual e o redesenho. O painel prioriza os pontos e mantém os campos de planta/roteador recolhidos. Nenhuma coleta física ou teste ICMP foi iniciado nessa conferência.

## Verificação da v1.2.0 em 2026-10-22

A solução 1.2.0 foi compilada em Release no Windows x64, SDK .NET 10.0.402 e runtime .NET 10.0.13, com **0 avisos, 0 erros e 336 testes simulados aprovados**, sem falhas ou ignorados. A saída local ficou em `artifacts/v1.2-validation`. Esta verificação sucede a revisão de 335 testes acima e inclui a regressão de fechamento: a janela principal respeita uma operação protegida da planta mesmo quando não há alterações por salvar.

A distribuição gráfica usa o perfil `Portable-win-x64`, autocontido e em arquivo único, com compressão, bibliotecas nativas incluídas para extração e sem remoção de código por trimming. Na configuração padrão, o .NET pode extrair essas bibliotecas para uma subpasta de `%TEMP%\.net` na inicialização. Isso não altera o formato nem o local escolhido para projetos e JSONs de coleta.

O executável único foi inspecionado sozinho em uma pasta com espaço e acento no nome, sem DLLs externas ao lado. O monitor abriu, um projeto fictício existente foi carregado e a seleção por etiqueta atualizou o resultado. A biblioteca nativa WPF foi carregada do cache do próprio pacote. O console publicado executou `--help`. O ambiente já tinha .NET instalado; não foi usado um Windows limpo sem runtime. Não houve nova coleta ICMP nesse percurso.

As pendências físicas descritas neste guia permanecem vigentes. A autorização para integrar e distribuir a etapa não substitui uma coleta real vinculada à planta da loja nem comprova a localização física do notebook.
