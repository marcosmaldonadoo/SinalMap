# Como executar o SinalMap 1.0.0 no Windows

## Janela gráfica

1. Baixe `SinalMap-v1.0.0-win-x64.zip` na release oficial do repositório.
2. Extraia o ZIP inteiro para uma pasta local. Não abra o executável diretamente de dentro do ZIP nem copie somente o `.exe`.
3. Abra `SinalMap.Desktop.Wpf.exe`. O pacote já inclui o .NET necessário.
4. Se houver mais de uma interface Wi-Fi conectada, selecione a desejada. A aba Sinal acompanha a conexão sem iniciar um teste ICMP.
5. Para salvar uma sessão, confira o destino IPv4 e a pasta e clique em **Iniciar medição · 60 s**. O gateway é sugerido quando o Windows informa um único candidato válido para a interface.
6. Use **Abrir coleta** para analisar um JSON anterior e **Sinal ao vivo** para voltar ao monitoramento.

O aplicativo requer Windows x64, adaptador Wi-Fi, driver e serviço WLAN AutoConfig em execução. Respeita o consentimento de localização quando exigido pelo Windows. Não requer executar como administrador para substituir esse consentimento.

A janela solicita varreduras Wi-Fi a cada 5 s, inclusive durante testes; isso pode aumentar o RTT. A tela e o JSON registram essa condição. Cancelar ou fechar uma coleta aguarda as operações pendentes e tenta salvar a sessão parcial. Resultados ficam, por padrão, em `Documentos/SinalMap/results`.

## Console

Extraia `SinalMap-Console-v1.0.0-win-x64.zip` e abra um PowerShell nessa pasta:

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
Get-FileHash ./SinalMap-v1.0.0-win-x64.zip -Algorithm SHA256
```

Não há importação de plantas, mapas de calor, teste de velocidade ou medição de PHY/MIMO/largura de canal nesta versão. Dados ausentes permanecem indisponíveis. O JSON contém identificadores da rede; revise antes de compartilhar.
