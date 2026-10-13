using System.Globalization;
using Tela.Controls;
using Tela.Library;
using Tela.Mpv;
using Tela.Platform;
using Tela.Themes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Tela.Views;

/// <summary>
/// A janela da Tela. Este ficheiro liga o leitor (libmpv) à interface: abrir, retomar onde
/// ficou, a lista da pasta, o histórico. Os outros parciais tratam da janela e do teclado
/// (Window), dos controlos por cima do vídeo (Overlay), dos menus (Menus) e do ecrã inicial (Home).
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSettings _settings = App.Settings;
    private readonly History _history = History.Load(SettingsStore.Folder);
    private readonly PlayQueue _queue = new();
    private readonly ScreenSaver _screenSaver = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer _autosave = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly bool _demo;

    private MpvPlayer? _player;
    private bool _videoReady;
    private (List<string> Items, int Start, List<string> Subs)? _pendingOpen;

    // Estado do ficheiro aberto (vem das propriedades observadas).
    private string? _current;
    private bool _loaded;
    private bool _paused = true;
    private bool _eof;
    private double _duration;
    private bool _seekable;
    private bool _hasVideo;
    private long _dw, _dh;
    private string? _mediaTitle;
    private List<MediaTrack> _tracks = [];
    private List<Chapter> _chapters = [];
    private double? _resumedFrom;
    private bool _fitPending;
    private bool _firstFit = true;
    /// <summary>Faixas escolhidas à mão neste ficheiro (vão para o histórico).</summary>
    private string? _chosenAid, _chosenSid;
    /// <summary>Língua das faixas escolhidas à mão: o episódio seguinte abre com as mesmas.</summary>
    private string? _sessionAlang, _sessionSlang;
    private bool _silentReload;
    private bool _sessionSubsOff;

    public MainWindow() : this(demo: false)
    {
    }

    /// <param name="demo">Para as capturas: sem libmpv, com um "vídeo" de demonstração.</param>
    public MainWindow(bool demo)
    {
        _demo = demo;
        InitializeComponent();
        SetupWindow();
        SetupControls();
        SetupHome();
        ApplyThemeClasses();
        ThemeManager.Changed += ApplyThemeClasses;
        Loc.Changed += OnLanguageChanged;

        _clock.Tick += (_, _) => Tick();
        _clock.Start();
        _autosave.Tick += (_, _) =>
        {
            SaveProgress(thumb: false);
            SettingsStore.Save(_settings);
        };
        _autosave.Start();

        if (demo)
        {
            ShowHome();
            return;
        }
        if (!Native.IsAvailable(out var error))
        {
            ShowMissingLibmpv(error);
            ShowHome();
            return;
        }
        StartPlayer();
        ShowHome();
    }

    // ── Leitor ────────────────────────────────────────────────────────────────

    private void StartPlayer()
    {
        var options = new Dictionary<string, string>
        {
            ["vo"] = "libmpv",
            ["hwdec"] = _settings.HardwareDecoding ? "auto-safe" : "no",
            ["keep-open"] = "yes",
            ["idle"] = "yes",
            ["osc"] = "no",
            ["osd-level"] = "0",
            ["osd-bar"] = "no",
            ["volume-max"] = "150",
            ["volume"] = MpvPlayer.Seconds(_settings.Volume),
            ["mute"] = _settings.Muted ? "yes" : "no",
            ["sub-auto"] = "fuzzy",
            ["sub-file-paths"] = "subs:Subs:subtitles:Subtitles:legendas:Legendas",
            ["subs-with-matching-audio"] = "forced",
            ["sub-scale"] = MpvPlayer.Seconds(_settings.SubScale),
            ["sub-border-style"] = _settings.SubBackground ? "background-box" : "outline-and-shadow",
            ["sub-back-color"] = "#C0000000",
            ["slang"] = _settings.SubLanguages,
            ["alang"] = _settings.AudioLanguages,
            ["audio-device"] = _settings.AudioDevice,
            ["demuxer-max-bytes"] = "256MiB",
            ["demuxer-max-back-bytes"] = "128MiB",
            ["msg-level"] = "all=warn",
        };
        try
        {
            // TELA_MPV_LOG=v (ou debug): registo detalhado da libmpv no log.txt, para diagnósticos.
            var level = Environment.GetEnvironmentVariable("TELA_MPV_LOG") is { Length: > 0 } l ? l : "warn";
            if (level != "warn")
                options["msg-level"] = "all=" + level;
            _player = MpvPlayer.Create(options, a => Dispatcher.UIThread.Post(a), level);
        }
        catch (Exception ex)
        {
            Log.Write("A libmpv não arrancou: " + ex.Message);
            ShowMissingLibmpv(ex.Message);
            return;
        }
        _player.LogMessage += m =>
        {
            Log.Write("mpv " + m);
            // Formato sem descodificador nesta libmpv (ex.: HEVC no Fedora sem o RPM Fusion): diz-se
            // porquê, em vez de mostrar um vídeo preto com som.
            var match = MissingDecoder.Match(m);
            if (match.Success)
                Dispatcher.UIThread.Post(() => ShowOsd(Loc.F("NoDecoder", match.Groups[1].Value.ToUpperInvariant())
                    + (OperatingSystem.IsLinux() ? Environment.NewLine + Loc.T("NoDecoderLinux") : ""), seconds: 9));
        };
        foreach (var (name, format) in new (string, Native.Format)[]
        {
            ("pause", Native.Format.Flag), ("time-pos", Native.Format.Double), ("duration", Native.Format.Double),
            ("eof-reached", Native.Format.Flag), ("volume", Native.Format.Double), ("mute", Native.Format.Flag),
            ("speed", Native.Format.Double), ("track-list", Native.Format.None), ("chapter-list", Native.Format.None),
            ("media-title", Native.Format.String), ("dwidth", Native.Format.Int64), ("dheight", Native.Format.Int64),
            ("seekable", Native.Format.Flag), ("ab-loop-a", Native.Format.String), ("ab-loop-b", Native.Format.String),
            ("sid", Native.Format.String),
        })
            _player.Observe(name, format);
        _player.PropertyChanged += OnProperty;
        _player.FileLoaded += OnFileLoaded;
        _player.EndFile += OnEndFile;
        if (_settings.Normalize)
            _player.Set("af", NormalizeFilter);

        Video.Ready += OnVideoReady;
        Video.Unavailable += _ => StartSoftwareVideo();
        SoftVideo.Ready += OnVideoReady;
        Video.ForceSoftware = _settings.SoftwareVideo;
        Video.Player = _player;
    }

    /// <summary>Sem OpenGL (ou por escolha): a mesma imagem, desenhada por software.</summary>
    private void StartSoftwareVideo()
    {
        if (_player is null || SoftVideo.IsRunning)
            return;
        // A libmpv só aceita um contexto de desenho: o do OpenGL sai primeiro (tirar a vista da
        // árvore liberta-o com o OpenGL ainda vivo).
        var reload = Video.IsReady && _loaded;
        var position = _player.TimePos;
        Root.Children.Remove(Video);
        SoftVideo.IsVisible = true;
        void Begin()
        {
            // A meio de um vídeo, volta-se a abri-lo no mesmo ponto para a imagem passar para cá.
            if (reload && _queue.Current is not null)
                PlayCurrent(startAt: double.IsFinite(position) ? position : null);
        }
        if (SoftVideo.Start(_player))
            Begin();
        else
            // O contexto do OpenGL pode ainda não ter saído: tenta-se outra vez daqui a pouco.
            DispatcherTimer.RunOnce(() =>
            {
                if (_player is not null && SoftVideo.Start(_player))
                    Begin();
                else
                    ShowOsd(Loc.F("VideoFailed", "software"), seconds: 6);
            }, TimeSpan.FromMilliseconds(400));
    }

    private void OnVideoReady()
    {
        _videoReady = true;
        if (_pendingOpen is { } p)
        {
            _pendingOpen = null;
            StartQueue(p.Items, p.Start, p.Subs);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex MissingDecoder =
        new(@"Failed to initialize a decoder for codec '([^']+)'", System.Text.RegularExpressions.RegexOptions.Compiled);

    private const string NormalizeFilter = "lavfi=[dynaudnorm=f=250:g=31:p=0.6]";

    private string ScreenshotFolder()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ScreenshotFolder))
            return _settings.ScreenshotFolder;
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrEmpty(pictures))
            pictures = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(pictures, "Tela");
    }

    private void OnProperty(string name, object? value)
    {
        switch (name)
        {
            case "pause":
                _paused = value is true;
                UpdatePlayState();
                break;
            case "duration":
                _duration = value is double d && double.IsFinite(d) ? d : 0;
                Seek.Maximum = Math.Max(1, _duration);
                Seek.IsEnabled = _duration > 0 && _seekable;
                UpdateTime();
                break;
            case "seekable":
                _seekable = value is true;
                Seek.IsEnabled = _duration > 0 && _seekable;
                break;
            case "eof-reached":
                var eof = value is true;
                if (eof == _eof)
                    break;
                _eof = eof;
                if (eof && _loaded)
                    OnReachedEnd();
                else if (!eof)
                    HideUpNext();
                UpdatePlayState();
                break;
            case "volume" when value is double v:
                if (!VolumeSlider.IsDragging)
                    VolumeSlider.Value = v;
                _settings.Volume = Math.Round(v);
                UpdateVolumeGlyph();
                break;
            case "mute":
                _settings.Muted = value is true;
                UpdateVolumeGlyph();
                break;
            case "speed" when value is double sp:
                SpeedButton.Content = FormatSpeed(sp);
                break;
            case "track-list":
                if (_player is null)
                    break;
                _tracks = _player.ReadTracks();
                OnTracksChanged();
                break;
            case "chapter-list":
                if (_player is null)
                    break;
                _chapters = _player.ReadChapters();
                Seek.Marks = _chapters.Select(c => c.Time).ToList();
                break;
            case "media-title":
                _mediaTitle = value as string;
                UpdateTitle();
                break;
            case "dwidth" when value is long w:
                _dw = w;
                MaybeFitWindow();
                break;
            case "dheight" when value is long h:
                _dh = h;
                MaybeFitWindow();
                break;
            case "ab-loop-a" or "ab-loop-b":
                UpdateLoopMarks();
                break;
            case "sid":
                SubsGlyph.Opacity = value is null or "no" ? 0.55 : 1;
                break;
        }
    }

    private void OnTracksChanged()
    {
        var video = _tracks.FirstOrDefault(t => t.Type == "video" && t.Selected);
        _hasVideo = video is not null && !video.AlbumArt;
        AudioView.IsVisible = _loaded && video is null;
        SubsButton.IsEnabled = _loaded;
        AudioButton.IsEnabled = _tracks.Any(t => t.Type == "audio");
        UpdatePlayState();
    }

    // ── Abrir ─────────────────────────────────────────────────────────────────

    /// <summary>Abre o que chegou (ficheiros, pastas, legendas): um vídeo traz a pasta com ele.</summary>
    public void Open(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return;
        var (items, start, subs) = MediaFiles.Expand(paths);
        if (items.Count == 0)
        {
            // Só legendas: juntam-se ao vídeo que está a tocar.
            if (subs.Count > 0 && _loaded)
                foreach (var sub in subs)
                    AddSubtitle(sub);
            else
                ShowOsd(Loc.T("NothingToPlay"), seconds: 3);
            return;
        }
        if (Path.GetDirectoryName(items[start]) is { } folder)
            _settings.LastFolder = folder;
        StartQueue(items, start, subs);
    }

    private List<string> _subsForNext = [];

    private void StartQueue(List<string> items, int start, List<string> subs)
    {
        if (_player is null)
            return;
        if (!_videoReady)
        {
            _pendingOpen = (items, start, subs);
            HideHome();
            return;
        }
        LeaveCurrent();
        // Uma lista nova (outra pasta) começa sem as línguas escolhidas na anterior.
        _sessionAlang = _sessionSlang = null;
        _sessionSubsOff = false;
        _queue.Set(items, start);
        _subsForNext = subs;
        PlayCurrent();
    }

    /// <summary>Toca o item atual da lista, a partir de onde ficou (se se lembrar).</summary>
    private void PlayCurrent(bool fromStart = false, double? startAt = null)
    {
        if (_player is null || _queue.Current is not { } path)
            return;
        HideUpNext();
        _current = path;
        _loaded = false;
        _eof = false;
        _duration = 0;
        _tracks = [];
        _chapters = [];
        _chosenAid = _chosenSid = null;
        _mediaTitle = null;
        _dw = _dh = 0;
        _fitPending = true;
        ResetThumbs();
        Seek.Marks = [];
        Seek.SetRange(null, null);
        Seek.Value = 0;
        Seek.IsEnabled = false;

        var entry = _history.Find(path);
        _resumedFrom = startAt ?? (fromStart || !_settings.Resume ? null : History.ResumePoint(entry));

        // Opções do ficheiro seguinte: de onde começar, que faixas, que atrasos.
        _player.Set("start", _resumedFrom is { } r ? MpvPlayer.Seconds(r) : "none");
        _player.Set("aid", entry?.Aid ?? "auto");
        _player.Set("sid", entry?.Sid ?? (_sessionSubsOff ? "no" : "auto"));
        _player.Set("alang", _sessionAlang ?? _settings.AudioLanguages);
        _player.Set("slang", _sessionSlang ?? _settings.SubLanguages);
        _player.Set("sub-delay", entry?.SubDelay ?? 0);
        _player.Set("audio-delay", entry?.AudioDelay ?? 0);
        _player.Set("ab-loop-a", "no");
        _player.Set("ab-loop-b", "no");
        _player.Set("pause", false);
        _player.CommandAsync("loadfile", path, "replace");

        HideHome();
        UpdateTitle();
        UpdateQueueButtons();
        ShowChrome(temporary: true);
    }

    private void OnFileLoaded()
    {
        if (_player is null)
            return;
        _loaded = true;
        _player.Set("start", "none");
        foreach (var sub in _subsForNext)
            AddSubtitle(sub);
        _subsForNext = [];
        OnTracksChanged();
        if (_resumedFrom is { } r && r > 0 && !_silentReload)
            ShowOsd(Loc.F("Resumed", FormatTime(r)), seconds: 4);
        _silentReload = false;
        _mediaKeys?.SetActive(true);
        UpdatePlayState();
    }

    private void OnEndFile(Native.EndFileReason reason, int error)
    {
        // Uma lista de reprodução dentro do ficheiro (M3U, por exemplo): a Tela não a segue, que
        // pode apontar para qualquer lado. Os vídeos de uma pasta abrem-se pela pasta.
        if (reason == Native.EndFileReason.Redirect)
        {
            _player?.Command("stop");
            _player?.Command("playlist-clear");
            _loaded = false;
            ShowOsd(Loc.T("NoPlaylists"), seconds: 5);
            ShowHome();
            return;
        }
        if (reason != Native.EndFileReason.Error)
            return;
        var name = _current is null ? "" : Path.GetFileName(_current);
        Log.Write($"Não abriu: {_current} ({Native.ErrorText(error)})");
        _loaded = false;
        ShowOsd(Loc.F("CantPlay", name), seconds: 5);
        // Um ficheiro estragado no meio de uma série não pára a série.
        if (_queue.HasNext)
        {
            DispatcherTimer.RunOnce(() =>
            {
                if (!_loaded && _queue.MoveNext())
                    PlayCurrent();
            }, TimeSpan.FromSeconds(1.2));
        }
        else
            ShowHome();
    }

    private void AddSubtitle(string file)
    {
        if (_player is null)
            return;
        _player.CommandAsync("sub-add", file, "select");
        ShowOsd(Loc.F("SubtitleAdded", Path.GetFileName(file)), seconds: 2.5);
    }

    // ── Lista ─────────────────────────────────────────────────────────────────

    private void Next()
    {
        if (!_queue.HasNext)
        {
            ShowOsd(Loc.T("LastInList"), seconds: 1.5);
            return;
        }
        LeaveCurrent();
        _queue.MoveNext();
        PlayCurrent();
    }

    /// <summary>No princípio do vídeo vai ao anterior; a meio, volta ao princípio deste.</summary>
    private void Previous()
    {
        if (_player is null)
            return;
        var pos = _player.TimePos;
        if (_queue.HasPrevious && (!double.IsFinite(pos) || pos < 5))
        {
            LeaveCurrent();
            _queue.MovePrevious();
            PlayCurrent();
        }
        else
            SeekTo(0);
    }

    private void PlayIndex(int index)
    {
        if (index == _queue.Index || index < 0 || index >= _queue.Count)
            return;
        LeaveCurrent();
        _queue.MoveTo(index);
        PlayCurrent();
    }

    private void UpdateQueueButtons()
    {
        PrevButton.IsEnabled = _queue.Count > 0;
        NextButton.IsEnabled = _queue.HasNext;
        QueueText.Text = _queue.Count > 1 ? $"{_queue.Index + 1}/{_queue.Count}" : "";
    }

    /// <summary>Fecha o vídeo e volta ao ecrã inicial.</summary>
    private void CloseVideo()
    {
        LeaveCurrent();
        _player?.Command("stop");
        _queue.Clear();
        _current = null;
        _loaded = false;
        _mediaKeys?.SetActive(false);
        ResetThumbs();
        UpdatePlayState();
        ShowHome();
    }

    // ── Memória (histórico) ───────────────────────────────────────────────────

    /// <summary>Guarda onde ficou o vídeo atual (e, a sair dele, a imagem do "continuar a ver").</summary>
    private void SaveProgress(bool thumb)
    {
        if (_player is null || _current is null || !_loaded)
            return;
        var pos = _player.TimePos;
        if (!double.IsFinite(pos))
            return;
        var entry = _history.Update(_current, DisplayTitle(), pos, _duration);
        if (_chosenAid is not null)
            entry.Aid = _chosenAid;
        if (_chosenSid is not null)
            entry.Sid = _chosenSid;
        entry.SubDelay = _player.GetDouble("sub-delay") ?? 0;
        entry.AudioDelay = _player.GetDouble("audio-delay") ?? 0;
        if (thumb && _hasVideo && History.ResumePoint(entry) is not null)
            CaptureThumb(entry.Path, pos);
        _history.Save();
    }

    private void LeaveCurrent() => SaveProgress(thumb: true);

    private readonly List<Task> _thumbJobs = [];

    /// <summary>A imagem do "continuar a ver", tirada em segundo plano por uma segunda libmpv.</summary>
    private void CaptureThumb(string path, double position)
    {
        var name = History.ThumbName(path);
        var folder = _history.ThumbFolder;
        var job = Task.Run(() =>
        {
            var frame = Thumbnailer.Capture(path, position, 320, TimeSpan.FromSeconds(6));
            if (frame is null)
                return;
            Directory.CreateDirectory(folder);
            using (var bitmap = ToBitmap(frame))
                bitmap.Save(Path.Combine(folder, name));
            Dispatcher.UIThread.Post(() =>
            {
                if (_history.Find(path) is not { } e)
                    return;
                e.Thumb = name;
                _history.Save();
                if (Home.IsVisible)
                    RefreshHome();
            });
        });
        lock (_thumbJobs)
        {
            _thumbJobs.RemoveAll(t => t.IsCompleted);
            _thumbJobs.Add(job);
        }
    }

    internal static WriteableBitmap ToBitmap(ThumbFrame frame)
    {
        var bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var fb = bitmap.Lock();
        unsafe
        {
            fixed (byte* src = frame.Pixels)
            {
                for (var y = 0; y < frame.Height; y++)
                {
                    var from = src + y * frame.Stride;
                    var to = (byte*)fb.Address + y * fb.RowBytes;
                    Buffer.MemoryCopy(from, to, fb.RowBytes, frame.Width * 4);
                    // O quarto byte vem por preencher: fica opaco.
                    for (var x = 3; x < frame.Width * 4; x += 4)
                        to[x] = 255;
                }
            }
        }
        return bitmap;
    }

    // ── Tempo ─────────────────────────────────────────────────────────────────

    private void Tick()
    {
        UpdateTime();
        UpdateUpNext();
    }

    private void UpdateTime()
    {
        if (_player is null)
            return;
        var pos = _player.TimePos;
        if (!double.IsFinite(pos))
            pos = _loaded ? Seek.Value : 0;
        if (!Seek.IsDragging)
            Seek.Value = Math.Clamp(pos, 0, Seek.Maximum);
        var shown = Seek.IsDragging ? Seek.Value : pos;
        TimeText.Text = _settings.TimeRemaining && _duration > 0
            ? $"−{FormatTime(Math.Max(0, _duration - shown))} / {FormatTime(_duration)}"
            : $"{FormatTime(shown)} / {FormatTime(_duration)}";
        if (InfoPanel.IsVisible)
            UpdateInfo();
    }

    internal static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            seconds = 0;
        var t = TimeSpan.FromSeconds(Math.Floor(seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    internal static string FormatSpeed(double speed) =>
        speed.ToString(Math.Abs(speed % 1) < 1e-9 ? "0" : "0.##", CultureInfo.InvariantCulture) + "×";

    private string DisplayTitle()
    {
        if (_current is null)
            return "";
        var file = Path.GetFileName(_current);
        // A libmpv dá o nome do ficheiro quando não há título; aí mostra-se o nome arrumado.
        return string.IsNullOrWhiteSpace(_mediaTitle) || _mediaTitle == file
            ? MediaFiles.DisplayName(_current)
            : _mediaTitle!;
    }

    private void UpdateTitle()
    {
        var title = _current is null ? "TELA" : DisplayTitle();
        TitleText.Text = title;
        AudioTitle.Text = title;
        Title = _current is null ? "Tela" : $"{title} · Tela";
    }

    // ── Comandos ──────────────────────────────────────────────────────────────

    private void PlayPause()
    {
        if (_player is null || !_loaded)
            return;
        // No fim, tocar recomeça do princípio.
        if (_eof)
        {
            SeekTo(0);
            _player.Set("pause", false);
            return;
        }
        _player.Set("pause", !_paused);
    }

    private void SeekTo(double seconds, bool exact = true)
    {
        if (_player is null || !_loaded)
            return;
        _player.CommandAsync("seek", MpvPlayer.Seconds(Math.Max(0, seconds)), exact ? "absolute+exact" : "absolute+keyframes");
    }

    private void SeekBy(double seconds)
    {
        if (_player is null || !_loaded || !_seekable)
            return;
        _player.CommandAsync("seek", MpvPlayer.Seconds(seconds), "relative+exact");
        ShowChrome(temporary: true);
    }

    private void SeekPercent(double percent)
    {
        if (_duration <= 0)
            return;
        SeekTo(_duration * percent / 100);
        ShowChrome(temporary: true);
    }

    private void SeekChapter(int direction)
    {
        if (_player is null || !_loaded || _chapters.Count == 0)
            return;
        _player.CommandAsync("add", "chapter", direction.ToString(CultureInfo.InvariantCulture));
        ShowChrome(temporary: true);
    }

    private void SetVolume(double volume)
    {
        if (_player is null)
            return;
        volume = Math.Clamp(Math.Round(volume), 0, 150);
        _player.Set("volume", volume);
        if (volume > 0 && _settings.Muted)
            _player.Set("mute", false);
        ShowOsd(Loc.F("VolumeOsd", volume), bar: volume / 150, seconds: 1.2);
    }

    private void ChangeVolume(double delta) => SetVolume((_player?.GetDouble("volume") ?? _settings.Volume) + delta);

    private void ToggleMute()
    {
        if (_player is null)
            return;
        var mute = !_settings.Muted;
        _player.Set("mute", mute);
        ShowOsd(mute ? Loc.T("Muted") : Loc.F("VolumeOsd", Math.Round(_settings.Volume)), seconds: 1.2);
    }

    private void UpdateVolumeGlyph()
    {
        VolumeGlyph.Kind = _settings.Muted || _settings.Volume <= 0 ? GlyphKind.Mute
            : _settings.Volume < 50 ? GlyphKind.VolumeLow : GlyphKind.Volume;
    }

    private void SetSpeed(double speed)
    {
        if (_player is null)
            return;
        speed = Math.Clamp(Math.Round(speed, 2), 0.25, 4);
        _player.Set("speed", speed);
        ShowOsd(Loc.F("SpeedOsd", FormatSpeed(speed)), seconds: 1.2);
    }

    private static readonly double[] SpeedSteps = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4];

    private void ChangeSpeed(int direction)
    {
        var current = _player?.GetDouble("speed") ?? 1;
        var next = direction > 0
            ? SpeedSteps.FirstOrDefault(s => s > current + 0.001, 4)
            : SpeedSteps.LastOrDefault(s => s < current - 0.001, 0.25);
        SetSpeed(next);
    }

    private void FrameStep(bool back)
    {
        if (_player is null || !_loaded || !_hasVideo)
            return;
        _player.CommandAsync(back ? "frame-back-step" : "frame-step");
    }

    private void Screenshot(bool withSubtitles)
    {
        if (_player is null || !_loaded || !_hasVideo)
            return;
        var folder = ScreenshotFolder();
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            ShowOsd(ex.Message, seconds: 3);
            return;
        }
        var name = $"{Path.GetFileNameWithoutExtension(_current)} {FormatTime(_player.TimePos).Replace(':', '-')}";
        name = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        var file = Path.Combine(folder, name + ".jpg");
        for (var i = 2; File.Exists(file); i++)
            file = Path.Combine(folder, $"{name} ({i}).jpg");
        if (_player.Command("screenshot-to-file", file, withSubtitles ? "subtitles" : "video") >= 0)
            ShowOsd(Loc.F("ScreenshotSaved", Path.GetFileName(file)), seconds: 2.5);
    }

    /// <summary>A → B → limpar, como no mpv: repetir um troço.</summary>
    private void CycleAbLoop()
    {
        if (_player is null || !_loaded)
            return;
        var a = _player.GetString("ab-loop-a");
        var b = _player.GetString("ab-loop-b");
        var pos = _player.TimePos;
        if (a is null or "no")
        {
            _player.Set("ab-loop-a", pos);
            ShowOsd(Loc.F("LoopA", FormatTime(pos)), seconds: 2);
        }
        else if (b is null or "no")
        {
            _player.Set("ab-loop-b", pos);
            ShowOsd(Loc.T("LoopOn"), seconds: 2);
        }
        else
        {
            _player.Set("ab-loop-a", "no");
            _player.Set("ab-loop-b", "no");
            ShowOsd(Loc.T("LoopOff"), seconds: 1.5);
        }
    }

    private void UpdateLoopMarks()
    {
        static double? Parse(string? s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        Seek.SetRange(Parse(_player?.GetString("ab-loop-a")), Parse(_player?.GetString("ab-loop-b")));
    }

    private bool LoopFileOn => _player?.GetString("loop-file") is "inf" or "yes";

    private void ToggleLoopFile()
    {
        if (_player is null)
            return;
        var on = LoopFileOn;
        _player.Set("loop-file", on ? "no" : "inf");
        ShowOsd(Loc.T(on ? "RepeatOff" : "RepeatOn"), seconds: 1.5);
    }

    private void ChangeSubDelay(double delta)
    {
        if (_player is null || !_loaded)
            return;
        var d = delta == 0 ? 0 : Math.Round((_player.GetDouble("sub-delay") ?? 0) + delta, 2);
        _player.Set("sub-delay", d);
        ShowOsd(Loc.F("SubDelayOsd", FormatDelay(d)), seconds: 1.5);
    }

    private void ChangeAudioDelay(double delta)
    {
        if (_player is null || !_loaded)
            return;
        var d = delta == 0 ? 0 : Math.Round((_player.GetDouble("audio-delay") ?? 0) + delta, 2);
        _player.Set("audio-delay", d);
        ShowOsd(Loc.F("AudioDelayOsd", FormatDelay(d)), seconds: 1.5);
    }

    internal static string FormatDelay(double seconds) =>
        (seconds > 0 ? "+" : seconds < 0 ? "−" : "") + Math.Abs(seconds * 1000).ToString("0", CultureInfo.InvariantCulture) + " ms";

    private void SetSubScale(double scale)
    {
        if (_player is null)
            return;
        _settings.SubScale = Math.Clamp(Math.Round(scale, 2), 0.5, 3);
        _player.Set("sub-scale", _settings.SubScale);
        ShowOsd(Loc.F("SubSizeOsd", Math.Round(_settings.SubScale * 100)), seconds: 1.5);
    }

    private void UpdatePlayState()
    {
        var playing = _loaded && !_paused && !_eof;
        PlayGlyph.Kind = playing ? GlyphKind.Pause : GlyphKind.Play;
        ToolTip.SetTip(PlayButton, Loc.T(playing ? "PauseTip" : "PlayTip"));
        _screenSaver.Set(playing && _hasVideo);
        if (!playing)
            ShowChrome(temporary: false);
        else
            ScheduleHide();
    }

    private void OnLanguageChanged()
    {
        UpdatePlayState();
        UpdateTitle();
        if (Home.IsVisible)
            RefreshHome();
    }

    private void ApplyThemeClasses()
    {
        var t = ThemeManager.Current;
        Classes.Set("sink", t.PressSinks);
        Classes.Set("lift", t.HoverLifts);
        Classes.Set("motion", t.Motion);
        Classes.Set("bevel", t.Slider == SliderLook.Bevel);
        ApplyWindowCorners();
    }
}
