# Como executar o SinalMap 1.2.0 no Windows

## Janela gráfica

1. Baixe [SinalMap-v1.2.0-win-x64.exe](https://github.com/marcosmaldonadoo/SinalMap/releases/download/v1.2.0/SinalMap-v1.2.0-win-x64.exe) na release oficial do repositório.
2. Salve o arquivo em uma pasta local. Esse é o aplicativo portátil completo; não há ZIP ou DLLs que precisem ficar ao lado dele.
3. Abra `SinalMap-v1.2.0-win-x64.exe`. O .NET necessário está incluído; não é preciso instalar SDK ou runtime separadamente.
4. Se houver mais de uma interface Wi-Fi conectada, selecione a desejada. O campo **Rede conectada · SSID** mostra o nome da rede informado pelo Windows. A aba **Visão geral** acompanha o sinal sem iniciar um teste ICMP.
5. Para salvar uma sessão, confira o destino IPv4, expanda **Pasta de resultados** se precisar alterar a pasta e clique em **Coletar por 60 segundos**. O gateway é sugerido quando o Windows informa um único candidato válido para a interface; o destino continua editável. Um endereço inválido impede o início e recebe orientação junto ao campo.
6. Use **Abrir coleta** (`Ctrl+O`) para analisar um JSON anterior e **Ao vivo** (`Ctrl+L`) para voltar ao monitoramento. O histórico deixa explícito que o monitor está pausado.

O aplicativo requer Windows x64, adaptador Wi-Fi, driver e serviço WLAN AutoConfig em execução. Respeita o consentimento de localização quando exigido pelo Windows. Não requer executar como administrador para substituir esse consentimento. O download não é assinado digitalmente.

Na primeira abertura, as bibliotecas nativas incluídas são extraídas automaticamente para o cache do .NET em `%TEMP%/.net`; a pasta precisa permitir escrita. Isso faz parte da [publicação em arquivo único do .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#native-libraries). O aplicativo continua sendo distribuído por um único arquivo; seus projetos e resultados são arquivos de trabalho separados.

A janela solicita varreduras Wi-Fi a cada 5 s, inclusive durante testes; isso pode aumentar o RTT. A tela e o JSON registram essa condição. Cancelar ou fechar uma coleta aguarda as operações pendentes e tenta salvar a sessão parcial. Resultados ficam, por padrão, em `Documentos/SinalMap/results`.

O visor e a régua mostram o mesmo RSSI em dBm; sem uma leitura válida, o visor mostra um traço e a régua não apresenta marcador. Para inspecionar o gráfico, passe o mouse sobre as leituras ou use as setas, `Home` e `End` com o gráfico em foco; `Esc` encerra a seleção. As abas de análise aparecem quando há uma coleta disponível. A tabela permite copiar a seleção com `Ctrl+C`.

Se o salvamento falhar, os dados permanecem em memória. Use **Escolher outra pasta…** e **Tentar salvar novamente**. Descartar a sessão exige confirmação; encerrar o processo à força pode perder esses dados.

Se a janela ficar branca por uma falha de aceleração gráfica, abra um PowerShell na pasta do executável e execute:

```powershell
./SinalMap-v1.2.0-win-x64.exe --software-rendering
```

Essa alternativa usa renderização por software somente no processo, sem modificar o Windows.

## Medir na planta da loja

1. Na janela principal, confira a interface Wi-Fi, o destino IPv4 autorizado e a pasta dos resultados.
2. Abra **Planta da loja** e **Importar foto…**. Escolha uma imagem PNG/JPEG local, marque o roteador e crie os pontos com **+ Novo ponto**.
3. Selecione um ponto, vá até ele com o notebook e use **Medir neste ponto · 60 s**. Escolha onde salvar o projeto quando solicitado.
4. A planta mostra a leitura **Ao vivo** no ponto ativo. Após salvar, a cor e o valor passam a representar a média da última coleta desse ponto. Pontos sem coleta ou sem RSSI válido ficam neutros.
5. Use **Salvar projeto** e preserve também os JSONs originais. **Abrir projeto…** restaura imagem, marcações e vínculos, sem iniciar testes.

Abra e edite cada projeto em uma única instância por vez: uma cópia desatualizada pode substituir vínculos gravados por outra instância. A posição do notebook é informada manualmente; as cores não estimam o sinal entre os pontos. Consulte o [guia da planta](planta-pontos-medicao.md) para os limites e a recuperação de falhas.

## Console

Extraia `SinalMap-Console-v1.2.0-win-x64.zip` e abra um PowerShell nessa pasta:

```powershell
./SinalMap.Console.Probe.exe --list
./SinalMap.Console.Probe.exe --help
```

Para coletar, informe `--target` com um IPv4 autorizado. A coleta padrão do console não inicia as varreduras periódicas da janela. Para analisar um arquivo existente:

```powershell
./SinalMap.Console.Probe.exe --analyze ./ponto-01.json
```

## Verificar o download

O arquivo `SHA256SUMS.txt` da release contém os hashes do executável e do ZIP do console. Compare o valor com:

```powershell
Get-FileHash ./SinalMap-v1.2.0-win-x64.exe -Algorithm SHA256
```

Não há mapa de calor, simulação de propagação, teste de velocidade ou medição de PHY/MIMO/largura de canal nesta versão. Dados ausentes permanecem indisponíveis. Projetos de planta e JSONs podem conter informações privadas; revise antes de compartilhar.
