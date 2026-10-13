using System.Globalization;
using Avalonia;

namespace Tela;

public enum UiLanguage { En, Pt }

/// <summary>
/// Textos da interface, em inglês (por omissão) e em português de Portugal.
/// O XAML usa-os como {DynamicResource L.Chave}; os botões usam {DynamicResource LU.Chave}, que
/// fica em maiúsculas nos temas que as pedem. Trocar de língua atualiza a janela na hora.
/// </summary>
public static class Loc
{
    internal static readonly Dictionary<string, (string En, string Pt)> Texts = new()
    {
        ["Tagline"] = ("All you need. Nothing else.", "Tudo o que precisa. Nada mais."),
        ["By"] = ("by zimutek", "by zimutek"),
        ["About"] = ("A video player for Windows and Linux: everything you need to watch, and nothing in the way.",
                     "Um leitor de vídeo para Windows e Linux: tudo o que é preciso para ver, e nada no caminho."),
        ["Credits"] = ("Video: libmpv (mpv), FFmpeg · Interface: Avalonia · Fonts: SIL Open Font License",
                       "Vídeo: libmpv (mpv), FFmpeg · Interface: Avalonia · Fontes: SIL Open Font License"),

        // Ecrã inicial
        ["OpenFile"] = ("Open file", "Abrir ficheiro"),
        ["OpenFolder"] = ("Open folder", "Abrir pasta"),
        ["DropHint"] = ("Drop a video anywhere, or press O", "Arraste um vídeo para aqui, ou carregue em O"),
        ["ContinueWatching"] = ("Continue watching", "Continuar a ver"),
        ["TimeLeft"] = ("{0} left", "faltam {0}"),
        ["ForgetTip"] = ("Remove from this list", "Tirar desta lista"),
        ["MpvMissingTitle"] = ("The video engine (libmpv) is missing", "Falta o motor de vídeo (libmpv)"),
        ["MpvMissingWindows"] = ("libmpv-2.dll should be next to Tela.exe. Reinstall Tela, or put the file back in its folder.",
                                 "A libmpv-2.dll devia estar ao lado do Tela.exe. Reinstale a Tela, ou ponha o ficheiro de volta na pasta dela."),
        ["MpvMissingLinux"] = ("Install it from your distribution:\nFedora: sudo dnf install mpv-libs\nDebian / Ubuntu: sudo apt install libmpv2\nArch: sudo pacman -S mpv",
                               "Instale-o pela sua distribuição:\nFedora: sudo dnf install mpv-libs\nDebian / Ubuntu: sudo apt install libmpv2\nArch: sudo pacman -S mpv"),
        ["VideoFiles"] = ("Video and audio", "Vídeo e som"),
        ["SubtitleFiles"] = ("Subtitles", "Legendas"),

        // Barra de título
        ["Menu"] = ("Menu", "Menu"),
        ["Minimize"] = ("Minimize", "Minimizar"),
        ["Maximize"] = ("Maximize", "Maximizar"),
        ["Restore"] = ("Restore", "Restaurar"),
        ["Close"] = ("Close", "Fechar"),

        // Barra de comandos
        ["PlayTip"] = ("Play (Space)", "Tocar (Espaço)"),
        ["PauseTip"] = ("Pause (Space)", "Pausa (Espaço)"),
        ["PrevTip"] = ("Previous (P)", "Anterior (P)"),
        ["NextTip"] = ("Next (N)", "Seguinte (N)"),
        ["MuteTip"] = ("Mute (M)", "Sem som (M)"),
        ["VolumeTip"] = ("Volume (↑ ↓, wheel) · double-click: 100%", "Volume (↑ ↓, roda) · duplo clique: 100%"),
        ["TimeTip"] = ("Click: elapsed or remaining", "Clique: tempo decorrido ou que falta"),
        ["SpeedTip"] = ("Speed ([ ])", "Velocidade ([ ])"),
        ["SubsTip"] = ("Subtitles (V)", "Legendas (V)"),
        ["AudioTip"] = ("Audio track (B)", "Faixa de áudio (B)"),
        ["PipTip"] = ("Mini player, always on top (I)", "Mini-janela, sempre por cima (I)"),
        ["FullscreenTip"] = ("Full screen (F, double-click)", "Ecrã inteiro (F, duplo clique)"),

        // Mensagens
        ["Resumed"] = ("Resumed at {0} · Home to start over", "Continua em {0} · Home para recomeçar"),
        ["CantPlay"] = ("Can't play {0}", "Não consigo tocar {0}"),
        ["NoDecoder"] = ("This video's format ({0}) can't be decoded here: only the sound plays.",
                         "O formato deste vídeo ({0}) não se consegue descodificar aqui: só toca o som."),
        ["NoDecoderLinux"] = ("On Fedora, RPM Fusion's ffmpeg adds it: sudo dnf swap ffmpeg-free ffmpeg --allowerasing",
                              "No Fedora, o ffmpeg do RPM Fusion junta-o: sudo dnf swap ffmpeg-free ffmpeg --allowerasing"),
        ["VideoFailed"] = ("Video output failed: {0}", "A imagem falhou: {0}"),
        ["NoPlaylists"] = ("Tela doesn't follow playlists: open the videos or their folder instead",
                           "A Tela não segue listas de reprodução: abra os vídeos ou a pasta deles"),
        ["NothingToPlay"] = ("Nothing to play there", "Não há nada para ver aí"),
        ["SubtitleAdded"] = ("Subtitles: {0}", "Legendas: {0}"),
        ["LastInList"] = ("This is the last one", "Este é o último"),
        ["VolumeOsd"] = ("Volume {0}%", "Volume {0}%"),
        ["Muted"] = ("Muted", "Sem som"),
        ["SpeedOsd"] = ("Speed {0}", "Velocidade {0}"),
        ["ScreenshotSaved"] = ("Screenshot saved: {0}", "Captura guardada: {0}"),
        ["LoopA"] = ("Loop from {0} · press R again at the end", "Repetir a partir de {0} · R outra vez no fim"),
        ["LoopOn"] = ("Looping this part · R to stop", "A repetir este troço · R para parar"),
        ["LoopOff"] = ("Loop off", "Sem repetição"),
        ["RepeatOn"] = ("Repeating this video", "A repetir este vídeo"),
        ["RepeatOff"] = ("Not repeating", "Sem repetir"),
        ["SubDelayOsd"] = ("Subtitle delay {0}", "Atraso das legendas {0}"),
        ["AudioDelayOsd"] = ("Audio delay {0}", "Atraso do som {0}"),
        ["SubSizeOsd"] = ("Subtitle size {0}%", "Tamanho das legendas {0}%"),
        ["SubtitlesOsd"] = ("Subtitles: {0}", "Legendas: {0}"),
        ["AudioOsd"] = ("Audio: {0}", "Áudio: {0}"),
        ["NoSubtitles"] = ("No subtitles in this video", "Este vídeo não tem legendas"),
        ["NoAudio"] = ("No audio in this video", "Este vídeo não tem som"),
        ["OnTopOn"] = ("Always on top", "Sempre por cima"),
        ["OnTopOff"] = ("Not on top", "Já não fica por cima"),
        ["HwOn"] = ("Hardware decoding on", "Descodificação pela placa ligada"),
        ["HwOff"] = ("Hardware decoding off", "Descodificação pela placa desligada"),
        ["NormalizeOn"] = ("Even out volume: on", "Nivelar o volume: ligado"),
        ["NormalizeOff"] = ("Even out volume: off", "Nivelar o volume: desligado"),
        ["AspectOsd"] = ("Aspect ratio: {0}", "Proporção: {0}"),
        ["CropOn"] = ("Crop to fill", "Cortar para encher"),
        ["CropOff"] = ("Whole picture", "Imagem inteira"),
        ["DeinterlaceOn"] = ("Deinterlace on", "Desentrelaçar ligado"),
        ["DeinterlaceOff"] = ("Deinterlace off", "Desentrelaçar desligado"),

        // A seguir / fim
        ["UpNextIn"] = ("Up next in {0}", "A seguir, daqui a {0}"),
        ["PlayNow"] = ("Play now", "Ver já"),
        ["Cancel"] = ("Cancel", "Cancelar"),
        ["Finished"] = ("Finished", "Acabou"),
        ["WatchAgain"] = ("Watch again", "Ver outra vez"),
        ["NextVideo"] = ("Next video", "Vídeo seguinte"),
        ["PreviousVideo"] = ("Previous video", "Vídeo anterior"),
        ["CloseVideo"] = ("Close video", "Fechar o vídeo"),

        // Menus
        ["Play"] = ("Play", "Tocar"),
        ["Pause"] = ("Pause", "Pausa"),
        ["AddSubtitle"] = ("Add subtitles…", "Juntar legendas…"),
        ["InThisFolder"] = ("In this folder ({0})", "Nesta pasta ({0})"),
        ["Subtitles"] = ("Subtitles", "Legendas"),
        ["Audio"] = ("Audio", "Áudio"),
        ["Video"] = ("Picture", "Imagem"),
        ["Speed"] = ("Speed", "Velocidade"),
        ["Normal"] = ("normal", "normal"),
        ["Repeat"] = ("Repeat", "Repetir"),
        ["RepeatVideo"] = ("Repeat this video", "Repetir este vídeo"),
        ["LoopAb"] = ("Loop a part (A–B)", "Repetir um troço (A–B)"),
        ["Screenshot"] = ("Screenshot", "Captura de ecrã"),
        ["Theme"] = ("Theme", "Tema"),
        ["Language"] = ("Language", "Língua"),
        ["Settings"] = ("Settings", "Definições"),
        ["AutoplayNext"] = ("Play the next video automatically", "Passar ao vídeo seguinte sozinho"),
        ["ResumeOption"] = ("Resume where I left off", "Continuar onde fiquei"),
        ["FitWindow"] = ("Fit the window to the video", "Ajustar a janela ao vídeo"),
        ["ClickToPause"] = ("Click the video to pause", "Clicar no vídeo põe em pausa"),
        ["AlwaysOnTop"] = ("Always on top", "Sempre por cima"),
        ["HardwareDecoding"] = ("Hardware decoding", "Descodificar pela placa gráfica"),
        ["SoftwareVideo"] = ("Draw video in software (if the picture stays black)", "Desenhar o vídeo por software (se a imagem ficar preta)"),
        ["SoftwareVideoOn"] = ("Video drawn in software", "Vídeo desenhado por software"),
        ["SoftwareVideoOff"] = ("Back to the graphics card the next time Tela opens", "Volta à placa gráfica da próxima vez que abrir a Tela"),
        ["OpenScreenshots"] = ("Open the screenshots folder", "Abrir a pasta das capturas"),
        ["Shortcuts"] = ("Keyboard shortcuts", "Atalhos de teclado"),
        ["MediaInfo"] = ("Media info", "Informação técnica"),
        ["AboutMenu"] = ("About Tela", "Acerca da Tela"),
        ["Quit"] = ("Quit", "Sair"),
        ["Off"] = ("Off", "Desligadas"),
        ["SubSize"] = ("Size", "Tamanho"),
        ["SubBackground"] = ("Dark box behind", "Caixa escura por trás"),
        ["DelayNow"] = ("Delay ({0})", "Atraso ({0})"),
        ["DelayEarlier"] = ("100 ms earlier", "100 ms mais cedo"),
        ["DelayLater"] = ("100 ms later", "100 ms mais tarde"),
        ["DelayReset"] = ("Back to 0", "Voltar a 0"),
        ["PreferredSubs"] = ("Preferred language", "Língua preferida"),
        ["PreferredAudio"] = ("Preferred language", "Língua preferida"),
        ["LangFile"] = ("As in the file", "A do ficheiro"),
        ["Normalize"] = ("Even out volume", "Nivelar o volume"),
        ["Output"] = ("Output", "Saída"),
        ["DefaultDevice"] = ("System default", "A do sistema"),
        ["Aspect"] = ("Aspect ratio", "Proporção"),
        ["Auto"] = ("Automatic", "Automática"),
        ["Rotate"] = ("Rotate", "Rodar"),
        ["CropFill"] = ("Crop to fill the window", "Cortar para encher a janela"),
        ["Deinterlace"] = ("Deinterlace", "Desentrelaçar"),
        ["TrackN"] = ("Track {0}", "Faixa {0}"),
        ["Forced"] = ("forced", "forçadas"),
        ["External"] = ("file", "ficheiro"),
        ["Mono"] = ("mono", "mono"),
        ["Stereo"] = ("stereo", "estéreo"),

        // Informação técnica
        ["NoFile"] = ("No video open", "Nenhum vídeo aberto"),
        ["InfoVideo"] = ("Video    ", "Vídeo    "),
        ["InfoAudio"] = ("Audio    ", "Áudio    "),
        ["InfoSubs"] = ("Subtitles", "Legendas "),
        ["InfoBitrate"] = ("Bitrate  ", "Débito   "),
        ["InfoDropped"] = ("Dropped  ", "Perdidas "),
        ["InfoCache"] = ("cache", "memória"),
        ["InfoSoftware"] = ("software decoding", "descodificação por software"),

        // Atalhos
        ["K.Click"] = ("click", "clique"),
        ["K.Wheel"] = ("wheel", "roda"),
        ["K.DoubleClick"] = ("double-click", "duplo clique"),
        ["K.Play"] = ("Play / pause", "Tocar / pausa"),
        ["K.Seek"] = ("Back / forward 5 s · 10 s", "Recuar / avançar 5 s · 10 s"),
        ["K.Fine"] = ("1 s · one frame at a time", "1 s · imagem a imagem"),
        ["K.Jump"] = ("Jump to 0–90% · to the start", "Saltar para 0–90% · para o início"),
        ["K.Chapters"] = ("Previous / next chapter", "Capítulo anterior / seguinte"),
        ["K.Volume"] = ("Volume · mute", "Volume · sem som"),
        ["K.Fullscreen"] = ("Full screen · leave", "Ecrã inteiro · sair"),
        ["K.Pip"] = ("Mini player · always on top", "Mini-janela · sempre por cima"),
        ["K.NextPrev"] = ("Next / previous video in the folder", "Vídeo seguinte / anterior da pasta"),
        ["K.Tracks"] = ("Next subtitles · next audio track", "Legendas seguintes · faixa de áudio seguinte"),
        ["K.Delay"] = ("Subtitle delay · audio delay (−/+ 100 ms)", "Atraso das legendas · do som (−/+ 100 ms)"),
        ["K.SubSize"] = ("Smaller / bigger subtitles", "Legendas mais pequenas / maiores"),
        ["K.Speed"] = ("Slower / faster · normal speed", "Mais devagar / mais depressa · velocidade normal"),
        ["K.Loop"] = ("Loop a part (A, B, off) · repeat the video", "Repetir um troço (A, B, parar) · repetir o vídeo"),
        ["K.Picture"] = ("Aspect ratio · crop to fill · deinterlace", "Proporção · cortar para encher · desentrelaçar"),
        ["K.Screenshot"] = ("Screenshot · without subtitles", "Captura · sem legendas"),
        ["K.Info"] = ("Media info", "Informação técnica"),
        ["K.Open"] = ("Open file · folder · paste", "Abrir ficheiro · pasta · colar"),
        ["K.Close"] = ("Close the video · quit", "Fechar o vídeo · sair"),
    };

