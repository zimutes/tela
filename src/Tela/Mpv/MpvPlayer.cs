using System.Globalization;

namespace Tela.Mpv;

/// <summary>Uma faixa do ficheiro aberto (vídeo, áudio ou legendas).</summary>
public sealed record MediaTrack(
    long Id, string Type, string? Title, string? Lang, string? Codec, bool Selected, bool External,
    bool Default, bool Forced, int Channels, int Width, int Height, bool AlbumArt, string? ExternalFile);

/// <summary>Um capítulo: onde começa e como se chama.</summary>
public sealed record Chapter(double Time, string? Title);

/// <summary>
/// O leitor: uma instância da libmpv com a thread de eventos. As mudanças de propriedades
/// chegam à interface pelo despachante que se lhe dá (a thread da interface, na janela); a
/// posição (que muda a cada imagem) não se despacha: lê-se em <see cref="TimePos"/> quando é
/// preciso, para não inundar a interface.
/// </summary>
public sealed unsafe class MpvPlayer : IDisposable
{
    private readonly Action<Action> _post;
    private readonly Thread _events;
    private readonly Dictionary<ulong, string> _observed = [];
    private ulong _nextObserveId = 1;
    private double _timePos;
    private int _disposed;

    public IntPtr Handle { get; }

    /// <summary>Uma propriedade observada mudou (na thread do despachante).</summary>
    public event Action<string, object?>? PropertyChanged;
    public event Action? FileLoaded;
    public event Action<Native.EndFileReason, int>? EndFile;
    /// <summary>A reprodução recomeçou depois de abrir ou de um salto.</summary>
    public event Action? PlaybackRestart;
    /// <summary>Mensagens de aviso ou erro da libmpv (na thread de eventos).</summary>
    public event Action<string>? LogMessage;

    /// <summary>
    /// Opções de segurança, iguais em todas as instâncias: a Tela abre ficheiros que não fez.
    /// Sem configuração nem scripts do utilizador (o mpv.conf e os scripts Lua de quem tem o mpv
    /// instalado), sem youtube-dl, e os formatos que abrem outros ficheiros por dentro (HLS,
    /// concat) só podem abrir ficheiros locais, nunca endereços na rede.
    /// </summary>
    public static readonly KeyValuePair<string, string>[] SafeDefaults =
    [
        new("config", "no"),
        new("load-scripts", "no"),
        new("ytdl", "no"),
        new("load-unsafe-playlists", "no"),
        new("demuxer-lavf-o", "protocol_whitelist=[file,crypto,data]"),
        new("input-default-bindings", "no"),
        new("input-vo-keyboard", "no"),
        new("terminal", "no"),
    ];

    /// <summary>Posição em segundos, atualizada pela thread de eventos (NaN sem ficheiro).</summary>
    public double TimePos => Volatile.Read(ref _timePos);

    private MpvPlayer(IntPtr handle, Action<Action> post)
    {
        Handle = handle;
        _post = post;
        _timePos = double.NaN;
        _events = new Thread(EventLoop) { IsBackground = true, Name = "mpv-eventos" };
    }

    /// <summary>
    /// Cria e inicializa. <paramref name="options"/> são opções da libmpv aplicadas antes de
    /// inicializar (as que só valem nessa altura, como vo).
    /// </summary>
    public static MpvPlayer Create(IEnumerable<KeyValuePair<string, string>> options, Action<Action> post, string logLevel = "warn")
    {
        var handle = Native.mpv_create();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException("mpv_create falhou");
        var player = new MpvPlayer(handle, post);
        foreach (var (name, value) in SafeDefaults.Concat(options))
        {
            var err = player.SetOptionRaw(name, value);
            if (err < 0)
                Tela.Log.Write($"mpv: opção {name}={value} recusada ({Native.ErrorText(err)})");
        }
        var init = Native.mpv_initialize(handle);
        if (init < 0)
        {
            Native.mpv_terminate_destroy(handle);
            throw new InvalidOperationException("mpv_initialize: " + Native.ErrorText(init));
        }
        var level = Native.Utf8(logLevel);
        Native.mpv_request_log_messages(handle, level);
        Native.Free(level);
        player._events.Start();
        return player;
    }

    private int SetOptionRaw(string name, string value)
    {
        var n = Native.Utf8(name);
        var v = Native.Utf8(value);
        try
        {
            return Native.mpv_set_option_string(Handle, n, v);
        }
        finally
        {
            Native.Free(n);
            Native.Free(v);
        }
    }

