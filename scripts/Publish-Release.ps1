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
$desktopZip = Join-Path $destination "SinalMap-v$releaseVersion-win-x64.zip"
$consoleZip = Join-Path $destination "SinalMap-Console-v$releaseVersion-win-x64.zip"
$checksums = Join-Path $destination 'SHA256SUMS.txt'
foreach ($file in @($desktopZip, $consoleZip, $checksums)) {
    if (Test-Path -LiteralPath $file) { throw "O arquivo já existe e não será sobrescrito: $file" }
}
$staging = Join-Path $destination ('staging-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$packages = @(
    @{ Project = 'src/SinalMap.Desktop.Wpf'; Folder = 'Desktop'; Executable = 'SinalMap.Desktop.Wpf.exe'; Zip = $desktopZip },
    @{ Project = 'src/SinalMap.Console.Probe'; Folder = 'Console'; Executable = 'SinalMap.Console.Probe.exe'; Zip = $consoleZip }
)
foreach ($package in $packages) {
    $publishFolder = Join-Path $staging $package.Folder
    & dotnet publish (Join-Path $repositoryRoot $package.Project) -c Release -r win-x64 --self-contained true `
        -o $publishFolder '-p:DebugType=None' '-p:DebugSymbols=false' '-p:IncludeSourceRevisionInInformationalVersion=false'
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar $($package.Project)." }
    if (-not (Test-Path -LiteralPath (Join-Path $publishFolder $package.Executable))) { throw 'Executável não encontrado na publicação.' }
    if (Get-ChildItem -LiteralPath $publishFolder -Recurse -Filter '*.pdb') { throw 'O pacote não deve incluir símbolos de depuração.' }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishFolder
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/como-executar.md') -Destination (Join-Path $publishFolder 'LEIA-ME.md')
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "docs/releases/v$releaseVersion.md") -Destination (Join-Path $publishFolder 'NOTAS-DA-VERSAO.md')
    Compress-Archive -Path (Join-Path $publishFolder '*') -DestinationPath $package.Zip -CompressionLevel Optimal
}
$hashLines = foreach ($file in @($desktopZip, $consoleZip)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($file))"
}
[IO.File]::WriteAllLines($checksums, $hashLines, [Text.UTF8Encoding]::new($false))
Write-Output "Pacotes publicados em: $destination"
Write-Output "Diretório de inspeção: $staging"
