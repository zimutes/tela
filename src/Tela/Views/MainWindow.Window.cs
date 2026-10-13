using Tela.Controls;
using Tela.Library;
using Tela.Platform;
using Tela.Themes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Tela.Views;

public partial class MainWindow
{
    private WindowsMediaKeys? _mediaKeys;
    private WindowState _beforeFullscreen = WindowState.Normal;
    private bool _pip;
    private (PixelPoint Position, double Width, double Height, WindowState State)? _beforePip;
    private bool _closing;

    private void SetupWindow()
    {
        // Moldura própria. No Windows fica a borda do sistema (redimensionar, sombra, encaixe);
        // no Linux a janela fica sem decorações e redimensiona-se pelas bordas invisíveis.
        if (OperatingSystem.IsWindows())
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 0;
        }
        else
        {
            SystemDecorations = SystemDecorations.None;
            SetupResizeEdges();
        }

        Topmost = _settings.AlwaysOnTop;
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;

        TopBar.PointerPressed += (_, e) =>
        {
            if ((e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null)
                return;
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            if (e.ClickCount == 2)
                ToggleMaximize();
            else if (WindowState != WindowState.FullScreen)
                BeginMoveDrag(e);
            e.Handled = true;
        };
        MenuButton.Click += (_, _) => MainMenu().ShowAt(MenuButton);
        MinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeButton.Click += (_, _) => ToggleMaximize();
        CloseButton.Click += (_, _) => Close();

        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
                OnWindowStateChanged();
            else if (e.Property == BoundsProperty)
                OnSizeChanged();
        };

