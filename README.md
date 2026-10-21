# SinalMap

<p align="center">
  <img src="docs/assets/sinalmap-logo.png" alt="Logo do SinalMap: marcador com sinal Wi-Fi sobre uma planta baixa" width="600">
</p>

A logo apresenta a identidade visual usada na janela técnica.

**SinalMap 1.1.0** — aplicativo open source de monitoramento e coleta Wi-Fi para Windows. Inclui a janela WPF e o console, com Native Wi-Fi API, ICMP, JSON local e análise descritiva.

A documentação do projeto é mantida em **português brasileiro**. A versão em inglês será preparada em uma etapa futura.

O objetivo é inspecionar os dados fornecidos pelo Windows e pelos drivers sem inventar medições. A janela técnica permite operar a coleta existente. Importação de plantas, marcação de pontos e mapas de calor ficam para uma proposta posterior.

## Baixar e abrir no Windows

Baixe **SinalMap-v1.1.0-win-x64.zip** na [release v1.1.0](https://github.com/marcosmaldonadoo/SinalMap/releases/tag/v1.1.0), extraia toda a pasta e abra **SinalMap.Desktop.Wpf.exe**. O pacote inclui o .NET necessário; não exige SDK ou instalação separada do runtime. Preserve os arquivos ao lado do executável.

O pacote **SinalMap-Console-v1.1.0-win-x64.zip** contém a ferramenta de linha de comando. Os pacotes são portáteis para Windows x64, sem instalador ou assinatura digital. Os [passos de uso](docs/como-executar.md), as [notas da versão](docs/releases/v1.1.0.md) e os [limites da validação](docs/validacao-wpf.md) detalham o escopo.

## Pré-requisitos

- SDK **.NET 10** estável. `global.json` aceita versões estáveis da linha 10.0 a partir de 10.0.100.
- Windows com adaptador Wi-Fi conectado, driver instalado e serviço **WLAN AutoConfig / WlanSvc** em execução para a coleta real. O alvo inicial de validação é Windows 11 x64.
- Permissão de localização quando exigida pelo Windows para consultar SSID/BSSID. `ERROR_ACCESS_DENIED` é registrado com orientação; executar como administrador não substitui consentimento.
- Um **IPv4 de destino autorizado** para a coleta com ICMP, preferencialmente o gateway dessa rede. A janela sugere o gateway IPv4 da interface selecionada; o console exige `--target`. Não há destino externo padrão nem resolução DNS. O monitoramento de RSSI dispensa destino.

Domain, Application, Presentation e os testes simulados também compilam em Linux. O console recusa coleta nativa fora do Windows; WPF executa somente em Windows. A infraestrutura usa `net10.0` com verificação de plataforma em execução para permitir testar o código de adaptação sem carregar DLLs do Windows.

## Compilar e testar

```powershell
git clone https://github.com/marcosmaldonadoo/SinalMap.git
cd SinalMap
dotnet restore SinalMap.sln
dotnet build SinalMap.sln -c Release --no-restore
dotnet test SinalMap.sln -c Release --no-build --no-restore
```

No Linux, restaure, compile e teste `tests/SinalMap.Tests/SinalMap.Tests.csproj` em vez da solução completa. Esse projeto inclui as dependências compartilhadas e o console, sem WPF. O CI distingue esses alvos.

As dependências externas são usadas somente pelo projeto de testes: xUnit e Microsoft.NET.Test.Sdk. Os executáveis usam .NET, WPF e as APIs do Windows, sem biblioteca externa de MVVM.

Para preparar os dois pacotes portáteis e os hashes SHA-256 no Windows:

```powershell
./scripts/Publish-Release.ps1
```

Os ZIPs ficam em `artifacts/releases/1.1.0/`, fora do controle de versão. O script recusa sobrescrever pacotes existentes; use `-ArtifactRoot` para gerar em outra pasta.

## Abrir a janela técnica no Windows

Esta seção descreve a interface distribuída na versão 1.1.0.

```powershell
dotnet run --project src/SinalMap.Desktop.Wpf -c Release
```

Ao abrir a janela, a aba **Visão geral** acompanha o RSSI da interface conectada selecionada, aproximadamente uma vez por segundo, sem iniciar teste, enviar ICMP ou gravar arquivo. Havendo mais de uma interface conectada, selecione uma. O gráfico mantém até 60 leituras da janela recente de 60 s; o horário indica a resposta da consulta do Windows, não uma garantia de nova recepção pelo rádio.

O campo **Rede conectada · SSID**, abaixo do seletor de interface, mostra o nome retornado pelo Windows nas mesmas consultas do monitor e da coleta. Trocas de rede e desconexões atualizam esse campo; quando o Windows não fornece um nome textual, ele fica indisponível. Ao analisar um JSON, o campo indica **Monitor pausado**, sem apresentar a rede do arquivo como conexão atual.

A janela solicita uma **varredura Wi-Fi a cada 5 segundos**, também durante o teste de 60 s, e prioriza o RSSI da consulta em tempo real para conexões de enlace único não MLO. Isso corrige o caso observado neste adaptador, em que somente consultar repetidamente mantinha o RSSI estático. Varreduras podem aumentar a latência; a tela, o resumo e o JSON registram essa condição. A confirmação de conclusão é acompanhada separadamente do pedido. Consulte o [diagnóstico e os limites da correção](docs/correcao-rssi-estatico.md).

Para salvar uma medição, confira o destino autorizado e a pasta e clique em **Coletar por 60 segundos**. O gateway IPv4 é preenchido apenas quando há um único candidato válido da interface selecionada. Sem gateway ou com vários candidatos, o campo continua vazio; um destino editado pelo usuário é preservado ao atualizar ou trocar a interface. Erros de IPv4 aparecem junto ao campo e impedem o início pelo botão. Expanda **Pasta de resultados** para alterar o caminho, cujo padrão é `Documentos/SinalMap/results`, independente do diretório de onde o aplicativo foi aberto. A coleta dura 60 segundos; o progresso conta posições processadas, e as abas de resumo, tabela, contexto e ocorrências aparecem quando há um resultado.

O valor de sinal vem do adaptador, por exemplo `−60 dBm`; ele não é fixado nem convertido de porcentagem. Leituras ausentes ficam indisponíveis e interrompem a linha. Ao abrir um JSON, o mesmo gráfico mostra o histórico salvo. Consulte os [critérios do sinal ao vivo](docs/sinal-ao-vivo.md).

O monitor pausa antes de uma coleta ou da abertura de arquivo. O resultado fica em **HISTÓRICO** ao terminar; use **Ao vivo** (`Ctrl+L`) para voltar ao monitoramento sem teste. Durante a coleta, o RSSI é exibido assim que a consulta Wi-Fi termina, sem aguardar a resposta ICMP; somente a amostra completa é persistida.

A interface destaca a última leitura, os extremos registrados e a quantidade de posições com RSSI. A régua de dBm acompanha o mesmo valor real do visor; quando falta RSSI, o visor mostra um traço e a régua fica sem marcador. Ela não converte qualidade percentual nem atribui uma classificação de cobertura. Passe o mouse sobre o gráfico para consultar o tempo e o valor de cada posição. Com o gráfico em foco, use `←`/`→`, `Home` e `End`; `Esc` encerra a seleção por teclado. O indicador distingue coleta ao vivo de histórico; **Detalhes e diagnóstico** reúne fontes, varreduras e ocorrências. Uma nova coleta ou um novo histórico volta à leitura principal; as atualizações por amostra preservam a posição de inspeção. Os comandos de iniciar e cancelar permanecem acessíveis enquanto a configuração e o conteúdo da aba rolam em janelas menores. A tabela mantém posição, RSSI e RTT fixos durante a rolagem horizontal e permite copiar a seleção com `Ctrl+C`.

Cancelar ou fechar durante a coleta solicita cancelamento e aguarda o salvamento parcial. Se a gravação falhar, a janela mantém a sessão em memória e bloqueia sua substituição. O aviso oferece **Escolher outra pasta…** e **Tentar salvar novamente** diretamente. Descartar uma sessão não salva exige confirmação explícita. Encerramento forçado ainda pode perder esses dados.

Use **Abrir coleta** (`Ctrl+O`) para analisar um arquivo existente sem iniciar ICMP; `F5` atualiza as interfaces. O [roteiro WPF](docs/validacao-wpf.md) informa o estado das verificações e as limitações do piloto.

Se a janela ficar branca por uma falha de aceleração gráfica, a revisão atual oferece uma alternativa somente para esse processo:

```powershell
dotnet run --project src/SinalMap.Desktop.Wpf -c Release -- --software-rendering
```

Essa opção usa renderização por software e não modifica configurações do Windows.

## Executar no Windows

```powershell
# 1. Consultar GUIDs e estados das interfaces Wi-Fi.
dotnet run --project src/SinalMap.Console.Probe -c Release -- --list

# 2. Usar o IPv4 real do gateway ou de outro destino autorizado nessa rede.
# 192.168.10.1 é apenas um exemplo, não é descoberto nem escolhido pelo programa.
dotnet run --project src/SinalMap.Console.Probe -c Release -- --target 192.168.10.1

# 3. Com mais de uma interface Wi-Fi conectada, informar o GUID exibido por --list.
dotnet run --project src/SinalMap.Console.Probe -c Release -- --target 192.168.10.1 --interface "GUID-EXIBIDO-NA-LISTAGEM" --output results/ponto-01.json
```

A sessão tem **60 posições planejadas**, em `t=0, 1, …, 59 s`, dentro de uma janela de 60 s. O timeout ICMP é de 800 ms. Atraso de início superior a 250 ms descarta aquela posição; não são disparados pacotes em rajada para compensá-la. O sistema operacional não oferece garantia de tempo real.

O JSON é salvo em `results/` por padrão. `Ctrl+C` cancela a aquisição, aguarda as operações gerenciadas e salva a sessão parcial. Posições não iniciadas ficam identificadas. Um arquivo existente nunca é sobrescrito. O encerramento forçado do processo ou uma falha de disco pode impedir a gravação; não há recuperação de sessão após queda nesta fase.

Códigos de saída: `0` sessão concluída com alguma leitura Wi-Fi válida; `1` falha de inicialização ou gravação; `2` argumentos/plataforma inválidos; `3` JSON salvo, mas nenhuma leitura Wi-Fi conectada válida; `130` cancelamento. Timeout ICMP é uma observação, não um erro fatal do programa. O código `0` não significa que a rede está saudável.

## Analisar uma coleta salva

```powershell
dotnet run --project src/SinalMap.Console.Probe -c Release -- --analyze results/ponto-01.json
```

O resumo local informa posições, resultados ICMP, estatísticas de RTT/RSSI, disponibilidade das métricas, fontes e ocorrências, sem exibir os identificadores da rede. Também indica variações e informações indeterminadas do contexto registrado, para ajudar a interpretar médias globais que podem reunir redes, APs ou condições de medição diferentes. Funciona também em Linux e preserva o JSON original. Consulte os [critérios, limites e códigos de saída da análise](docs/analise-local.md). O resumo auxilia a inspeção e não substitui a validação em hardware.

Para comparar duas coletas salvas:

```powershell
dotnet run --project src/SinalMap.Console.Probe -c Release -- --compare results/ponto-a.json results/ponto-b.json
```

O relatório mostra os dois resumos e a compatibilidade dos contextos registrados. Diferenças **B − A** de média de RTT/RSSI são calculadas somente quando seus requisitos são atendidos, sem presumir identidades ou valores ausentes. Consulte o [guia de comparação](docs/comparacao-coletas.md), incluindo os limites para interpretar mudanças de local.

## O que é registrado

| Campo | Origem / significado |
|---|---|
| Interface | GUID, descrição e estado de `WlanEnumInterfaces` |
| SSID e BSSID | `WlanQueryInterface(current_connection)`; SSID preservado também em hexadecimal, sem assumir que todos os SSIDs são UTF-8 |
| RSSI, em dBm | `WlanQueryInterface(rssi)`; em falha compatível, tentar `realtime_connection_quality` somente para conexão não MLO de enlace único. A fonte é registrada; não converter qualidade percentual |
| Canal | `WlanQueryInterface(channel_number)`; não inferido da frequência |
| Frequência, em kHz | Entrada **em cache** de `WlanGetNetworkBssList`, com correspondência de SSID em bytes, BSSID e tipo BSS |
| Idade da informação BSS | Timestamp de recepção fornecido pelo driver, em FILETIME bruto; não representa o horário da consulta atual |
| Tempo | UTC observado e ticks monotônicos; a frequência do relógio está no cabeçalho da sessão |
| ICMP | Invocação/conclusão, status, RTT somente em sucesso e erro separado; `0 ms` pode ser um RTT legítimo |
| Rota | Índice IPv4 do adaptador selecionado e índice escolhido por `GetBestInterfaceEx` antes do envio |
| Mudanças de BSSID | Mudanças observadas entre amostras, com indicação de lacunas e de SSID diferente |

Cada campo opcional sem suporte permanece **`null`**, com motivo em `issues`. Não há medições geradas ou modo de demonstração no executável. Dados sintéticos existem exclusivamente nos testes.

O console informa separadamente quantas posições têm RSSI, canal e frequência disponíveis. **Uma leitura da conexão Wi-Fi não garante que todos esses campos foram obtidos.** Se a primeira consulta de RSSI falhar e a alternativa funcionar, o erro original permanece em `issues` e `rssiSource` identifica a consulta que forneceu o valor. Se ambas falharem, RSSI e fonte ficam nulos.

Antes de cada ping, a rota deve apontar para o adaptador Wi-Fi selecionado. Caso contrário, o envio é bloqueado e registrado como `RouteRejected`. A verificação não fixa a rota: uma mudança entre a checagem e o envio ainda é possível. `SendAttempted=true` indica chamada à API, não captura de um pacote no rádio; `null` indica que um adaptador lançou uma exceção e não confirmou se tentou enviar.

## Estrutura

- `src/SinalMap.Domain`: modelos de observação, sessão, erros e mudanças de BSSID.
- `src/SinalMap.Application`: interfaces de coleta/ICMP/relógio/persistência e agendamento.
- `src/SinalMap.Infrastructure.Windows`: P/Invoke, recursos nativos seguros, coleta Windows, ICMP e JSON.
- `src/SinalMap.Console.Probe`: composição e argumentos do console.
- `src/SinalMap.Presentation`: estado da operação, comandos e projeção dos resultados, sem controles WPF.
- `src/SinalMap.Desktop.Wpf`: janela Windows, bindings, diálogos locais e composição.
- `tests/SinalMap.Tests`: simulações, agendamento, serialização, ABI e propriedade dos buffers/handles.

Leia o [plano da Fase 0](docs/phase-0-plan.md), as [limitações e APIs](docs/api-and-limitations.md) e o [roteiro de validação no Windows](docs/windows-validation.md).

## Estado da validação

A validação automatizada usa adaptadores simulados. Uma [coleta básica real de 60 s foi examinada e aprovada no MediaTek Wi-Fi 6 MT7921](docs/validacao-coleta-estavel.md), com RSSI, canal, frequência em cache e ICMP registrados sem erros. Nessa execução, o RSSI veio da consulta principal; a alternativa não foi utilizada.

Uma [rodada manual complementar](docs/validacao-manual-complementar.md) forneceu resumos de comparação entre locais, cancelamento com salvamento parcial, desconexão e reconexão. O cancelamento terminou com código 130, e a sessão de reconexão terminou conectada após a interrupção relatada. Os JSONs dessa rodada permaneceram locais e não foram examinados integralmente nesta análise. O RSSI foi constante nas duas sessões comparadas e variou em outras execuções; sua resposta à distância e precisão continuam inconclusivas.

O teste de localização negada também foi relatado: 57 posições com código 5, uma leitura com timeout e duas com monitor ocupado, sem métricas Wi-Fi registradas. A sessão foi salva e analisada, com código de saída 3. Essa indisponibilidade por permissão não determina se um campo é suportado pelo driver.

A rota pela Ethernet foi rejeitada nas 60 posições, com índices diferentes e `sendAttempted=false`, mantendo 60 leituras Wi-Fi. O ambiente foi identificado como Windows 11 Pro e MediaTek MT7921 com driver 3.5.0.1349. Esses resultados descrevem o perfil testado, sem certificar precisão de RSSI ou compatibilidade universal de drivers.

O [parecer da Fase 0](docs/parecer-fase-0.md) consolida as evidências e os limites aceitos pelo responsável em 2026-10-20 para a continuação técnica. A [inspeção do sinal ao vivo](docs/sinal-ao-vivo.md) também registrou seis valores da alternativa RSSI no perfil MediaTek, preservando os erros da consulta principal. Campos sem suporte, MLO, roaming e túneis específicos permanecem sem validação física. A [proposta de janela WPF](docs/proposta-fase-1-wpf.md) teve seu escopo aprovado; o [roteiro WPF](docs/validacao-wpf.md) registra as verificações do piloto. O [roteiro no Windows](docs/windows-validation.md) preserva as pendências da coleta.

## Privacidade e licença

Nenhuma telemetria, serviço remoto ou envio de resultados é implementado. Os únicos pacotes de teste são ICMP para o destino escolhido; a compilação/restauração pode acessar o NuGet. SSID, BSSID, GUID de interface e IP do destino permanecem no JSON local. `results/` está no `.gitignore`; revise arquivos antes de compartilhá-los.

[Licença MIT em português](docs/licenca-mit-pt-br.md), com o [texto padronizado da licença](LICENSE) preservado no repositório.
