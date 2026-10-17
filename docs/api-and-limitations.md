# APIs, formato e limitações conhecidas

## ABI e propriedade dos recursos

`WlanOpenHandle` solicita a versão 2. O handle pertence a `SafeWlanHandle` e é fechado com `WlanCloseHandle`. Resultados de enumeração, consulta e lista BSS pertencem a `SafeWlanMemory` e são liberados com `WlanFreeMemory`, inclusive quando há erro ou falha de parsing. Cada worker de leitura é proprietário do seu cliente nativo; um timeout do consumidor não fecha o handle de um RPC em andamento.

Os retornos WLAN são códigos Win32 diretos. Não se usa `Marshal.GetLastWin32Error` para esses retornos. `DWORD`/`ULONG`/enums têm 32 bits, `LONG` é assinado de 32 bits, `BOOL` tem 4 bytes, `BOOLEAN` tem 1 byte, SSID tem até 32 bytes e BSSID tem 6 bytes. Buffers de texto são UTF-16 de tamanho fixo. Os testes verificam tamanhos e offsets das estruturas, rejeição de tamanhos inválidos e liberação dos recursos com uma implementação nativa simulada.

`WlanQueryInterface` informa o tamanho: ele é validado antes de ler a estrutura. A lista BSS informa tamanho total e contagem: ambos são validados antes da iteração. A lista de interfaces não informa o tamanho total da alocação; sua contagem faz parte do contrato do Windows e recebe um limite de sanidade antes da aritmética. Nenhum parser gerenciado consegue provar a validade de um ponteiro arbitrário fornecido por uma API nativa. Os IEs variáveis da lista BSS não são lidos.

## Ausência e proveniência

| Dado / caso | Comportamento da Fase 0 |
|---|---|
| RSSI não suportado | `rssiDbm=null`; não converter a qualidade percentual da conexão |
| Canal indisponível ou zero | `channel=null`; não converter da frequência |
| Frequência sem BSS correspondente | `frequencyKHz=null`; não escolher uma entrada vizinha |
| Frequência de PHY FHSS / retorno zero | Ausente, com motivo; campo não é válido para FHSS |
| BSS em cache | Frequência preservada como valor do cache; `bssHostTimestampFileTime` bruto permite examinar sua antiguidade |
| SSID não UTF-8 | Texto nulo; bytes exatos em `ssidHex`; não inserir caracteres de substituição como identidade |
| Permissão negada / rádio desligado / interface removida | Erro com operação e código quando disponível; nenhum dado anterior reutilizado como leitura atual |
| BSSID muda durante as consultas | Amostra Wi-Fi descartada como inconsistente |
| Mudança entre amostras | Registrar a observação e a lacuna; não afirmar instante exato, tempo de roaming ou ausência de interrupção |
| Ping sem resposta | `TimedOut`, RTT nulo; não equivale por si só a perda causada pelo Wi-Fi |
| ICMP bloqueado / host inalcançável | Preservar status/erro; não gerar RTT nem diagnóstico de RF |
| Posição perdida / cancelada antes de iniciar | Wi-Fi/ICMP nulos e disposição explícita; não contar como pacote perdido |

`bssHostTimestampFileTime` é um inteiro fornecido pelo driver em intervalos de 100 ns desde 1601, distinto dos ticks monotônicos da aplicação. Não se calcula frequência a partir de canal, nem canal a partir de frequência. O monitor não força `WlanScan`, não muda SSID e não configura rádio, canal, potência ou domínio regulatório.

## Temporização e paralelismo

As posições planejadas são 0 a 59, com intervalo de 1 s. A janela termina em 60 s; ela pode ser excedida pelo tempo necessário para completar operações já iniciadas ou para gravar o arquivo. O agendamento usa somente ticks monotônicos. `plannedAtUtc` é uma projeção do UTC inicial; UTC observado pode diferir após sincronização do relógio.

`dispatchedAt` marca o disparo da posição pelo agendador. No ICMP, `startedAt` marca a chamada ao transporte depois da verificação da rota, ou o início da tentativa se ela for rejeitada antes do transporte. Esses horários não são timestamps de transmissão no rádio. A latência é o `RoundtripTime` retornado pelo .NET, em milissegundos inteiros; não é tempo de ida, tempo de enlace nem precisão submilissegundo.

ICMP e consulta WLAN de uma posição começam independentemente. Windows e drivers não garantem que representem o mesmo instante. Consultas de conexão antes/depois reduzem inconsistências de identidade, mas não detectam uma mudança A→B→A inteiramente entre as duas consultas, nem uma alteração de canal sem mudança de identidade.

