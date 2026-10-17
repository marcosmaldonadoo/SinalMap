# SinalMap

Prova de conceito open source de coleta Wi-Fi para Windows. Esta entrega implementa **somente a Fase 0**: console, Native Wi-Fi API, ICMP, JSON local e testes com adaptadores simulados.

O objetivo é validar os dados fornecidos pelo Windows e pelos drivers antes de desenvolver a interface WPF, a importação de plantas ou mapas de calor. Esses recursos ainda não fazem parte desta versão.

## Pré-requisitos

- SDK **.NET 10** estável. `global.json` aceita versões estáveis da linha 10.0 a partir de 10.0.100.
- Windows com adaptador Wi-Fi conectado, driver instalado e serviço **WLAN AutoConfig / WlanSvc** em execução para a coleta real. O alvo inicial de validação é Windows 11 x64.
- Permissão de localização quando exigida pelo Windows para consultar SSID/BSSID. `ERROR_ACCESS_DENIED` é registrado com orientação; executar como administrador não substitui consentimento.
- Um **IPv4 de destino explícito**, preferencialmente o gateway dessa rede. Não há destino externo padrão nem resolução DNS.

Domain, Application e os testes simulados também compilam em Linux. O console recusa coleta nativa fora do Windows. A infraestrutura usa `net10.0` com verificação de plataforma em execução para permitir testar o código de adaptação sem carregar DLLs do Windows.

## Compilar e testar

```powershell
git clone --branch phase0/probe https://github.com/marcosmaldonadoo/SinalMap.git
cd SinalMap
dotnet restore SinalMap.sln
dotnet build SinalMap.sln -c Release --no-restore
dotnet test SinalMap.sln -c Release --no-build --no-restore
```

Enquanto a entrega estiver em revisão, o código está na branch `phase0/probe`. Depois da integração, também poderá ser obtido pela branch principal.

As dependências externas são usadas somente pelo projeto de testes: xUnit e Microsoft.NET.Test.Sdk. O executável usa apenas a biblioteca padrão do .NET e as APIs do Windows.

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

## O que é registrado

| Campo | Origem / significado |
|---|---|
| Interface | GUID, descrição e estado de `WlanEnumInterfaces` |
| SSID e BSSID | `WlanQueryInterface(current_connection)`; SSID preservado também em hexadecimal, sem assumir que todos os SSIDs são UTF-8 |
| RSSI, em dBm | `WlanQueryInterface(rssi)`; não convertido de qualidade percentual |
| Canal | `WlanQueryInterface(channel_number)`; não inferido da frequência |
| Frequência, em kHz | Entrada **em cache** de `WlanGetNetworkBssList`, com correspondência de SSID em bytes, BSSID e tipo BSS |
| Idade da informação BSS | Timestamp de recepção fornecido pelo driver, em FILETIME bruto; não representa o horário da consulta atual |
| Tempo | UTC observado e ticks monotônicos; a frequência do relógio está no cabeçalho da sessão |
| ICMP | Invocação/conclusão, status, RTT somente em sucesso e erro separado; `0 ms` pode ser um RTT legítimo |
| Rota | Índice IPv4 do adaptador selecionado e índice escolhido por `GetBestInterfaceEx` antes do envio |
| Mudanças de BSSID | Mudanças observadas entre amostras, com indicação de lacunas e de SSID diferente |

Cada campo opcional sem suporte permanece **`null`**, com motivo em `issues`. Não há medições geradas ou modo de demonstração no executável. Dados sintéticos existem exclusivamente nos testes.

Antes de cada ping, a rota deve apontar para o adaptador Wi-Fi selecionado. Caso contrário, o envio é bloqueado e registrado como `RouteRejected`. A verificação não fixa a rota: uma mudança entre a checagem e o envio ainda é possível. `SendAttempted=true` indica chamada à API, não captura de um pacote no rádio; `null` indica que um adaptador lançou uma exceção e não confirmou se tentou enviar.

## Estrutura

- `src/SinalMap.Domain`: modelos de observação, sessão, erros e mudanças de BSSID.
- `src/SinalMap.Application`: interfaces de coleta/ICMP/relógio/persistência e agendamento.
- `src/SinalMap.Infrastructure.Windows`: P/Invoke, recursos nativos seguros, coleta Windows, ICMP e JSON.
- `src/SinalMap.Console.Probe`: composição e argumentos do console.
- `tests/SinalMap.Tests`: simulações, agendamento, serialização, ABI e propriedade dos buffers/handles.

Leia o [plano da Fase 0](docs/phase-0-plan.md), as [limitações e APIs](docs/api-and-limitations.md) e o [roteiro de validação no Windows](docs/windows-validation.md).

## Estado da validação

A validação automatizada usa adaptadores simulados e não comprova coleta de rádio em hardware real. A execução manual no Windows, com o adaptador e o driver utilizados no suporte técnico, continua sendo um critério obrigatório antes de avançar à interface gráfica. O checklist em `docs/windows-validation.md` permanece pendente até existir evidência dessa execução.

## Privacidade e licença

Nenhuma telemetria, serviço remoto ou envio de resultados é implementado. Os únicos pacotes de teste são ICMP para o destino escolhido; a compilação/restauração pode acessar o NuGet. SSID, BSSID, GUID de interface e IP do destino permanecem no JSON local. `results/` está no `.gitignore`; revise arquivos antes de compartilhá-los.

[Licença MIT](LICENSE).
