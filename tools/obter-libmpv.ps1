# Descarrega a libmpv para Windows (a libmpv-2.dll, o motor de vídeo) para lib\win-x64\.
# É a compilação de shinchiro (github.com/shinchiro/mpv-winbuild-cmake), a que o site do mpv
# indica para Windows. A versão está fixada aqui, com a soma de verificação: para atualizar,
# mudar as três linhas de baixo (a soma é a do .7z, sha256sum).
param([switch]$Forcar)

$ErrorActionPreference = 'Stop'
$etiqueta = '20261013'
$ficheiro = 'mpv-dev-x86_64-20261013-git-9d144e57b6.7z'
$soma = '10860837bc04491862c1e66a95dc1af1d7886d69757ce447a09e1e49ae993b52'

$raiz = Split-Path $PSScriptRoot
$destino = Join-Path $raiz 'lib\win-x64'
$dll = Join-Path $destino 'libmpv-2.dll'
if ((Test-Path -LiteralPath $dll) -and -not $Forcar) {
    Write-Host "Já existe: $dll (use -Forcar para descarregar outra vez)"
    return
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ("tela-libmpv-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
try {
    $arquivo = Join-Path $temp $ficheiro
    $url = "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/$etiqueta/$ficheiro"
    Write-Host "A descarregar $url"
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $url -OutFile $arquivo -UseBasicParsing
    $obtida = (Get-FileHash -LiteralPath $arquivo -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($obtida -ne $soma) { throw "Soma de verificação errada: $obtida (esperada $soma)" }

    # O 7-Zip abre o .7z; sem ele, o tar do Windows (libarchive) também sabe.
    $extraido = Join-Path $temp 'x'
    $sete = @("$env:ProgramFiles\7-Zip\7z.exe", "${env:ProgramFiles(x86)}\7-Zip\7z.exe", (Get-Command 7z -ErrorAction SilentlyContinue).Source) |
        Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if ($sete) {
        & $sete x -y "-o$extraido" $arquivo | Out-Null
    } else {
        New-Item -ItemType Directory -Force -Path $extraido | Out-Null
        & tar -xf $arquivo -C $extraido
    }
    if ($LASTEXITCODE -ne 0) { throw 'Não consegui abrir o arquivo da libmpv.' }

    New-Item -ItemType Directory -Force -Path $destino | Out-Null
    Copy-Item -LiteralPath (Join-Path $extraido 'libmpv-2.dll') -Destination $dll -Force
    Write-Host "libmpv pronta: $dll ($([math]::Round((Get-Item -LiteralPath $dll).Length / 1MB)) MB)"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