A espera WLAN é de até 750 ms. Uma chamada nativa que excede isso continua sob propriedade do worker até retornar; os dados tardios são descartados e leituras concorrentes são rejeitadas como `Busy`. Não há garantia de encerramento limpo do RPC se o processo for terminado. Não se acumula uma fila de consultas nativas.

A pré-verificação de rota tem espera máxima de 200 ms. Uma pré-verificação cujo tempo monotônico exceda 250 ms impede o envio. Em situação normal, ICMP tem timeout de 800 ms. Cada envio tem sua própria instância de `Ping`. Cancelamento é propagado; o comportamento preciso do cancelamento interno depende da implementação do sistema operacional.

## Roteamento, plataforma e escopo

- Somente destino IPv4 unicast nesta fase; IPv6 e nomes DNS estão fora do escopo. Loopback, destino zero e multicast são rejeitados.
- O GUID WLAN é associado ao índice IPv4 do mesmo adaptador. `GetBestInterfaceEx` deve indicar esse índice antes de cada envio. Rotas por VPN/Ethernet resultam em `RouteRejected`, sem envio.
- A checagem não vincula `Ping.SendPingAsync` a um endereço de origem, e há uma corrida entre consulta da rota e envio. Uma captura de tráfego seria necessária para comprovar o caminho de cada pacote. Túneis/filtros de terceiros também podem alterar o caminho efetivo.
- Mais de um adaptador Wi-Fi conectado exige `--interface`. A seleção permanece fixa durante a sessão; não se migra silenciosamente para outro adaptador.
- A sessão acompanha a conexão atual desse adaptador. Se o usuário trocar de SSID, os snapshots mostram os bytes dos SSIDs diferentes; uma mudança de BSSID com SSID diferente recebe uma classificação distinta. Não há média global que misture as redes.
- O alvo de validação inicial é Windows 11 x64. Outras arquiteturas/versões do Windows e drivers precisam de validação própria; o uso de `net10.0` não implica suporte à coleta em Linux.
- Não há SNR, ruído, ocupação real de canal/airtime, interferência, retransmissões 802.11, largura de canal validada, MCS/NSS, MLO por enlace, throughput, recomendação de canal, estimativa de distância ou certificação regulatória brasileira.
- O foco inicial no Brasil não altera a leitura das APIs. Não há tabela ANATEL embutida nem classificação de canais como permitidos. Essa análise exigiria uma etapa separada, documentada e atualizada.

## Permissões e gravação

O Windows pode restringir chamadas que revelam BSSID conforme a permissão de localização. O aplicativo não altera configurações nem solicita elevação automaticamente. Os erros indicam o código original e orientam a conferir **Configurações > Privacidade e segurança > Localização**, inclusive acesso de aplicativos da área de trabalho quando disponível. Políticas corporativas também podem bloquear o acesso.

WlanSvc parado, driver incompatível, adaptador removido e radio desligado podem resultar em códigos diferentes conforme o sistema. O código original é preservado para análise. Falhas anteriores à criação da sessão são apresentadas em stderr, sem gerar um arquivo que pareça uma medição realizada.

O JSON inclui os valores nulos e enums textuais, com `schemaVersion=1`. Ele é serializado em arquivo temporário no mesmo diretório e renomeado sem sobrescrita após conclusão. Isso evita publicar um JSON incompleto no caminho final em falhas normais; não garante durabilidade contra queda de energia. Somente `Ctrl+C` tratado permite tentar salvar uma sessão parcial. Não há autosave por amostra, telemetria nem upload.

## Referências oficiais

- [WlanOpenHandle](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanopenhandle)
- [WlanEnumInterfaces](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanenuminterfaces)
- [WlanQueryInterface](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanqueryinterface)
- [WLAN_INTF_OPCODE](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/ne-wlanapi-wlan_intf_opcode)
- [WlanGetNetworkBssList](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlangetnetworkbsslist)
- [WLAN_BSS_ENTRY](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/ns-wlanapi-wlan_bss_entry)
- [WlanFreeMemory](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanfreememory)
- [WlanCloseHandle](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanclosehandle)
- [Mudanças de acesso Wi-Fi e localização](https://learn.microsoft.com/en-us/windows/win32/nativewifi/wi-fi-access-location-changes)
- [GetBestInterfaceEx](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getbestinterfaceex)
- [Ping.SendPingAsync no .NET 10](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ping.sendpingasync?view=net-10.0)
- [Stopwatch](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch?view=net-10.0)
