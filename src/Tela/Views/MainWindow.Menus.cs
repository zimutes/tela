using System.Diagnostics;
using System.Globalization;
using Tela.Library;
using Tela.Mpv;
using Tela.Themes;
using Avalonia.Controls;
using Avalonia.Input;

namespace Tela.Views;

public partial class MainWindow
{
    private static MenuItem Item(string header, Action click, string? gesture = null, bool enabled = true)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGesture = gesture is null ? null : KeyGesture.Parse(gesture),
            IsEnabled = enabled,
        };
        item.Click += (_, _) => click();
        return item;
    }

    private static MenuItem Radio(string header, bool on, Action click)
    {
        var item = new MenuItem { Header = header, ToggleType = MenuItemToggleType.Radio, IsChecked = on };
        item.Click += (_, _) => click();
        return item;
    }

    private static MenuItem Check(string header, bool on, Action click, string? gesture = null)
    {
        var item = new MenuItem
        {
            Header = header, ToggleType = MenuItemToggleType.CheckBox, IsChecked = on,
            InputGesture = gesture is null ? null : KeyGesture.Parse(gesture),
        };
        item.Click += (_, _) => click();
        return item;
    }

    private static MenuItem Sub(string header, params object[] items)
    {
        var sub = new MenuItem { Header = header };
        foreach (var i in items)
            sub.Items.Add(i);
        return sub;
    }

    /// <summary>O menu ☰ (e o do botão direito em cima do vídeo, que começa pelos comandos de reprodução).</summary>
    private MenuFlyout MainMenu(bool context = false)
    {
        var menu = new MenuFlyout();
        var loaded = _loaded;

        if (context && loaded)
        {
            menu.Items.Add(Item(Loc.T(_paused ? "Play" : "Pause"), PlayPause, "Space"));
            if (_queue.Count > 1)
            {
                menu.Items.Add(Item(Loc.T("NextVideo"), Next, "N", _queue.HasNext));
                menu.Items.Add(Item(Loc.T("PreviousVideo"), Previous, "P"));
            }
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Item(Loc.T("OpenFile"), () => _ = OpenFilesAsync(), "Ctrl+O"));
        menu.Items.Add(Item(Loc.T("OpenFolder"), () => _ = OpenFolderAsync(), "Ctrl+Shift+O"));
        if (loaded)
            menu.Items.Add(Item(Loc.T("AddSubtitle"), () => _ = OpenSubtitleAsync()));

        if (_queue.Count > 1)
            menu.Items.Add(QueueMenu());

        if (loaded)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(Sub(Loc.T("Subtitles"), [.. SubtitleItems()]));
            menu.Items.Add(Sub(Loc.T("Audio"), [.. AudioItems()]));
            menu.Items.Add(Sub(Loc.T("Video"), [.. VideoItems()]));
            menu.Items.Add(Sub(Loc.T("Speed"), [.. SpeedItems()]));
            menu.Items.Add(Sub(Loc.T("Repeat"),
                Check(Loc.T("RepeatVideo"), LoopFileOn, ToggleLoopFile, "Shift+R"),
                Item(Loc.T("LoopAb"), CycleAbLoop, "R")));
            menu.Items.Add(Item(Loc.T("Screenshot"), () => Screenshot(withSubtitles: true), "S", _hasVideo));
        }

        menu.Items.Add(new Separator());
        var theme = new MenuItem { Header = Loc.T("Theme") };
        foreach (var t in Catalog.All)
            theme.Items.Add(Radio(t.Name, ThemeManager.Current == t, () => SetTheme(t)));
        menu.Items.Add(theme);
        menu.Items.Add(Sub(Loc.T("Language"),
            Radio("English", Loc.Language == UiLanguage.En, () => SetLanguage(UiLanguage.En)),
            Radio("Português", Loc.Language == UiLanguage.Pt, () => SetLanguage(UiLanguage.Pt))));
        menu.Items.Add(Sub(Loc.T("Settings"),
            Check(Loc.T("AutoplayNext"), _settings.AutoplayNext, () => _settings.AutoplayNext = !_settings.AutoplayNext),
            Check(Loc.T("ResumeOption"), _settings.Resume, () => _settings.Resume = !_settings.Resume),
            Check(Loc.T("FitWindow"), _settings.FitWindow, () => _settings.FitWindow = !_settings.FitWindow),
            Check(Loc.T("ClickToPause"), _settings.ClickToPause, () => _settings.ClickToPause = !_settings.ClickToPause),
            Check(Loc.T("AlwaysOnTop"), _settings.AlwaysOnTop, ToggleOnTop, "T"),
            Check(Loc.T("HardwareDecoding"), _settings.HardwareDecoding, ToggleHardwareDecoding),
            Check(Loc.T("SoftwareVideo"), _settings.SoftwareVideo || SoftVideo.IsRunning, ToggleSoftwareVideo),
            new Separator(),
            Item(Loc.T("OpenScreenshots"), () => OpenFolderInShell(ScreenshotFolder()))));

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.T("Shortcuts"), ShowShortcuts, "F1"));
        if (loaded)
            menu.Items.Add(Check(Loc.T("MediaInfo"), InfoPanel.IsVisible, ToggleInfo, "Shift+I"));
        menu.Items.Add(Item(Loc.T("AboutMenu"), ShowAbout));
        menu.Items.Add(new Separator());
        if (loaded || _current is not null)
            menu.Items.Add(Item(Loc.T("CloseVideo"), CloseVideo, "Ctrl+W"));
        menu.Items.Add(Item(Loc.T("Quit"), Close, "Ctrl+Q"));
        return menu;
    }

    /// <summary>Os vídeos da lista (à volta do atual, para uma temporada longa não encher o ecrã).</summary>
    private MenuItem QueueMenu()
    {
        var sub = new MenuItem { Header = Loc.F("InThisFolder", _queue.Count) };
        var from = Math.Max(0, _queue.Index - 12);
        var to = Math.Min(_queue.Count, from + 25);
        for (var i = from; i < to; i++)
        {
            var index = i;
            var path = _queue.Items[i];
            var name = MediaFiles.DisplayName(path);
            var entry = _history.Find(path);
            if (entry is { Finished: true })
                name = "✓ " + name;
            else if (History.ResumePoint(entry) is not null)
                name += $"  ·  {FormatTime(entry!.Position)}";
            sub.Items.Add(Radio(name, i == _queue.Index, () => PlayIndex(index)));
        }
        return sub;
    }

    private void OpenFolderInShell(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write("Abrir pasta: " + ex.Message);
        }
    }

    private void SetTheme(ThemeDef theme)
    {
        _settings.Theme = theme.Key;
        ThemeManager.Apply(theme);
    }

    private void SetLanguage(UiLanguage language)
    {
        _settings.Language = Loc.Code(language);
        Loc.Apply(language, ThemeManager.Current.Uppercase);
    }

    private void ToggleHardwareDecoding()
    {
        _settings.HardwareDecoding = !_settings.HardwareDecoding;
        _player?.Set("hwdec", _settings.HardwareDecoding ? "auto-safe" : "no");
        ShowOsd(Loc.T(_settings.HardwareDecoding ? "HwOn" : "HwOff"), seconds: 1.5);
    }

    /// <summary>
    /// Para quem vê o vídeo preto (controladores gráficos com problemas): passa a desenhar por
    /// software já. Voltar ao OpenGL só ao abrir a Tela outra vez.
    /// </summary>
    private void ToggleSoftwareVideo()
    {
        _settings.SoftwareVideo = !_settings.SoftwareVideo;
        if (_settings.SoftwareVideo)
        {
            _silentReload = true;
            StartSoftwareVideo();
            ShowOsd(Loc.T("SoftwareVideoOn"), seconds: 3);
        }
        else
            ShowOsd(Loc.T("SoftwareVideoOff"), seconds: 4);
    }

    // ── Legendas ──────────────────────────────────────────────────────────────

    private MenuFlyout SubtitleMenu()
    {
        var menu = new MenuFlyout();
        foreach (var item in SubtitleItems())
            menu.Items.Add(item);
        return menu;
    }

    private List<object> SubtitleItems()
    {
        var subs = _tracks.Where(t => t.Type == "sub").ToList();
        var items = new List<object>
        {
            Radio(Loc.T("Off"), !subs.Any(t => t.Selected), () => SelectTrack("sub", null)),
        };
        foreach (var t in subs)
            items.Add(Radio(TrackLabel(t, includeTech: true), t.Selected, () => SelectTrack("sub", t)));
        items.Add(new Separator());
        items.Add(Item(Loc.T("AddSubtitle"), () => _ = OpenSubtitleAsync()));
        var size = new MenuItem { Header = Loc.T("SubSize") };
        foreach (var s in new[] { 0.75, 0.9, 1.0, 1.15, 1.3, 1.5, 2.0 })
            size.Items.Add(Radio($"{s * 100:0}%", Math.Abs(_settings.SubScale - s) < 0.01, () => SetSubScale(s)));
        items.Add(size);
        items.Add(Check(Loc.T("SubBackground"), _settings.SubBackground, ToggleSubBackground));
        var delay = _player?.GetDouble("sub-delay") ?? 0;
        items.Add(Sub(Loc.F("DelayNow", FormatDelay(delay)),
            Item(Loc.T("DelayEarlier"), () => ChangeSubDelay(-0.1), "Z"),
            Item(Loc.T("DelayLater"), () => ChangeSubDelay(0.1), "X"),
            Item(Loc.T("DelayReset"), () => ChangeSubDelay(0))));
        items.Add(LanguageMenu(Loc.T("PreferredSubs"), _settings.SubLanguages, code =>
        {
            _settings.SubLanguages = code;
            _player?.Set("slang", code);
        }));
        return items;
    }

    private void ToggleSubBackground()
    {
        _settings.SubBackground = !_settings.SubBackground;
        _player?.Set("sub-border-style", _settings.SubBackground ? "background-box" : "outline-and-shadow");
    }

    // ── Áudio ─────────────────────────────────────────────────────────────────

    private MenuFlyout AudioMenu()
    {
        var menu = new MenuFlyout();
        foreach (var item in AudioItems())
            menu.Items.Add(item);
        return menu;
    }

    private List<object> AudioItems()
    {
        var items = new List<object>();
        foreach (var t in _tracks.Where(t => t.Type == "audio"))
            items.Add(Radio(TrackLabel(t, includeTech: true), t.Selected, () => SelectTrack("audio", t)));
        if (items.Count > 0)
            items.Add(new Separator());
        items.Add(Check(Loc.T("Normalize"), _settings.Normalize, ToggleNormalize));
        var delay = _player?.GetDouble("audio-delay") ?? 0;
        items.Add(Sub(Loc.F("DelayNow", FormatDelay(delay)),
            Item(Loc.T("DelayEarlier"), () => ChangeAudioDelay(-0.1), "Shift+Z"),
            Item(Loc.T("DelayLater"), () => ChangeAudioDelay(0.1), "Shift+X"),
            Item(Loc.T("DelayReset"), () => ChangeAudioDelay(0))));
        var output = new MenuItem { Header = Loc.T("Output") };
        foreach (var (name, description) in _player?.ReadAudioDevices() ?? [])
        {
            var label = name == "auto" ? Loc.T("DefaultDevice") : description;
            output.Items.Add(Radio(label, _settings.AudioDevice == name, () =>
            {
                _settings.AudioDevice = name;
                _player?.Set("audio-device", name);
            }));
        }
        if (output.Items.Count > 0)
            items.Add(output);
        items.Add(LanguageMenu(Loc.T("PreferredAudio"), _settings.AudioLanguages, code =>
        {
            _settings.AudioLanguages = code;
            _player?.Set("alang", code);
        }));
        return items;
    }

    private void ToggleNormalize()
    {
        _settings.Normalize = !_settings.Normalize;
        _player?.Set("af", _settings.Normalize ? NormalizeFilter : "");
        ShowOsd(Loc.T(_settings.Normalize ? "NormalizeOn" : "NormalizeOff"), seconds: 1.5);
    }

    // ── Vídeo ─────────────────────────────────────────────────────────────────

    private static readonly (string Value, string Label)[] Aspects =
        [("-1", ""), ("16:9", "16:9"), ("4:3", "4:3"), ("2.35:1", "2.35:1"), ("1:1", "1:1")];

    private List<object> VideoItems()
    {
        var aspect = _player?.GetString("video-aspect-override") ?? "-1";
        var current = Aspects.FirstOrDefault(a => AspectMatches(aspect, a.Value)).Value ?? "-1";
        var aspectMenu = new MenuItem { Header = Loc.T("Aspect") };
        foreach (var (value, label) in Aspects)
            aspectMenu.Items.Add(Radio(value == "-1" ? Loc.T("Auto") : label, current == value, () => SetAspect(value)));
        var rotate = new MenuItem { Header = Loc.T("Rotate") };
        var angle = _player?.GetLong("video-rotate") ?? 0;
        foreach (var a in new[] { 0, 90, 180, 270 })
            rotate.Items.Add(Radio($"{a}°", angle == a, () => _player?.Set("video-rotate", (long)a)));
        return
        [
            aspectMenu,
            Check(Loc.T("CropFill"), (_player?.GetDouble("panscan") ?? 0) > 0.5, ToggleCrop, "C"),
            rotate,
            Check(Loc.T("Deinterlace"), _player?.GetFlag("deinterlace") ?? false, ToggleDeinterlace, "D"),
        ];
    }

    private static bool AspectMatches(string mpvValue, string option)
    {
        if (option == "-1")
            return mpvValue is "-1" or "no" or "" || mpvValue.StartsWith("-1", StringComparison.Ordinal);
        return mpvValue == option;
    }

    private void SetAspect(string value)
    {
        _player?.Set("video-aspect-override", value);
        var label = Aspects.First(a => a.Value == value).Label;
        ShowOsd(Loc.F("AspectOsd", value == "-1" ? Loc.T("Auto") : label), seconds: 1.5);
    }

    private void CycleAspect()
    {
        if (_player is null || !_hasVideo)
            return;
        var aspect = _player.GetString("video-aspect-override") ?? "-1";
        var i = Array.FindIndex(Aspects, a => AspectMatches(aspect, a.Value));
        SetAspect(Aspects[(i + 1) % Aspects.Length].Value);
    }

    private void ToggleCrop()
    {
        if (_player is null || !_hasVideo)
            return;
        var on = (_player.GetDouble("panscan") ?? 0) > 0.5;
        _player.Set("panscan", on ? 0.0 : 1.0);
        ShowOsd(Loc.T(on ? "CropOff" : "CropOn"), seconds: 1.5);
    }

    private void ToggleDeinterlace()
    {
        if (_player is null || !_hasVideo)
            return;
        var on = _player.GetFlag("deinterlace") ?? false;
        _player.Set("deinterlace", !on);
        ShowOsd(Loc.T(on ? "DeinterlaceOff" : "DeinterlaceOn"), seconds: 1.5);
    }

    // ── Velocidade ────────────────────────────────────────────────────────────

    private MenuFlyout SpeedMenu()
    {
        var menu = new MenuFlyout();
        foreach (var item in SpeedItems())
            menu.Items.Add(item);
        return menu;
    }

    private List<object> SpeedItems()
    {
        var current = _player?.GetDouble("speed") ?? 1;
        return SpeedSteps.Where(s => s <= 3)
            .Select(s => (object)Radio(s == 1 ? $"{FormatSpeed(s)}  ({Loc.T("Normal")})" : FormatSpeed(s), Math.Abs(current - s) < 0.001, () => SetSpeed(s)))
            .ToList();
    }

    // ── Faixas ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Escolha à mão de uma faixa: fica no histórico deste vídeo e a língua passa para o
    /// episódio seguinte da mesma lista.
    /// </summary>
    private void SelectTrack(string type, MediaTrack? track)
    {
        if (_player is null)
            return;
        var id = track?.Id.ToString(CultureInfo.InvariantCulture) ?? "no";
        if (type == "sub")
        {
            _player.Set("sid", id);
            _chosenSid = id;
            _sessionSubsOff = track is null;
            if (track?.Lang is { Length: > 0 } lang)
                _sessionSlang = lang;
            ShowOsd(Loc.F("SubtitlesOsd", track is null ? Loc.T("Off") : TrackLabel(track, includeTech: false)), seconds: 1.8);
        }
        else
        {
            _player.Set("aid", id);
            _chosenAid = id;
            if (track?.Lang is { Length: > 0 } lang)
                _sessionAlang = lang;
            ShowOsd(Loc.F("AudioOsd", track is null ? Loc.T("Off") : TrackLabel(track, includeTech: false)), seconds: 1.8);
        }
    }

    /// <summary>V e B: passa à faixa seguinte (nas legendas, com "desligadas" pelo meio).</summary>
    private void CycleTrack(string type)
    {
        var list = _tracks.Where(t => t.Type == type).ToList();
        if (list.Count == 0)
        {
            ShowOsd(Loc.T(type == "sub" ? "NoSubtitles" : "NoAudio"), seconds: 1.5);
            return;
        }
        var options = new List<MediaTrack?>(list);
        if (type == "sub")
            options.Insert(0, null);
        var current = list.FindIndex(t => t.Selected);
        var index = type == "sub" ? current + 1 : current;
        SelectTrack(type, options[(index + 1) % options.Count]);
    }

    private static string TrackLabel(MediaTrack t, bool includeTech)
    {
        var lang = LanguageName(t.Lang);
        var title = string.IsNullOrWhiteSpace(t.Title) ? null : t.Title!.Trim();
        string main;
        if (title is not null && lang is not null && !title.Contains(lang, StringComparison.OrdinalIgnoreCase)
            && !lang.Contains(title, StringComparison.OrdinalIgnoreCase))
            main = $"{title} · {lang}";
        else
            main = title ?? lang ?? (t.External && t.ExternalFile is { } f ? Path.GetFileName(f) : Loc.F("TrackN", t.Id));
        if (!includeTech)
            return main;
        var extras = new List<string>();
        if (t.Type == "audio" && t.Channels > 0)
            extras.Add(ChannelsText(t.Channels));
        if (!string.IsNullOrEmpty(t.Codec))
            extras.Add(CodecName(t.Codec!));
        if (t.Forced)
            extras.Add(Loc.T("Forced"));
        if (t.External)
            extras.Add(Loc.T("External"));
        return extras.Count == 0 ? main : $"{main}   ({string.Join(", ", extras)})";
    }

    private static string CodecName(string codec) => codec switch
    {
        "subrip" => "SRT",
        "hdmv_pgs_subtitle" => "PGS",
        "dvd_subtitle" => "VobSub",
        "webvtt" => "WebVTT",
        "mov_text" => "TX3G",
        "eac3" => "E-AC3",
        "truehd" => "TrueHD",
        _ => codec.ToUpperInvariant(),
    };

    internal static string ChannelsText(int channels) => channels switch
    {
        1 => Loc.T("Mono"),
        2 => Loc.T("Stereo"),
        6 => "5.1",
        7 => "6.1",
        8 => "7.1",
        _ => $"{channels} ch",
    };

    private static readonly Dictionary<string, string> Iso639 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "en", ["por"] = "pt", ["pob"] = "pt-BR", ["spa"] = "es", ["fre"] = "fr", ["fra"] = "fr",
        ["ger"] = "de", ["deu"] = "de", ["ita"] = "it", ["jpn"] = "ja", ["kor"] = "ko", ["chi"] = "zh",
        ["zho"] = "zh", ["rus"] = "ru", ["ara"] = "ar", ["hin"] = "hi", ["dut"] = "nl", ["nld"] = "nl",
        ["swe"] = "sv", ["nor"] = "nb", ["nob"] = "nb", ["dan"] = "da", ["fin"] = "fi", ["pol"] = "pl",
        ["tur"] = "tr", ["gre"] = "el", ["ell"] = "el", ["heb"] = "he", ["cze"] = "cs", ["ces"] = "cs",
        ["hun"] = "hu", ["rum"] = "ro", ["ron"] = "ro", ["ukr"] = "uk", ["tha"] = "th", ["vie"] = "vi",
        ["ind"] = "id", ["may"] = "ms", ["msa"] = "ms", ["cat"] = "ca", ["glg"] = "gl", ["baq"] = "eu",
        ["eus"] = "eu", ["hrv"] = "hr", ["srp"] = "sr", ["slv"] = "sl", ["slo"] = "sk", ["slk"] = "sk",
        ["bul"] = "bg", ["ice"] = "is", ["isl"] = "is", ["per"] = "fa", ["fas"] = "fa", ["fil"] = "fil",
        ["tam"] = "ta", ["tel"] = "te", ["ben"] = "bn", ["urd"] = "ur", ["lit"] = "lt", ["lav"] = "lv", ["est"] = "et",
    };

    /// <summary>"por" → "Português", "eng" → "English" (o nome da língua nela própria).</summary>
    internal static string? LanguageName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code is "und" or "mis" or "zxx")
            return null;
        var two = Iso639.TryGetValue(code, out var mapped) ? mapped : code;
        try
        {
            var culture = CultureInfo.GetCultureInfo(two);
            if (culture.CultureTypes.HasFlag(CultureTypes.UserCustomCulture) || string.IsNullOrEmpty(culture.NativeName)
                || culture.NativeName == two)
                return code.ToUpperInvariant();
            var name = culture.NativeName;
            return char.ToUpper(name[0], culture) + name[1..];
        }
        catch (CultureNotFoundException)
        {
            return code.ToUpperInvariant();
        }
    }

    /// <summary>Línguas preferidas (para legendas ou áudio): a primeira que existir no ficheiro ganha.</summary>
    private MenuItem LanguageMenu(string header, string current, Action<string> set)
    {
        (string Code, string Label)[] options =
        [
            ("", Loc.T("LangFile")),
            ("pt,por,pob,pt-BR,pt-PT", "Português"),
            ("en,eng", "English"),
            ("es,spa", "Español"),
            ("fr,fre,fra", "Français"),
            ("de,ger,deu", "Deutsch"),
            ("it,ita", "Italiano"),
            ("ja,jpn", "日本語"),
        ];
        var sub = new MenuItem { Header = header };
        foreach (var (code, label) in options)
            sub.Items.Add(Radio(label, current == code, () => set(code)));
        return sub;
    }
}
