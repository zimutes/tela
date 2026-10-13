# Compila a Tela e instala-a para o utilizador atual:
#   %LOCALAPPDATA%\Programs\Tela\Tela.exe (+ libmpv-2.dll ao lado)
#   + atalho no menu Iniciar, "Abrir com" nos formatos de vídeo (e nas "Aplicações
#     predefinidas" do Windows) e, no botão direito das pastas, «Ver na Tela» (a pasta inteira,
#     pela ordem natural: uma temporada, por exemplo).
# Usa o runtime .NET 10 já instalado. Para um .exe que corre em qualquer PC (sem .NET), usar
# -Autonomo. -SemMenus não mexe no botão direito do Explorador. -SemInstalar só compila.
param([switch]$Autonomo, [switch]$SemInstalar, [switch]$SemMenus)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot
$projeto = Join-Path $raiz 'src\Tela\Tela.csproj'
$saida = Join-Path $raiz 'publicar\win-x64'

# Sem a libmpv a Tela abre, mas não mostra vídeo: vai-se buscá-la primeiro.
& (Join-Path $PSScriptRoot 'obter-libmpv.ps1')

# As bibliotecas nativas do Avalonia (Skia, HarfBuzz) vão dentro do .exe; a libmpv fica ao lado.
$dotnetArgs = @('publish', $projeto, '-c', 'Release', '-r', 'win-x64', '-o', $saida,
          '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=none')
if ($Autonomo) { $dotnetArgs += @('--self-contained', '-p:EnableCompressionInSingleFile=true') }
else { $dotnetArgs += @('--no-self-contained') }

if (Test-Path -LiteralPath $saida) { Remove-Item -LiteralPath $saida -Recurse -Force }
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou.' }
Write-Host "Publicado em $saida"
if ($SemInstalar) { return }

$destino = Join-Path $env:LOCALAPPDATA 'Programs\Tela'
# Se estiver aberta, fecha-se com jeito (para guardar onde ficou o vídeo) e só depois à força.
foreach ($p in Get-Process Tela -ErrorAction SilentlyContinue) {
    $p.CloseMainWindow() | Out-Null
    if (-not $p.WaitForExit(6000)) { $p.Kill() }
}
Start-Sleep -Milliseconds 500
New-Item -ItemType Directory -Force -Path $destino | Out-Null
foreach ($f in 'Tela.exe', 'libmpv-2.dll') {
    Copy-Item -LiteralPath (Join-Path $saida $f) -Destination $destino -Force
}
$exe = Join-Path $destino 'Tela.exe'

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Tela.lnk'))
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $destino
$lnk.Description = 'Video player · by zimutek'
$lnk.Save()

# "Abrir com": a Tela aparece na lista destes formatos e nas "Aplicações predefinidas" do
# Windows, mas não passa a ser a app por omissão (isso escolhe-se lá, se se quiser).
$video = '.mkv', '.mp4', '.m4v', '.mov', '.avi', '.webm', '.wmv', '.asf', '.flv', '.f4v', '.mpg', '.mpeg',
    '.m2v', '.m2ts', '.mts', '.ts', '.vob', '.ogv', '.3gp', '.3g2', '.divx', '.rm', '.rmvb', '.mxf', '.dv',
    '.y4m', '.ivf', '.hevc', '.h264', '.mk3d'
$abrir = "`"$exe`" `"%1`""
$app = 'HKCU:\Software\Classes\Applications\Tela.exe'
New-Item -Path "$app\shell\open\command" -Force | Out-Null
Set-Item -Path "$app\shell\open\command" -Value $abrir
Set-ItemProperty -Path $app -Name 'FriendlyAppName' -Value 'Tela'
New-Item -Path "$app\DefaultIcon" -Force | Out-Null
Set-Item -Path "$app\DefaultIcon" -Value "`"$exe`",0"
New-Item -Path "$app\SupportedTypes" -Force | Out-Null
foreach ($f in $video) { Set-ItemProperty -Path "$app\SupportedTypes" -Name $f -Value '' }

# Um ProgID e o ProgID no OpenWithProgids de cada extensão: é daqui que o submenu "Abrir com"
# tira as apps. MultiSelectModel=Player: o verbo aparece com qualquer número de ficheiros.
$capacidades = 'HKCU:\Software\zimutek\Tela\Capabilities'
New-Item -Path "$capacidades\FileAssociations" -Force | Out-Null
Set-ItemProperty -Path $capacidades -Name 'ApplicationName' -Value 'Tela'
Set-ItemProperty -Path $capacidades -Name 'ApplicationDescription' -Value 'Video player · by zimutek'
$progId = 'HKCU:\Software\Classes\Tela.Video'
New-Item -Path "$progId\shell\open\command" -Force | Out-Null
Set-Item -Path $progId -Value 'Video'
Set-ItemProperty -Path $progId -Name 'FriendlyTypeName' -Value 'Video'
New-Item -Path "$progId\DefaultIcon" -Force | Out-Null
Set-Item -Path "$progId\DefaultIcon" -Value "`"$exe`",0"
Set-ItemProperty -Path "$progId\shell\open" -Name 'MultiSelectModel' -Value 'Player'
Set-Item -Path "$progId\shell\open\command" -Value $abrir
foreach ($f in $video) {
    $lista = "HKCU:\Software\Classes\$f\OpenWithProgids"
    if (-not (Test-Path -LiteralPath $lista)) { New-Item -Path $lista -Force | Out-Null }
    New-ItemProperty -LiteralPath $lista -Name 'Tela.Video' -PropertyType None -Value ([byte[]]@()) -Force | Out-Null
    Set-ItemProperty -Path "$capacidades\FileAssociations" -Name $f -Value 'Tela.Video'
}
Set-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' -Name 'Tela' -Value 'Software\zimutek\Tela\Capabilities'

if (-not $SemMenus) {
    $pt = (Get-UICulture).TwoLetterISOLanguageName -eq 'pt'
    $chave = 'HKCU:\Software\Classes\Directory\shell\Tela.Play'
    New-Item -Path "$chave\command" -Force | Out-Null
    Set-ItemProperty -Path $chave -Name 'MUIVerb' -Value $(if ($pt) { 'Ver na Tela' } else { 'Watch in Tela' })
    Set-ItemProperty -Path $chave -Name 'Icon' -Value "`"$exe`",0"
    Set-ItemProperty -Path $chave -Name 'MultiSelectModel' -Value 'Player'
    Set-Item -Path "$chave\command" -Value $abrir
}

# Avisa o Explorador de que as associações mudaram (senão só se vê depois de reiniciar).
Add-Type -Namespace Tela -Name Shell -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, IntPtr a, IntPtr b);'
[Tela.Shell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)

Write-Host "Instalado em $destino (atalho no menu Iniciar; «Abrir com» e botão direito nas pastas)."