    public static UiLanguage Language { get; private set; } = UiLanguage.En;
    public static bool Upper { get; private set; }

    public static CultureInfo Culture => Language == UiLanguage.Pt ? Pt.Value : En.Value;

    // Sem ICU (Linux mínimo, ou DOTNET_SYSTEM_GLOBALIZATION_INVARIANT) só há a cultura invariante.
    private static readonly Lazy<CultureInfo> Pt = new(() => Find("pt-PT"));
    private static readonly Lazy<CultureInfo> En = new(() => Find("en-GB"));

    private static CultureInfo Find(string name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    public static event Action? Changed;

    public static string T(string key) =>
        Texts.TryGetValue(key, out var v) ? (Language == UiLanguage.Pt ? v.Pt : v.En) : key;

    public static string F(string key, params object[] args) => string.Format(Culture, T(key), args);

    public static UiLanguage Parse(string? code) => code == "pt" ? UiLanguage.Pt : UiLanguage.En;

    public static string Code(UiLanguage language) => language == UiLanguage.Pt ? "pt" : "en";

    public static void Apply(UiLanguage language, bool upper)
    {
        Language = language;
        Upper = upper;
        var resources = Application.Current?.Resources;
        if (resources is not null)
            foreach (var key in Texts.Keys)
            {
                var text = T(key);
                resources["L." + key] = text;
                resources["LU." + key] = upper ? text.ToUpper(Culture) : text;
            }
        Changed?.Invoke();
    }
}
