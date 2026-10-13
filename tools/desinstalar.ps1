<#
    Remove a Tela: fecha-a, apaga a instalação, o atalho do menu Iniciar e as entradas no
    Explorador ("Abrir com" e o botão direito nas pastas).

    As definições, o histórico (onde ficou cada vídeo) e as imagens do "continuar a ver" em
    %APPDATA%\Tela ficam, a não ser que se use -Tudo. Com -WhatIf mostra o que faria.
#>

[CmdletBinding(SupportsShouldProcess)]
param([Alias("All")][switch]$Tudo)

$ErrorActionPreference = "Continue"

$destino = Join-Path $env:LOCALAPPDATA "Programs\Tela"
$atalho = Join-Path ([Environment]::GetFolderPath("Programs")) "Tela.lnk"
$dados = Join-Path $env:APPDATA "Tela"
$chaves = @(
    "HKCU:\Software\Classes\Applications\Tela.exe",
    "HKCU:\Software\Classes\Directory\shell\Tela.Play",
    "HKCU:\Software\Classes\Tela.Video",
    "HKCU:\Software\zimutek\Tela"
)

function Feito($texto) { Write-Host "  $texto" -ForegroundColor Green }

# Apagar uma junção pelo caminho apaga o destino real: nesse caso não se toca em nada.
function Remover-Pasta($pasta, $descricao) {
    if (-not (Test-Path -LiteralPath $pasta)) { return }
    $item = Get-Item -LiteralPath $pasta -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        Write-Host "  $pasta é uma junção/ligação: não foi apagada" -ForegroundColor Yellow
        return
    }
    if ($PSCmdlet.ShouldProcess($pasta, "Apagar")) {
        Remove-Item -LiteralPath $pasta -Recurse -Force
        Feito $descricao
    }
}

Write-Host "`nTela — remoção`n" -ForegroundColor White

$processos = Get-Process Tela -ErrorAction SilentlyContinue
if ($processos -and $PSCmdlet.ShouldProcess("Tela.exe", "Fechar")) {
    $processos | Stop-Process -Force
    Start-Sleep -Milliseconds 700
    Feito "app fechada"
}

if ((Test-Path -LiteralPath $atalho) -and $PSCmdlet.ShouldProcess($atalho, "Apagar")) {
    Remove-Item -LiteralPath $atalho -Force
    Feito "atalho do menu Iniciar removido"
}

foreach ($chave in $chaves) {
    if ((Test-Path -LiteralPath $chave) -and $PSCmdlet.ShouldProcess($chave, "Apagar")) {
        Remove-Item -LiteralPath $chave -Recurse -Force
        Feito "registo removido: $chave"
    }
}

# Nas extensões só se tira o valor da Tela: o resto do OpenWithProgids é de outras apps.
$listas = Get-ChildItem "HKCU:\Software\Classes" -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName.StartsWith(".") } |
    ForEach-Object { Join-Path $_.PSPath "OpenWithProgids" } |
    Where-Object { Test-Path -LiteralPath $_ }
foreach ($lista in $listas) {
    $item = Get-Item -LiteralPath $lista
    if ($item.GetValueNames() -contains "Tela.Video" -and $PSCmdlet.ShouldProcess("$($item.Name) (Tela.Video)", "Apagar")) {
        Remove-ItemProperty -LiteralPath $lista -Name "Tela.Video"
    }
}
$registadas = "HKCU:\Software\RegisteredApplications"
if ((Test-Path -LiteralPath $registadas) -and (Get-Item -LiteralPath $registadas).GetValueNames() -contains "Tela" -and
        $PSCmdlet.ShouldProcess("$registadas (Tela)", "Apagar")) {
    Remove-ItemProperty -LiteralPath $registadas -Name "Tela"
    Feito "registo removido: Abrir com e Aplicações predefinidas"
}
$zimutek = "HKCU:\Software\zimutek"
if ((Test-Path -LiteralPath $zimutek) -and -not (Get-ChildItem -LiteralPath $zimutek) -and
        (Get-Item -LiteralPath $zimutek).ValueCount -eq 0 -and $PSCmdlet.ShouldProcess($zimutek, "Apagar")) {
    Remove-Item -LiteralPath $zimutek
}

Remover-Pasta $destino "ficheiros removidos"

if ($Tudo) {
    Remover-Pasta $dados "definições e histórico removidos"
} elseif (Test-Path -LiteralPath $dados) {
    Write-Host "  As definições e o histórico ficaram em $dados (use -Tudo para apagar também)."
}

Add-Type -Namespace Tela -Name Shell -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, IntPtr a, IntPtr b);'
[Tela.Shell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Host ""
