[CmdletBinding()]
param(
    [string]$ArtifactRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$releaseVersion = [string]$buildProperties.Project.PropertyGroup.VersionPrefix
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'A versão deve ter o formato major.minor.patch.' }
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $repositoryRoot 'artifacts/releases' }
$destination = Join-Path ([IO.Path]::GetFullPath($ArtifactRoot)) $releaseVersion
$desktopExe = Join-Path $destination "SinalMap-v$releaseVersion-win-x64.exe"
$consoleZip = Join-Path $destination "SinalMap-Console-v$releaseVersion-win-x64.zip"
$checksums = Join-Path $destination 'SHA256SUMS.txt'
foreach ($file in @($desktopExe, $consoleZip, $checksums)) {
    if (Test-Path -LiteralPath $file) { throw "O arquivo já existe e não será sobrescrito: $file" }
}
$staging = Join-Path $destination ('staging-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$buildRoot = Join-Path $staging 'build'
$desktopFolder = Join-Path $staging 'Desktop'
& dotnet publish (Join-Path $repositoryRoot 'src/SinalMap.Desktop.Wpf') -c Release -r win-x64 --self-contained true `
    --artifacts-path $buildRoot -o $desktopFolder '-p:PublishProfile=Portable-win-x64' `
    '-p:DebugType=None' '-p:DebugSymbols=false' '-p:IncludeSourceRevisionInInformationalVersion=false'
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar a janela em executável único.' }
$desktopFiles = @(Get-ChildItem -LiteralPath $desktopFolder -Recurse -File)
if ($desktopFiles.Count -ne 1 -or $desktopFiles[0].Name -ne 'SinalMap.Desktop.Wpf.exe') {
    throw 'A publicação da janela deve produzir somente SinalMap.Desktop.Wpf.exe, sem DLLs ou outros arquivos externos.'
}

$consoleFolder = Join-Path $staging 'Console'
& dotnet publish (Join-Path $repositoryRoot 'src/SinalMap.Console.Probe') -c Release -r win-x64 --self-contained true `
    --artifacts-path $buildRoot -o $consoleFolder '-p:DebugType=None' '-p:DebugSymbols=false' '-p:IncludeSourceRevisionInInformationalVersion=false'
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o console.' }
if (-not (Test-Path -LiteralPath (Join-Path $consoleFolder 'SinalMap.Console.Probe.exe'))) { throw 'Executável do console não encontrado.' }
if (Get-ChildItem -LiteralPath $consoleFolder -Recurse -Filter '*.pdb') { throw 'O pacote não deve incluir símbolos de depuração.' }
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $consoleFolder
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/como-executar.md') -Destination (Join-Path $consoleFolder 'LEIA-ME.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs/releases/v$releaseVersion.md") -Destination (Join-Path $consoleFolder 'NOTAS-DA-VERSAO.md')
Copy-Item -LiteralPath $desktopFiles[0].FullName -Destination $desktopExe
Compress-Archive -Path (Join-Path $consoleFolder '*') -DestinationPath $consoleZip -CompressionLevel Optimal
$hashLines = foreach ($file in @($desktopExe, $consoleZip)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($file))"
}
[IO.File]::WriteAllLines($checksums, $hashLines, [Text.UTF8Encoding]::new($false))
Write-Output "Pacotes publicados em: $destination"
Write-Output "Diretório de inspeção: $staging"