        Opened += (_, _) =>
        {
            RestorePosition();
            if (_settings.Maximized)
                WindowState = WindowState.Maximized;
            ApplyWindowCorners();
            if (OperatingSystem.IsWindows() && !_demo)
                _mediaKeys = WindowsMediaKeys.Start(key => Dispatcher.UIThread.Post(() => OnMediaKey(key)));
        };
        Closing += OnClosing;
    }

    private void SetupResizeEdges()
    {
        ResizeEdges.IsVisible = true;
        foreach (var (edge, which) in new (Border, WindowEdge)[]
        {
            (EdgeN, WindowEdge.North), (EdgeS, WindowEdge.South), (EdgeW, WindowEdge.West), (EdgeE, WindowEdge.East),
            (EdgeNW, WindowEdge.NorthWest), (EdgeNE, WindowEdge.NorthEast), (EdgeSW, WindowEdge.SouthWest), (EdgeSE, WindowEdge.SouthEast),
        })
        {
            edge.PointerPressed += (_, e) =>
            {
                if (WindowState == WindowState.Normal && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    BeginResizeDrag(which, e);
                    e.Handled = true;
                }
            };
        }
    }

    private void OnWindowStateChanged()
    {
        var full = WindowState == WindowState.FullScreen;
        FullscreenGlyph.Kind = full ? GlyphKind.ExitFullscreen : GlyphKind.Fullscreen;
        MaximizeGlyph.Kind = WindowState == WindowState.Maximized ? GlyphKind.Restore : GlyphKind.Maximize;
        ToolTip.SetTip(MaximizeButton, Loc.T(WindowState == WindowState.Maximized ? "Restore" : "Maximize"));
        ResizeEdges.IsVisible = !OperatingSystem.IsWindows() && WindowState == WindowState.Normal;
        MinimizeButton.IsVisible = MaximizeButton.IsVisible = !full;
        // Em ecrã inteiro a barra de comandos encosta às bordas.
        BottomBar.Margin = full ? new Thickness(24, 0, 24, 24) : new Thickness(12, 0, 12, 12);
        UpdateCursor();
    }

    private void OnSizeChanged()
    {
        // Janela estreita (ou mini): saem os botões menos precisos.
        Classes.Set("narrow", Bounds.Width < 620 || _pip);
        HomeGrid.MinHeight = Math.Max(0, HomeScroll.Bounds.Height);
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateSubtitlePosition, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void ToggleMaximize()
    {
        if (_pip)
            ExitPip();
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void ToggleFullscreen()
    {
        if (_pip)
            ExitPip();
        if (WindowState == WindowState.FullScreen)
            WindowState = _beforeFullscreen == WindowState.FullScreen ? WindowState.Normal : _beforeFullscreen;
        else
        {
            _beforeFullscreen = WindowState;
            WindowState = WindowState.FullScreen;
        }
    }

    // ── Mini-janela (vídeo pequeno, sempre por cima, num canto) ────────────────

    private void TogglePip()
    {
        if (_pip)
            ExitPip();
        else
            EnterPip();
    }

    private void EnterPip()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;
        if (WindowState == WindowState.FullScreen)
            WindowState = _beforeFullscreen == WindowState.FullScreen ? WindowState.Normal : _beforeFullscreen;
        _beforePip = (Position, Width, Height, WindowState);
        WindowState = WindowState.Normal;
        _pip = true;
        var scale = screen.Scaling;
        var area = screen.WorkingArea;
        var aspect = _dw > 0 && _dh > 0 ? (double)_dw / _dh : 16.0 / 9;
        var w = Math.Clamp(area.Width / scale * 0.26, 320, 560);
        var h = Math.Max(MinHeight, w / aspect);
        Width = w;
        Height = h;
        Position = new PixelPoint(area.Right - (int)(w * scale) - 24, area.Bottom - (int)(h * scale) - 24);
        Topmost = true;
        PipGlyph.Opacity = 1;
        PipButton.Classes.Set("on", true);
        OnSizeChanged();
        ShowChrome(temporary: true);
    }

    private void ExitPip()
    {
        if (!_pip)
            return;
        _pip = false;
        Topmost = _settings.AlwaysOnTop;
        PipButton.Classes.Set("on", false);
        if (_beforePip is { } b)
        {
            Width = b.Width;
            Height = b.Height;
            Position = b.Position;
            WindowState = b.State;
        }
        _beforePip = null;
        OnSizeChanged();
    }

    // ── Janela à medida do vídeo ──────────────────────────────────────────────

    /// <summary>
    /// Ao abrir um vídeo, a janela fica com a forma dele (sem barras pretas). Vindo do ecrã
    /// inicial, também com um tamanho à medida; depois disso, mantém a largura que o utilizador deu.
    /// </summary>
    private void MaybeFitWindow()
    {
        if (!_fitPending || _dw <= 0 || _dh <= 0)
            return;
        _fitPending = false;
        if (!_settings.FitWindow || _pip || WindowState != WindowState.Normal)
        {
            _firstFit = false;
            return;
        }
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;
        var scale = screen.Scaling;
        var area = screen.WorkingArea;
        var maxW = area.Width / scale * 0.85;
        var maxH = area.Height / scale * 0.85;
        var aspect = (double)_dw / _dh;
        double w = _firstFit ? Math.Min(_dw / scale, maxW) : Bounds.Width;
        w = Math.Max(w, Math.Min(640, maxW));
        var h = w / aspect;
        if (h > maxH)
        {
            h = maxH;
            w = h * aspect;
        }
        _firstFit = false;
        if (Math.Abs(w - Bounds.Width) < 2 && Math.Abs(h - Bounds.Height) < 2)
            return;
        // O centro da janela fica onde estava; e a janela, dentro do ecrã.
        var cx = Position.X + Bounds.Width * scale / 2;
        var cy = Position.Y + Bounds.Height * scale / 2;
        Width = w;
        Height = h;
        var x = (int)Math.Clamp(cx - w * scale / 2, area.X, Math.Max(area.X, area.Right - w * scale));
        var y = (int)Math.Clamp(cy - h * scale / 2, area.Y, Math.Max(area.Y, area.Bottom - h * scale));
        Position = new PixelPoint(x, y);
    }

    private void RestorePosition()
    {
        if (_settings.WindowX is not { } x || _settings.WindowY is not { } y)
            return;
        var point = new PixelPoint((int)x, (int)y);
        // Só se ainda couber num ecrã (o portátil pode estar sem o monitor de ontem).
        if (Screens.All.Any(s => s.WorkingArea.Contains(point + new PixelPoint(40, 20))))
            Position = point;
    }

    private void RememberWindow()
    {
        _settings.Maximized = WindowState == WindowState.Maximized
            || (WindowState == WindowState.FullScreen && _beforeFullscreen == WindowState.Maximized);
        if (WindowState != WindowState.Normal || _pip)
            return;
        _settings.WindowX = Position.X;
        _settings.WindowY = Position.Y;
        if (Bounds.Width > 0)
        {
            _settings.WindowWidth = Bounds.Width;
            _settings.WindowHeight = Bounds.Height;
        }
    }

    private void ApplyWindowCorners()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero)
            WindowsDwm.Apply(handle, ThemeManager.Current.RoundWindow);
    }

    // ── Fechar ────────────────────────────────────────────────────────────────

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closing)
            return;
        _closing = true;
        if (_pip)
            ExitPip();
        SaveProgress(thumb: true);
        RememberWindow();
        SettingsStore.Save(_settings);
        _clock.Stop();
        _autosave.Stop();
        _mediaKeys?.Dispose();
        _screenSaver.Dispose();
        ResetThumbs();
        // A imagem do "continuar a ver" pode ainda estar a ser tirada: espera-se um pouco.
        Task[] jobs;
        lock (_thumbJobs)
            jobs = _thumbJobs.ToArray();
        Task.WaitAll(jobs, TimeSpan.FromSeconds(4));
        // O contexto de desenho da libmpv tem de ir antes do leitor: tirar a vista do vídeo da
        // árvore liberta-o (com o OpenGL ainda vivo).
        Root.Children.Remove(Video);
        SoftVideo.Stop();
        _player?.Dispose();
        _player = null;
    }

    // ── Linha de comandos e segunda instância ─────────────────────────────────

    // Com vários ficheiros escolhidos, o Explorador lança um processo por ficheiro e cada um
    // manda só o seu caminho. O que chega quase junto é uma seleção: junta-se e abre-se de uma vez.
    private readonly List<string> _incoming = [];
    private DispatcherTimer? _incomingTimer;

    public void HandleArgs(string[] args, bool startup)
    {
        var paths = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (!startup)
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Activate();
        }
        if (paths.Count == 0)
            return;
        _incoming.AddRange(paths);
        if (_incomingTimer is null)
        {
            _incomingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(startup ? 500 : 350) };
            _incomingTimer.Tick += (_, _) =>
            {
                _incomingTimer!.Stop();
                var batch = _incoming.ToList();
                _incoming.Clear();
                Open(batch);
            };
        }
        _incomingTimer.Stop();
        _incomingTimer.Start();
    }

    // ── Arrastar e largar ─────────────────────────────────────────────────────

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
        if (paths.Count == 0)
            return;
        // Legendas largadas em cima de um vídeo juntam-se a ele; o resto abre-se.
        if (_loaded && paths.All(MediaFiles.IsSubtitle))
        {
            foreach (var p in paths)
                AddSubtitle(p);
            return;
        }
        Activate();
        Open(paths);
    }

    // ── Teclado ───────────────────────────────────────────────────────────────

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (Overlay.IsVisible)
        {
            if (e.Key is Key.Escape or Key.Enter or Key.F1)
            {
                HideOverlay();
                e.Handled = true;
            }
            return;
        }
        if (e.Source is TextBox)
            return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var plain = !ctrl && !alt;

        // Teclas do ecrã inicial: abrir e escolher um cartão do "continuar a ver".
        if (Home.IsVisible && HomeKey(e.Key, ctrl, shift))
        {
            e.Handled = true;
            return;
        }

        var handled = true;
        switch (e.Key)
        {
            case Key.Space or Key.K when plain:
            case Key.MediaPlayPause:
                PlayPause();
                break;
            case Key.Left when plain:
                SeekBy(shift ? -1 : -5);
                break;
            case Key.Right when plain:
                SeekBy(shift ? 1 : 5);
                break;
            case Key.Left when ctrl:
            case Key.PageUp:
                SeekChapter(-1);
                break;
            case Key.Right when ctrl:
            case Key.PageDown:
                SeekChapter(1);
                break;
            case Key.J when plain:
                SeekBy(-10);
                break;
            case Key.L when plain:
                SeekBy(10);
                break;
            case Key.Up when plain:
                ChangeVolume(5);
                break;
            case Key.Down when plain:
                ChangeVolume(-5);
                break;
            case Key.M when plain:
                ToggleMute();
                break;
            case Key.F when plain:
            case Key.Enter when alt:
            case Key.F11:
                ToggleFullscreen();
                break;
            case Key.Escape:
                if (UpNext.IsVisible)
                    CancelUpNext();
                else if (WindowState == WindowState.FullScreen)
                    ToggleFullscreen();
                else if (_pip)
                    ExitPip();
                else if (InfoPanel.IsVisible)
                    InfoPanel.IsVisible = false;
                break;
            case Key.Enter when plain && UpNext.IsVisible:
                UpNextPrimary();
                break;
            case Key.N when plain:
            case Key.MediaNextTrack:
                Next();
                break;
            case Key.P when plain:
            case Key.MediaPreviousTrack:
                Previous();
                break;
            case Key.MediaStop:
                if (_loaded && !_paused)
                    PlayPause();
                break;
            case Key.OemPeriod when plain:
                FrameStep(back: false);
                break;
            case Key.OemComma when plain:
                FrameStep(back: true);
                break;
            case Key.OemOpenBrackets when plain:
                ChangeSpeed(-1);
                break;
            case Key.OemCloseBrackets when plain:
                ChangeSpeed(1);
                break;
            case Key.Back when plain:
                SetSpeed(1);
                break;
            case >= Key.D0 and <= Key.D9 when plain:
                SeekPercent((e.Key - Key.D0) * 10);
                break;
            case >= Key.NumPad0 and <= Key.NumPad9 when plain:
                SeekPercent((e.Key - Key.NumPad0) * 10);
                break;
            case Key.Home when plain:
                SeekTo(0);
                ShowChrome(temporary: true);
                break;
            case Key.S when plain:
                Screenshot(withSubtitles: !shift);
                break;
            case Key.V when plain:
                CycleTrack("sub");
                break;
            case Key.B when plain:
                CycleTrack("audio");
                break;
            case Key.Z when plain:
                if (shift) ChangeAudioDelay(-0.1); else ChangeSubDelay(-0.1);
                break;
            case Key.X when plain:
                if (shift) ChangeAudioDelay(0.1); else ChangeSubDelay(0.1);
                break;
            case Key.G when plain:
                SetSubScale(_settings.SubScale - 0.1);
                break;
            case Key.H when plain:
                SetSubScale(_settings.SubScale + 0.1);
                break;
            case Key.R when plain:
                if (shift) ToggleLoopFile(); else CycleAbLoop();
                break;
            case Key.A when plain:
                CycleAspect();
                break;
            case Key.C when plain:
                ToggleCrop();
                break;
            case Key.D when plain:
                ToggleDeinterlace();
                break;
            case Key.I when plain && shift:
                ToggleInfo();
                break;
            case Key.I when plain:
                TogglePip();
                break;
            case Key.T when plain:
                ToggleOnTop();
                break;
            case Key.O when ctrl && shift:
                _ = OpenFolderAsync();
                break;
            case Key.O when ctrl || plain:
                _ = OpenFilesAsync();
                break;
            case Key.V when ctrl:
                _ = PasteAsync();
                break;
            case Key.W when ctrl:
                if (_current is not null) CloseVideo();
                break;
            case Key.Q when ctrl:
                Close();
                break;
            case Key.F1:
            case Key.OemQuestion when shift:
                ShowShortcuts();
                break;
            default:
                handled = false;
                break;
        }
        if (handled)
            e.Handled = true;
    }

    private void OnMediaKey(WindowsMediaKeys.MediaKey key)
    {
        switch (key)
        {
            case WindowsMediaKeys.MediaKey.PlayPause: PlayPause(); break;
            case WindowsMediaKeys.MediaKey.Next: Next(); break;
            case WindowsMediaKeys.MediaKey.Previous: Previous(); break;
            case WindowsMediaKeys.MediaKey.Stop:
                if (_loaded && !_paused)
                    PlayPause();
                break;
        }
    }

    private void ToggleOnTop()
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
        if (!_pip)
            Topmost = _settings.AlwaysOnTop;
        ShowOsd(Loc.T(_settings.AlwaysOnTop ? "OnTopOn" : "OnTopOff"), seconds: 1.5);
    }

    // ── Abrir (escolher, colar) ───────────────────────────────────────────────

    private async Task<IStorageFolder?> StartFolder()
    {
        try
        {
            if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
                return await StorageProvider.TryGetFolderFromPathAsync(_settings.LastFolder);
        }
        catch { }
        return null;
    }

    private async Task OpenFilesAsync()
    {
        var video = new FilePickerFileType(Loc.T("VideoFiles"))
        {
            Patterns = MediaFiles.VideoExtensions.Concat(MediaFiles.AudioExtensions).Select(e => "*" + e).ToList(),
        };
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = Loc.T("OpenFile"),
            FileTypeFilter = [video, FilePickerFileTypes.All],
            SuggestedStartLocation = await StartFolder(),
        });
        Open(files.Select(f => f.TryGetLocalPath()).OfType<string>().ToList());
    }

    private async Task OpenFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.T("OpenFolder"),
            SuggestedStartLocation = await StartFolder(),
        });
        var paths = folders.Select(f => f.TryGetLocalPath()).OfType<string>().ToList();
        if (paths.Count > 0)
            _settings.LastFolder = paths[0];
        Open(paths);
    }

    private async Task OpenSubtitleAsync()
    {
        if (!_loaded)
            return;
        var subs = new FilePickerFileType(Loc.T("SubtitleFiles"))
        {
            Patterns = MediaFiles.SubtitleExtensions.Select(e => "*" + e).ToList(),
        };
        var start = _current is not null && Path.GetDirectoryName(_current) is { } dir
            ? await StorageProvider.TryGetFolderFromPathAsync(dir)
            : null;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = Loc.T("AddSubtitle"),
            FileTypeFilter = [subs, FilePickerFileTypes.All],
            SuggestedStartLocation = start,
        });
        foreach (var path in files.Select(f => f.TryGetLocalPath()).OfType<string>())
            AddSubtitle(path);
    }

    /// <summary>Ctrl+V: ficheiros copiados no explorador, ou um caminho escrito.</summary>
    private async Task PasteAsync()
    {
        if (Clipboard is null)
            return;
        var paths = new List<string>();
        try
        {
            var files = await Clipboard.TryGetFilesAsync();
            if (files is not null)
                paths.AddRange(files.Select(f => f.TryGetLocalPath()).OfType<string>());
            if (paths.Count == 0 && await Clipboard.TryGetTextAsync() is { } text)
                paths.AddRange(text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => t.Trim('"')).Where(t => File.Exists(t) || Directory.Exists(t)));
        }
        catch (Exception ex)
        {
            Log.Write("Colar: " + ex.Message);
        }
        if (paths.Count > 0)
            Open(paths);
    }
}