    private bool Alive => Volatile.Read(ref _disposed) == 0;

    // ── Propriedades ──────────────────────────────────────────────────────────

    public void Observe(string name, Native.Format format)
    {
        var id = _nextObserveId++;
        lock (_observed)
            _observed[id] = name;
        var n = Native.Utf8(name);
        Native.mpv_observe_property(Handle, id, n, format);
        Native.Free(n);
    }

    public int Set(string name, string value)
    {
        if (!Alive) return -1;
        var n = Native.Utf8(name);
        var v = Native.Utf8(value);
        try
        {
            return Native.mpv_set_property_string(Handle, n, v);
        }
        finally
        {
            Native.Free(n);
            Native.Free(v);
        }
    }

    public int Set(string name, double value)
    {
        if (!Alive) return -1;
        var n = Native.Utf8(name);
        try
        {
            return Native.mpv_set_property(Handle, n, Native.Format.Double, &value);
        }
        finally
        {
            Native.Free(n);
        }
    }

    public int Set(string name, bool value)
    {
        if (!Alive) return -1;
        var n = Native.Utf8(name);
        var flag = value ? 1 : 0;
        try
        {
            return Native.mpv_set_property(Handle, n, Native.Format.Flag, &flag);
        }
        finally
        {
            Native.Free(n);
        }
    }

    public int Set(string name, long value)
    {
        if (!Alive) return -1;
        var n = Native.Utf8(name);
        try
        {
            return Native.mpv_set_property(Handle, n, Native.Format.Int64, &value);
        }
        finally
        {
            Native.Free(n);
        }
    }

    public string? GetString(string name)
    {
        if (!Alive) return null;
        var n = Native.Utf8(name);
        try
        {
            var p = Native.mpv_get_property_string(Handle, n);
            if (p == IntPtr.Zero)
                return null;
            var s = Native.FromUtf8(p);
            Native.mpv_free(p);
            return s;
        }
        finally
        {
            Native.Free(n);
        }
    }

    public double? GetDouble(string name)
    {
        if (!Alive) return null;
        var n = Native.Utf8(name);
        double v;
        try
        {
            return Native.mpv_get_property(Handle, n, Native.Format.Double, &v) >= 0 ? v : null;
        }
        finally
        {
            Native.Free(n);
        }
    }

    public long? GetLong(string name)
    {
        if (!Alive) return null;
        var n = Native.Utf8(name);
        long v;
        try
        {
            return Native.mpv_get_property(Handle, n, Native.Format.Int64, &v) >= 0 ? v : null;
        }
        finally
        {
            Native.Free(n);
        }
    }

    public bool? GetFlag(string name)
    {
        if (!Alive) return null;
        var n = Native.Utf8(name);
        int v;
        try
        {
            return Native.mpv_get_property(Handle, n, Native.Format.Flag, &v) >= 0 ? v != 0 : null;
        }
        finally
        {
            Native.Free(n);
        }
    }

    // ── Comandos ──────────────────────────────────────────────────────────────

    /// <summary>Corre um comando e espera por ele. Devolve o código da libmpv (negativo = erro).</summary>
    public int Command(params string[] args) => RunCommand(args, async: false);

    /// <summary>Corre um comando sem esperar (saltos, carregar ficheiros).</summary>
    public int CommandAsync(params string[] args) => RunCommand(args, async: true);

    private int RunCommand(string[] args, bool async)
    {
        if (!Alive) return -1;
        var ptrs = stackalloc byte*[args.Length + 1];
        for (var i = 0; i < args.Length; i++)
            ptrs[i] = Native.Utf8(args[i]);
        ptrs[args.Length] = null;
        try
        {
            var err = async ? Native.mpv_command_async(Handle, 0, ptrs) : Native.mpv_command(Handle, ptrs);
            if (err < 0)
                Tela.Log.Write($"mpv: comando {string.Join(' ', args.Take(2))} falhou ({Native.ErrorText(err)})");
            return err;
        }
        finally
        {
            for (var i = 0; i < args.Length; i++)
                Native.Free(ptrs[i]);
        }
    }

    // ── Listas (faixas, capítulos, dispositivos) ──────────────────────────────

