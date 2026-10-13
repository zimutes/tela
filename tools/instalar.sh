#!/bin/sh
#
#   Instala a Tela no Linux, só para o utilizador atual (sem root):
#     ~/.local/opt/tela/Tela          o executável (autónomo, não precisa do .NET)
#     ~/.local/bin/tela                 atalho para a linha de comandos
#     ~/.local/share/applications/…       entrada no menu de aplicações e "Abrir com"
#
#   A Tela usa a libmpv da distribuição (o motor de vídeo do mpv); se faltar, o script diz
#   como a instalar.
#
#   Se houver uma Tela já compilada ao lado deste script (o pacote da release), instala essa.
#   Senão, compila a partir do código (precisa do .NET 10 SDK).
#
#   Uso:
#       ./instalar.sh                instala ou atualiza
#       ./instalar.sh --remover      remove (as definições em ~/.config/tela ficam)
#

set -eu

aqui=$(cd "$(dirname "$0")" && pwd)
destino="$HOME/.local/opt/tela"
atalho="$HOME/.local/bin/tela"
menu="$HOME/.local/share/applications/tela.desktop"

if [ "${1:-}" = "--remover" ] || [ "${1:-}" = "--uninstall" ]; then
    rm -f "$atalho" "$menu"
    rm -rf "$destino"
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$HOME/.local/share/applications" || true
    echo "Tela removida. As definições e o histórico ficaram em ~/.config/tela."
    exit 0
fi

case "$(uname -m)" in
    x86_64|amd64) rid=linux-x64 ;;
    aarch64|arm64) rid=linux-arm64 ;;
    *) echo "Arquitetura não suportada: $(uname -m)" >&2; exit 1 ;;
esac

if [ -f "$aqui/Tela" ]; then
    origem="$aqui"
    icone="$aqui/tela.png"
else
    raiz=$(cd "$aqui/.." && pwd)
    command -v dotnet >/dev/null 2>&1 || { echo "Falta o .NET 10 SDK para compilar (ou use o pacote da release)." >&2; exit 1; }
    echo "A compilar ($rid)…"
    dotnet publish "$raiz/src/Tela" -c Release -r "$rid" --self-contained \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
        -o "$raiz/publicar/$rid"
    origem="$raiz/publicar/$rid"
    icone="$raiz/src/Tela/Assets/tela.png"
fi

mkdir -p "$destino" "$(dirname "$atalho")" "$(dirname "$menu")"
cp "$origem/Tela" "$destino/Tela"
chmod +x "$destino/Tela"
[ -f "$icone" ] && cp "$icone" "$destino/tela.png"
ln -sf "$destino/Tela" "$atalho"

cat > "$menu" <<EOF
[Desktop Entry]
Type=Application
Name=Tela
GenericName=Video Player
GenericName[pt]=Leitor de vídeo
Comment=Video player: all you need, nothing else · by zimutek
Comment[pt]=Leitor de vídeo: tudo o que precisa, nada mais · by zimutek
Exec="$destino/Tela" %F
Icon=$destino/tela.png
Terminal=false
Categories=AudioVideo;Video;Player;TV;
Keywords=video;movie;film;player;mpv;
MimeType=video/mp4;video/x-matroska;video/webm;video/quicktime;video/x-msvideo;video/avi;video/x-ms-wmv;video/x-ms-asf;video/x-flv;video/mpeg;video/mp2t;video/x-m4v;video/ogg;video/3gpp;video/3gpp2;video/x-ogm+ogg;video/dv;video/x-theora+ogg;video/vnd.rn-realvideo;video/x-mpeg2;application/x-matroska;
EOF
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$HOME/.local/share/applications" || true

echo "Tela instalada em $destino."

# A libmpv: o nome do ficheiro é o mesmo em todo o lado; o pacote, não.
if ! ldconfig -p 2>/dev/null | grep -q 'libmpv\.so\.2'; then
    if command -v dnf >/dev/null 2>&1; then
        echo "Atenção: falta a libmpv, o motor de vídeo (Fedora: sudo dnf install mpv-libs)."
    elif command -v apt-get >/dev/null 2>&1; then
        echo "Atenção: falta a libmpv, o motor de vídeo (Debian/Ubuntu: sudo apt install libmpv2)."
    elif command -v pacman >/dev/null 2>&1; then
        echo "Atenção: falta a libmpv, o motor de vídeo (Arch: sudo pacman -S mpv)."
    else
        echo "Atenção: falta a libmpv, o motor de vídeo (instale o pacote libmpv da sua distribuição)."
    fi
fi
if command -v dnf >/dev/null 2>&1 && rpm -q ffmpeg-free >/dev/null 2>&1; then
    # O ffmpeg-free do Fedora não traz HEVC (H.265) nem alguns formatos antigos.
    echo "Nota: com o ffmpeg-free do Fedora, vídeos HEVC (H.265) só tocam o som. O ffmpeg do RPM Fusion resolve:"
    echo "      sudo dnf swap ffmpeg-free ffmpeg --allowerasing   (depois de ativar o RPM Fusion)"
fi
case ":$PATH:" in
    *":$HOME/.local/bin:"*) ;;
    *) echo "Nota: ~/.local/bin não está no PATH; a Tela aparece no menu de aplicações na mesma." ;;
esac
