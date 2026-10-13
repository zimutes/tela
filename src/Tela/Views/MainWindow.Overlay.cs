using System.Globalization;
using System.Text;
using Tela.Controls;
using Tela.Mpv;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Tela.Views;

public partial class MainWindow
{
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(2.6) };
    private readonly DispatcherTimer _osdTimer = new();
    private readonly DispatcherTimer _clickTimer = new() { Interval = TimeSpan.FromMilliseconds(240) };
    private bool _chromeVisible = true;
    private int _menusOpen;
    private Point? _pressPoint;
    private PointerPressedEventArgs? _pressArgs;
    private bool _pressDragged;

    private void SetupControls()
    {
        PlayButton.Click += (_, _) => PlayPause();
        PrevButton.Click += (_, _) => Previous();
        NextButton.Click += (_, _) => Next();
        MuteButton.Click += (_, _) => ToggleMute();
        FullscreenButton.Click += (_, _) => ToggleFullscreen();
        PipButton.Click += (_, _) => TogglePip();
        SpeedButton.Click += (_, _) => Track(SpeedMenu()).ShowAt(SpeedButton);
        SubsButton.Click += (_, _) => Track(SubtitleMenu()).ShowAt(SubsButton);
        AudioButton.Click += (_, _) => Track(AudioMenu()).ShowAt(AudioButton);
        TimeText.PointerPressed += (_, e) =>
        {
            _settings.TimeRemaining = !_settings.TimeRemaining;
            UpdateTime();
            e.Handled = true;
        };
        VolumeSlider.Value = _settings.Volume;
        VolumeSlider.UserChanged += (_, v) => _player?.Set("volume", Math.Round(v));
        UpdateVolumeGlyph();
        SubsButton.IsEnabled = AudioButton.IsEnabled = false;
        UpdateQueueButtons();

        // Barra de posição: a arrastar salta para imagens-chave (rápido); ao largar, para o ponto exato.
        var lastScrub = 0L;
        Seek.UserChanged += (_, v) =>
        {
            if (!Seek.IsDragging)
                return;
            var now = Environment.TickCount64;
            if (now - lastScrub > 70)
            {
                lastScrub = now;
                SeekTo(v, exact: false);
            }
            ShowPreview(v);
        };
        Seek.Committed += (_, v) => SeekTo(v);
        Seek.Hover += (_, v) =>
        {
            if (v is { } value && Seek.IsEnabled)
                ShowPreview(value);
            else if (!Seek.IsDragging)
                Preview.IsVisible = false;
        };

        // Gestos em cima do vídeo.
        Surface.PointerPressed += OnSurfacePressed;
        Surface.PointerMoved += OnSurfaceMoved;
        Surface.PointerReleased += OnSurfaceReleased;
        Surface.PointerWheelChanged += (_, e) =>
        {
            if (!_loaded)
                return;
            if (Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y))
                SeekBy(Math.Sign(e.Delta.X) * 5);
            else if (e.Delta.Y != 0)
                ChangeVolume(Math.Sign(e.Delta.Y) * 5);
            e.Handled = true;
        };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            if (_settings.ClickToPause && _loaded)
            {
                // O ícone mostra o que vai acontecer (o estado só muda quando a libmpv responde).
                ShowFlash(_paused ? GlyphKind.Play : GlyphKind.Pause);
                PlayPause();
            }
        };

        // Qualquer movimento mostra os controlos; parados, somem (se o vídeo estiver a tocar).
        AddHandler(PointerMovedEvent, (_, _) => ShowChrome(temporary: true), RoutingStrategies.Tunnel, handledEventsToo: true);
        PointerExited += (_, _) => ScheduleHide(soon: true);
        _hideTimer.Tick += (_, _) => HideChrome();
        _osdTimer.Tick += (_, _) =>
        {
            _osdTimer.Stop();
            Osd.IsVisible = false;
        };

        UpNextPlay.Click += (_, _) => UpNextPrimary();
        UpNextCancel.Click += (_, _) => UpNextSecondary();
        Overlay.PointerPressed += (_, e) =>
        {
            if (e.Source == Overlay)
                HideOverlay();
        };
    }

    private MenuFlyout Track(MenuFlyout menu)
    {
        menu.Opened += (_, _) => _menusOpen++;
        menu.Closed += (_, _) =>
        {
            _menusOpen = Math.Max(0, _menusOpen - 1);
            ScheduleHide();
        };
        return menu;
    }

    // ── Gestos ────────────────────────────────────────────────────────────────

    private void OnSurfacePressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            Track(MainMenu(context: true)).ShowAt(Surface, showAtPointer: true);
            e.Handled = true;
            return;
        }
        if (point.Properties.IsXButton1Pressed)
        {
            SeekBy(-10);
            e.Handled = true;
            return;
        }
        if (point.Properties.IsXButton2Pressed)
        {
            SeekBy(10);
            e.Handled = true;
            return;
        }
        if (point.Properties.IsMiddleButtonPressed)
        {
            ToggleMute();
            e.Handled = true;
            return;
        }
        if (!point.Properties.IsLeftButtonPressed)
            return;
        if (e.ClickCount >= 2)
        {
            // Duplo clique: ecrã inteiro (o clique de pausa ainda não tinha acontecido).
            _clickTimer.Stop();
            _pressPoint = null;
            ToggleFullscreen();
            e.Handled = true;
            return;
        }
        _pressPoint = point.Position;
        _pressArgs = e;
        _pressDragged = false;
        e.Handled = true;
    }

    private void OnSurfaceMoved(object? sender, PointerEventArgs e)
    {
        if (_pressPoint is not { } start || _pressDragged)
            return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;
        var d = point.Position - start;
        // Arrastar o vídeo move a janela (como numa barra de título).
        if (Math.Abs(d.X) + Math.Abs(d.Y) > 6 && WindowState == WindowState.Normal && _pressArgs is { } press)
        {
            _pressDragged = true;
            _pressPoint = null;
            _pressArgs = null;
            BeginMoveDrag(press);
        }
    }

    private void OnSurfaceReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressPoint is null || _pressDragged || e.InitialPressMouseButton != MouseButton.Left)
        {
            _pressPoint = null;
            return;
        }
        _pressPoint = null;
        if (!_loaded)
            return;
        // Espera um instante para ver se é duplo clique.
        _clickTimer.Stop();
        _clickTimer.Start();
    }

    private void ShowFlash(GlyphKind kind)
    {
        FlashGlyph.Kind = kind;
        FlashBadge.Opacity = 1;
        FlashBadge.IsVisible = true;
        DispatcherTimer.RunOnce(() => FlashBadge.Opacity = 0, TimeSpan.FromMilliseconds(220));
        DispatcherTimer.RunOnce(() =>
        {
            if (FlashBadge.Opacity == 0)
                FlashBadge.IsVisible = false;
        }, TimeSpan.FromMilliseconds(650));
    }

    // ── Controlos que somem ───────────────────────────────────────────────────

    /// <summary>Mostra os controlos. Temporário: voltam a sumir se o vídeo continuar a tocar.</summary>
    private void ShowChrome(bool temporary)
    {
        if (!_chromeVisible)
        {
            _chromeVisible = true;
            Chrome.Opacity = 1;
            Chrome.IsHitTestVisible = true;
        }
        UpdateCursor();
        UpdateSubtitlePosition();
        if (temporary)
            ScheduleHide();
        else
            _hideTimer.Stop();
    }

    private void ScheduleHide(bool soon = false)
    {
        _hideTimer.Stop();
        _hideTimer.Interval = TimeSpan.FromSeconds(soon ? 0.8 : 2.6);
        _hideTimer.Start();
    }

    private bool CanHideChrome()
    {
        if (Home.IsVisible || !_loaded || _paused || _eof || _menusOpen > 0 || Overlay.IsVisible || Seek.IsDragging || UpNext.IsVisible)
            return false;
        // Com o rato em cima das barras, ficam.
        return !TopBar.IsPointerOver && !BottomBar.IsPointerOver;
    }

    private void HideChrome()
    {
        _hideTimer.Stop();
        if (!CanHideChrome())
            return;
        _chromeVisible = false;
        Chrome.Opacity = 0;
        Chrome.IsHitTestVisible = false;
        Preview.IsVisible = false;
        UpdateCursor();
        UpdateSubtitlePosition();
    }

    private double _subPos = 100;

    /// <summary>Com a barra de comandos à vista, as legendas sobem para cima dela (não ficam por trás).</summary>
    private void UpdateSubtitlePosition()
    {
        if (_player is null)
            return;
        var pos = 100.0;
        if (_chromeVisible && BottomBar.IsVisible && Root.Bounds.Height > 0)
        {
            var top = BottomBar.TranslatePoint(new Point(0, 0), Root)?.Y ?? Root.Bounds.Height;
            pos = Math.Clamp(Math.Floor((top - 6) / Root.Bounds.Height * 100), 50, 100);
        }
        if (Math.Abs(pos - _subPos) < 0.5)
            return;
        _subPos = pos;
        _player.Set("sub-pos", (long)pos);
    }

    private void UpdateCursor()
    {
        // Sem controlos, o cursor também desaparece de cima do vídeo.
        Root.Cursor = !_chromeVisible && _loaded && !Home.IsVisible ? new Cursor(StandardCursorType.None) : Cursor.Default;
    }

    // ── Mensagens ─────────────────────────────────────────────────────────────

    private void ShowOsd(string text, double? bar = null, double seconds = 1.5)
    {
        OsdText.Text = text;
        OsdBar.IsVisible = bar is not null;
        if (bar is { } b)
            OsdFill.Width = OsdBar.Width * Math.Clamp(b, 0, 1);
        // Com o painel de informação aberto, a mensagem vai para baixo dele.
        Osd.Margin = InfoPanel.IsVisible ? new Thickness(18, 56 + InfoPanel.Bounds.Height + 10, 18, 0) : new Thickness(18, 56, 18, 0);
        Osd.IsVisible = true;
        _osdTimer.Stop();
        _osdTimer.Interval = TimeSpan.FromSeconds(seconds);
        _osdTimer.Start();
    }

    // ── Pré-visualização na barra de posição ──────────────────────────────────

    private Thumbnailer? _thumbs;
    private readonly Dictionary<int, Bitmap> _thumbCache = [];
    private int _wantedBucket = -1;

    private void ResetThumbs()
    {
        _thumbs?.Dispose();
        _thumbs = null;
        foreach (var b in _thumbCache.Values)
            b.Dispose();
        _thumbCache.Clear();
        _wantedBucket = -1;
        Preview.IsVisible = false;
    }

    private double ThumbStep => Math.Max(2, _duration / 400);

    private void ShowPreview(double value)
    {
        if (_duration <= 0 || !_chromeVisible)
            return;
        PreviewTime.Text = FormatTime(value);
        var chapter = _chapters.LastOrDefault(c => c.Time <= value + 0.01);
        PreviewChapter.Text = chapter?.Title ?? "";
        PreviewChapter.IsVisible = !string.IsNullOrEmpty(chapter?.Title);

        var canThumb = _hasVideo && _current is not null && _thumbs?.Failed != true;
        PreviewFrame.IsVisible = canThumb;
        if (canThumb)
        {
            if (_dw > 0 && _dh > 0)
                PreviewFrame.Height = Math.Round(PreviewFrame.Width * _dh / _dw);
            var bucket = (int)Math.Round(value / ThumbStep);
            if (_thumbCache.TryGetValue(bucket, out var cached))
                PreviewImage.Source = cached;
            else
            {
                // A mais próxima que já houver, enquanto a certa não chega.
                if (_thumbCache.Count > 0)
                    PreviewImage.Source = _thumbCache.MinBy(kv => Math.Abs(kv.Key - bucket)).Value;
                RequestThumb(bucket);
            }
        }

        // Por cima do ponto da barra, sem sair da janela.
        var origin = Seek.TranslatePoint(new Point(Seek.XFor(value), 0), Root) ?? default;
        Preview.Measure(Size.Infinity);
        var width = Preview.DesiredSize.Width;
        var x = Math.Clamp(origin.X - width / 2, 8, Math.Max(8, Root.Bounds.Width - width - 8));
        var bottom = Root.Bounds.Height - origin.Y + 6;
        Preview.Margin = new Thickness(x, 0, 0, bottom);
        Preview.IsVisible = true;
    }

    private void RequestThumb(int bucket)
    {
        if (_current is null)
            return;
        _wantedBucket = bucket;
        _thumbs ??= new Thumbnailer(_current, 256);
        var thumbs = _thumbs;
        thumbs.Request(bucket * ThumbStep, frame =>
        {
            var bitmap = ToBitmap(frame);
            Dispatcher.UIThread.Post(() =>
            {
                if (_thumbs != thumbs)
                {
                    bitmap.Dispose();
                    return;
                }
                if (_thumbCache.TryAdd(bucket, bitmap))
                {
                    if (_wantedBucket == bucket && Preview.IsVisible)
                        PreviewImage.Source = bitmap;
                }
                else
                    bitmap.Dispose();
            });
        });
    }

    // ── A seguir / fim ────────────────────────────────────────────────────────

    private DateTime? _upNextAt;
    private bool _upNextIsEnd;

    private void OnReachedEnd()
    {
        SaveProgress(thumb: false);
        if (LoopFileOn)
            return;
        if (_queue.HasNext && _settings.AutoplayNext)
        {
            _upNextIsEnd = false;
            _upNextAt = DateTime.UtcNow.AddSeconds(6);
            UpNextTitle.Text = Library.MediaFiles.DisplayName(_queue.NextItem!);
            UpNextPlay.Content = Btn("PlayNow");
            UpNextCancel.Content = Btn("Cancel");
        }
        else
        {
            _upNextIsEnd = true;
            _upNextAt = null;
            UpNextLabel.Text = Loc.T("Finished").ToUpper(Loc.Culture);
            UpNextTitle.Text = DisplayTitle();
            UpNextPlay.Content = Btn("WatchAgain");
            UpNextCancel.Content = Btn(_queue.HasNext ? "NextVideo" : "CloseVideo");
        }
        UpdateUpNext();
        UpNext.IsVisible = true;
        ShowChrome(temporary: false);
    }

    private void UpdateUpNext()
    {
        if (!UpNext.IsVisible || _upNextAt is not { } at)
            return;
        var left = (int)Math.Ceiling((at - DateTime.UtcNow).TotalSeconds);
        if (left <= 0)
        {
            _upNextAt = null;
            UpNext.IsVisible = false;
            Next();
            return;
        }
        UpNextLabel.Text = Loc.F("UpNextIn", left).ToUpper(Loc.Culture);
    }

    private void UpNextPrimary()
    {
        if (_upNextIsEnd)
        {
            HideUpNext();
            SeekTo(0);
            _player?.Set("pause", false);
        }
        else
        {
            HideUpNext();
            Next();
        }
    }

    private void UpNextSecondary()
    {
        if (!_upNextIsEnd)
            CancelUpNext();
        else if (_queue.HasNext)
        {
            HideUpNext();
            Next();
        }
        else
            CloseVideo();
    }

    private void CancelUpNext()
    {
        if (_upNextIsEnd)
        {
            HideUpNext();
            return;
        }
        // Cancelar a contagem deixa o cartão como "fim", para se poder ainda passar ao seguinte.
        _upNextIsEnd = true;
        _upNextAt = null;
        UpNextLabel.Text = Loc.T("Finished").ToUpper(Loc.Culture);
        UpNextTitle.Text = DisplayTitle();
        UpNextPlay.Content = Btn("WatchAgain");
        UpNextCancel.Content = Btn("NextVideo");
    }

    /// <summary>Texto de botão: em maiúsculas nos temas que as pedem.</summary>
    private static string Btn(string key) => Loc.Upper ? Loc.T(key).ToUpper(Loc.Culture) : Loc.T(key);

    private void HideUpNext()
    {
        _upNextAt = null;
        UpNext.IsVisible = false;
    }

    // ── Informação técnica ────────────────────────────────────────────────────

    private void ToggleInfo()
    {
        InfoPanel.IsVisible = !InfoPanel.IsVisible;
        if (InfoPanel.IsVisible)
            UpdateInfo();
    }

    private void UpdateInfo()
    {
        if (_player is null || _current is null)
        {
            InfoText.Text = Loc.T("NoFile");
            return;
        }
        var p = _player;
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine(Path.GetFileName(_current));
        if (_hasVideo)
        {
            var codec = p.GetString("video-format")?.ToUpperInvariant() ?? "?";
            var w = p.GetLong("video-params/w") ?? 0;
            var h = p.GetLong("video-params/h") ?? 0;
            var fps = p.GetDouble("container-fps") ?? p.GetDouble("estimated-vf-fps") ?? 0;
            var hw = p.GetString("hwdec-current");
            var hdr = p.GetString("video-params/gamma");
            sb.Append(inv, $"{Loc.T("InfoVideo")}  {codec} {w}×{h} · {fps:0.###} fps");
            if (hdr is "pq" or "hlg")
                sb.Append(" · HDR");
            sb.AppendLine(string.IsNullOrEmpty(hw) || hw == "no" ? $" · {Loc.T("InfoSoftware")}" : $" · {hw}");
        }
        var audio = _tracks.FirstOrDefault(t => t.Type == "audio" && t.Selected);
        if (audio is not null)
        {
            var rate = p.GetLong("audio-params/samplerate") ?? 0;
            var ch = p.GetLong("audio-params/channel-count") ?? audio.Channels;
            sb.AppendLine(inv, $"{Loc.T("InfoAudio")}  {audio.Codec?.ToUpperInvariant()} · {ChannelsText((int)ch)} · {rate / 1000.0:0.#} kHz · {TrackLabel(audio, includeTech: false)}");
        }
        var sub = _tracks.FirstOrDefault(t => t.Type == "sub" && t.Selected);
        sb.AppendLine($"{Loc.T("InfoSubs")}  {(sub is null ? Loc.T("Off") : TrackLabel(sub, includeTech: true))}");
        var vb = p.GetDouble("video-bitrate") ?? 0;
        var ab = p.GetDouble("audio-bitrate") ?? 0;
        if (vb > 0 || ab > 0)
            sb.AppendLine(inv, $"{Loc.T("InfoBitrate")}  {vb / 1e6:0.0} Mb/s + {ab / 1e3:0} kb/s");
        var drops = p.GetLong("frame-drop-count") ?? 0;
        var cache = p.GetDouble("demuxer-cache-duration") ?? 0;
        sb.Append(inv, $"{Loc.T("InfoDropped")}  {drops} · {Loc.T("InfoCache")}  {cache:0} s");
        InfoText.Text = sb.ToString();
    }

    // ── Janelas por cima (atalhos, acerca) ────────────────────────────────────

    private void ShowOverlay(Control content)
    {
        OverlayContent.Content = content;
        Overlay.IsVisible = true;
        ShowChrome(temporary: false);
    }

    private void HideOverlay()
    {
        Overlay.IsVisible = false;
        OverlayContent.Content = null;
        ScheduleHide();
    }

    private Button OverlayButton(string key, Action click)
    {
        var button = new Button { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        button.Bind(ContentControl.ContentProperty, this.GetResourceObservable("LU." + key));
        button.Click += (_, _) => click();
        return button;
    }

    private TextBlock Heading(string text)
    {
        var t = new TextBlock { Text = text, FontSize = 18, Margin = new Thickness(0, 0, 0, 4) };
        t.Bind(TextBlock.FontFamilyProperty, this.GetResourceObservable("FontDisplay"));
        t.Bind(TextBlock.FontWeightProperty, this.GetResourceObservable("TitleWeight"));
        return t;
    }

    private void ShowShortcuts()
    {
        (string Keys, string Key)[] rows =
        [
            ("Space  ·  K  ·  " + Loc.T("K.Click"), "K.Play"),
            ("←  →  ·  J  L", "K.Seek"),
            ("Shift+←  →  ·  ,  .", "K.Fine"),
            ("0 – 9  ·  Home", "K.Jump"),
            ("PgUp  PgDn", "K.Chapters"),
            ("↑  ↓  ·  M  ·  " + Loc.T("K.Wheel"), "K.Volume"),
            ("F  ·  " + Loc.T("K.DoubleClick") + "  ·  Esc", "K.Fullscreen"),
            ("I  ·  T", "K.Pip"),
            ("N  ·  P", "K.NextPrev"),
            ("V  ·  B", "K.Tracks"),
            ("Z  X  ·  Shift+Z  X", "K.Delay"),
            ("G  H", "K.SubSize"),
            ("[  ]  ·  Backspace", "K.Speed"),
            ("R  ·  Shift+R", "K.Loop"),
            ("A  ·  C  ·  D", "K.Picture"),
            ("S  ·  Shift+S", "K.Screenshot"),
            ("Shift+I", "K.Info"),
            ("Ctrl+O  ·  Ctrl+Shift+O  ·  Ctrl+V", "K.Open"),
            ("Ctrl+W  ·  Ctrl+Q", "K.Close"),
        ];
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        for (var i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var keys = new TextBlock { Text = rows[i].Keys, Margin = new Thickness(0, 3, 20, 3), FontSize = 12 };
            keys.Bind(TextBlock.FontFamilyProperty, this.GetResourceObservable("FontMono"));
            keys.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Accent"));
            var text = new TextBlock { Text = Loc.T(rows[i].Key), Margin = new Thickness(0, 3), FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(keys, i);
            Grid.SetRow(text, i);
            Grid.SetColumn(text, 1);
            grid.Children.Add(keys);
            grid.Children.Add(text);
        }
        ShowOverlay(new StackPanel { Spacing = 8, Children = { Heading(Loc.T("Shortcuts")), grid, OverlayButton("Close", HideOverlay) } });
    }

    private void ShowAbout()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        var name = new TextBlock { Text = "TELA", FontSize = 28, VerticalAlignment = VerticalAlignment.Center };
        name.Bind(TextBlock.FontFamilyProperty, this.GetResourceObservable("FontDisplay"));
        name.Bind(TextBlock.FontWeightProperty, this.GetResourceObservable("TitleWeight"));
        name.Bind(TextBlock.LetterSpacingProperty, this.GetResourceObservable("TitleSpacing"));
        name.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Accent"));
        var tagline = new TextBlock { Text = Loc.T("Tagline"), FontSize = 15 };
        tagline.Bind(TextBlock.FontFamilyProperty, this.GetResourceObservable("FontDisplay"));
        var logo = new Glyph { Kind = GlyphKind.Logo, Width = 32, Height = 32 };
        logo.Bind(Glyph.ForegroundProperty, this.GetResourceObservable("Accent"));
        var mpv = _player?.GetString("mpv-version") ?? "libmpv —";
        ShowOverlay(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { logo, name } },
                tagline,
                new TextBlock { Text = $"{version?.ToString(3)} · {Loc.T("By")} · {mpv}", Classes = { "muted" } },
                new TextBlock { Text = Loc.T("About"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) },
                new TextBlock { Text = Loc.T("Credits"), TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Classes = { "muted" } },
                new TextBlock { Text = Loc.T("AiNote"), TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Classes = { "muted" } },
                OverlayButton("Close", HideOverlay),
            },
        });
    }
}