    public List<MediaTrack> ReadTracks()
    {
        var list = new List<MediaTrack>();
        var count = GetLong("track-list/count") ?? 0;
        for (var i = 0; i < count; i++)
        {
            var p = $"track-list/{i}/";
            list.Add(new MediaTrack(
                Id: GetLong(p + "id") ?? 0,
                Type: GetString(p + "type") ?? "",
                Title: GetString(p + "title"),
                Lang: GetString(p + "lang"),
                Codec: GetString(p + "codec"),
                Selected: GetFlag(p + "selected") ?? false,
                External: GetFlag(p + "external") ?? false,
                Default: GetFlag(p + "default") ?? false,
                Forced: GetFlag(p + "forced") ?? false,
                Channels: (int)(GetLong(p + "demux-channel-count") ?? 0),
                Width: (int)(GetLong(p + "demux-w") ?? 0),
                Height: (int)(GetLong(p + "demux-h") ?? 0),
                AlbumArt: GetFlag(p + "albumart") ?? false,
                ExternalFile: GetString(p + "external-filename")));
        }
        return list;
    }

    public List<Chapter> ReadChapters()
    {
        var list = new List<Chapter>();
        var count = GetLong("chapter-list/count") ?? 0;
        for (var i = 0; i < count; i++)
            list.Add(new Chapter(GetDouble($"chapter-list/{i}/time") ?? 0, GetString($"chapter-list/{i}/title")));
        return list;
    }

    /// <summary>Saídas de som: (nome interno, descrição).</summary>
    public List<(string Name, string Description)> ReadAudioDevices()
    {
        var list = new List<(string, string)>();
        var count = GetLong("audio-device-list/count") ?? 0;
        for (var i = 0; i < count; i++)
        {
            var name = GetString($"audio-device-list/{i}/name");
            if (name is null)
                continue;
            list.Add((name, GetString($"audio-device-list/{i}/description") ?? name));
        }
        return list;
    }

    // ── Eventos ───────────────────────────────────────────────────────────────

    private void EventLoop()
    {
        while (true)
        {
            var e = Native.mpv_wait_event(Handle, -1);
            if (e == null)
                continue;
            switch (e->EventId)
            {
                case Native.EventId.Shutdown:
                    return;
                case Native.EventId.PropertyChange:
                    OnProperty(e);
                    break;
                case Native.EventId.FileLoaded:
                    _post(() => FileLoaded?.Invoke());
                    break;
                case Native.EventId.EndFile:
                {
                    var end = (Native.EventEndFile*)e->Data;
                    var reason = end->Reason;
                    var error = end->Error;
                    Volatile.Write(ref _timePos, double.NaN);
                    _post(() => EndFile?.Invoke(reason, error));
                    break;
                }
                case Native.EventId.PlaybackRestart:
                    _post(() => PlaybackRestart?.Invoke());
                    break;
                case Native.EventId.LogMessage:
                {
                    var m = (Native.EventLogMessage*)e->Data;
                    var text = $"{Native.FromUtf8(m->Prefix)}: {Native.FromUtf8(m->Text)?.TrimEnd()}";
                    LogMessage?.Invoke(text);
                    break;
                }
            }
        }
    }

    private void OnProperty(Native.Event* e)
    {
        var p = (Native.EventProperty*)e->Data;
        var name = Native.FromUtf8(p->Name) ?? "";
        object? value = p->Format switch
        {
            Native.Format.Double when p->Data != IntPtr.Zero => *(double*)p->Data,
            Native.Format.Flag when p->Data != IntPtr.Zero => *(int*)p->Data != 0,
            Native.Format.Int64 when p->Data != IntPtr.Zero => *(long*)p->Data,
            Native.Format.String when p->Data != IntPtr.Zero => Native.FromUtf8(*(IntPtr*)p->Data),
            _ => null,
        };
        if (name == "time-pos")
        {
            Volatile.Write(ref _timePos, value is double d ? d : double.NaN);
            return;
        }
        _post(() => PropertyChanged?.Invoke(name, value));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        // "quit" faz o núcleo parar e mandar SHUTDOWN; só depois de a thread de eventos sair é
        // que o identificador pode ser destruído.
        var q = Native.Utf8("quit");
        var args = stackalloc byte*[2];
        args[0] = q;
        args[1] = null;
        Native.mpv_command(Handle, args);
        Native.Free(q);
        if (!_events.Join(TimeSpan.FromSeconds(3)))
            Tela.Log.Write("mpv: a thread de eventos não saiu a tempo");
        Native.mpv_terminate_destroy(Handle);
    }

    public static string Seconds(double s) => s.ToString("0.###", CultureInfo.InvariantCulture);
}
