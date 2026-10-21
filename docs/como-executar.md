# Como executar o SinalMap 1.1.0 no Windows

## Janela gráfica

1. Baixe `SinalMap-v1.1.0-win-x64.zip` na release oficial do repositório.
2. Extraia o ZIP inteiro para uma pasta local. Não abra o executável diretamente de dentro do ZIP nem copie somente o `.exe`.
3. Abra `SinalMap.Desktop.Wpf.exe`. O pacote já inclui o .NET necessário.
4. Se houver mais de uma interface Wi-Fi conectada, selecione a desejada. O campo **Rede conectada · SSID** mostra o nome da rede informado pelo Windows. A aba **Visão geral** acompanha o sinal sem iniciar um teste ICMP.
5. Para salvar uma sessão, confira o destino IPv4, expanda **Pasta de resultados** se precisar alterar a pasta e clique em **Coletar por 60 segundos**. O gateway é sugerido quando o Windows informa um único candidato válido para a interface; o destino continua editável. Um endereço inválido impede o início e recebe orientação junto ao campo.
6. Use **Abrir coleta** (`Ctrl+O`) para analisar um JSON anterior e **Ao vivo** (`Ctrl+L`) para voltar ao monitoramento. O histórico deixa explícito que o monitor está pausado.

O aplicativo requer Windows x64, adaptador Wi-Fi, driver e serviço WLAN AutoConfig em execução. Respeita o consentimento de localização quando exigido pelo Windows. Não requer executar como administrador para substituir esse consentimento.

A janela solicita varreduras Wi-Fi a cada 5 s, inclusive durante testes; isso pode aumentar o RTT. A tela e o JSON registram essa condição. Cancelar ou fechar uma coleta aguarda as operações pendentes e tenta salvar a sessão parcial. Resultados ficam, por padrão, em `Documentos/SinalMap/results`.

O visor e a régua mostram o mesmo RSSI em dBm; sem uma leitura válida, o visor mostra um traço e a régua não apresenta marcador. Para inspecionar o gráfico, passe o mouse sobre as leituras ou use as setas, `Home` e `End` com o gráfico em foco; `Esc` encerra a seleção. As abas de análise aparecem quando há uma coleta disponível. A tabela permite copiar a seleção com `Ctrl+C`.

Se o salvamento falhar, os dados permanecem em memória. Use **Escolher outra pasta…** e **Tentar salvar novamente**. Descartar a sessão exige confirmação; encerrar o processo à força pode perder esses dados.

Se a janela ficar branca por uma falha de aceleração gráfica, abra um PowerShell na pasta extraída e execute:

```powershell
./SinalMap.Desktop.Wpf.exe --software-rendering
```

Essa alternativa usa renderização por software somente no processo, sem modificar o Windows.

## Console

Extraia `SinalMap-Console-v1.1.0-win-x64.zip` e abra um PowerShell nessa pasta:

```powershell
./SinalMap.Console.Probe.exe --list
./SinalMap.Console.Probe.exe --help
```

Para coletar, informe `--target` com um IPv4 autorizado. A coleta padrão do console não inicia as varreduras periódicas da janela. Para analisar um arquivo existente:

```powershell
./SinalMap.Console.Probe.exe --analyze ./ponto-01.json
```

## Verificar o download

O arquivo `SHA256SUMS.txt` da release contém os hashes dos dois ZIPs. Compare o valor com:

```powershell
Get-FileHash ./SinalMap-v1.1.0-win-x64.zip -Algorithm SHA256
```

Não há importação de plantas, mapas de calor, teste de velocidade ou medição de PHY/MIMO/largura de canal nesta versão. Dados ausentes permanecem indisponíveis. O JSON contém identificadores da rede; revise antes de compartilhar.
