using System.Runtime.InteropServices;

namespace Tela.Mpv;

/// <summary>Uma imagem pequena do vídeo, em BGRA (o quarto byte não conta).</summary>
public sealed record ThumbFrame(double Time, int Width, int Height, int Stride, byte[] Pixels);

/// <summary>
/// Imagens do vídeo num ponto qualquer, para a pré-visualização da barra e para o "continuar a
/// ver". Uma segunda instância da libmpv, sem som, sem legendas e sem placa gráfica, que desenha
/// em memória (API de desenho por software) a imagem-chave mais próxima. Tudo numa thread só:
/// pedidos novos substituem os que ainda não começaram.
/// </summary>
public sealed unsafe class Thumbnailer : IDisposable
{
    private readonly string _path;
    private readonly int _width;
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly object _gate = new();
    private (double Time, Action<ThumbFrame> Done)? _request;
    private volatile bool _stop;
    private IntPtr _mpv;
    private IntPtr _ctx;
    private Native.UpdateCallbackFn? _onUpdate;
    private int _w, _h, _stride;
    private byte[]? _buffer;

    /// <summary>Não há vídeo (só som) ou a libmpv não abriu o ficheiro.</summary>
    public bool Failed { get; private set; }

    public string Path => _path;

    public Thumbnailer(string path, int width = 256)
    {
        _path = path;
        _width = Math.Max(32, width & ~1);
        _thread = new Thread(Run) { IsBackground = true, Name = "mpv-miniaturas", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    /// <summary>Pede a imagem perto de <paramref name="time"/>; a resposta chega na thread das miniaturas.</summary>
    public void Request(double time, Action<ThumbFrame> done)
    {
        lock (_gate)
            _request = (time, done);
        _wake.Set();
    }

    /// <summary>Uma imagem só, à espera dela (o "continuar a ver" ao fechar um vídeo).</summary>
    public static ThumbFrame? Capture(string path, double time, int width, TimeSpan timeout)
    {
        using var t = new Thumbnailer(path, width);
        using var done = new ManualResetEventSlim(false);
        ThumbFrame? result = null;
        t.Request(time, f =>
        {
            result = f;
            done.Set();
        });
        done.Wait(timeout);
        return result;
    }

    private void Run()
    {
        try
        {
            if (!Open())
            {
                Failed = true;
                return;
            }
            while (!_stop)
            {
                _wake.WaitOne();
                if (_stop)
                    break;
                (double Time, Action<ThumbFrame> Done)? request;
                lock (_gate)
                {
                    request = _request;
                    _request = null;
                }
                if (request is not { } r)
                    continue;
                var frame = Grab(r.Time);
                if (frame is not null && !_stop)
                    r.Done(frame);
            }
        }
        catch (Exception ex)
        {
            Failed = true;
            Log.Write("Miniaturas: " + ex.Message);
        }
        finally
        {
            Close();
        }
    }

    private void Option(string name, string value)
    {
        var n = Native.Utf8(name);
        var v = Native.Utf8(value);
        Native.mpv_set_option_string(_mpv, n, v);
        Native.Free(n);
        Native.Free(v);
    }

    private void Command(params string[] args)
    {
        var ptrs = stackalloc byte*[args.Length + 1];
        for (var i = 0; i < args.Length; i++)
            ptrs[i] = Native.Utf8(args[i]);
        ptrs[args.Length] = null;
        Native.mpv_command(_mpv, ptrs);
        for (var i = 0; i < args.Length; i++)
            Native.Free(ptrs[i]);
    }

    private long? GetLong(string name)
    {
        var n = Native.Utf8(name);
        long v;
        var err = Native.mpv_get_property(_mpv, n, Native.Format.Int64, &v);
        Native.Free(n);
        return err >= 0 ? v : null;
    }

    /// <summary>Espera por um evento (falso se o ficheiro acabou com erro, ou se o tempo passou).</summary>
    private bool WaitFor(Native.EventId id, double seconds)
    {
        var deadline = Environment.TickCount64 + (long)(seconds * 1000);
        while (!_stop)
        {
            var left = (deadline - Environment.TickCount64) / 1000.0;
            if (left <= 0)
                return false;
            var e = Native.mpv_wait_event(_mpv, left);
            if (e->EventId == id)
                return true;
            if (e->EventId is Native.EventId.EndFile or Native.EventId.Shutdown)
                return false;
        }
        return false;
    }

    private bool Open()
    {
        _mpv = Native.mpv_create();
        if (_mpv == IntPtr.Zero)
            return false;
        foreach (var (name, value) in MpvPlayer.SafeDefaults)
            Option(name, value);
        foreach (var (name, value) in new[]
        {
            ("vo", "libmpv"), ("hwdec", "no"), ("aid", "no"), ("sid", "no"), ("pause", "yes"),
            ("keep-open", "always"), ("idle", "yes"), ("config", "no"), ("terminal", "no"),
            ("load-scripts", "no"), ("ytdl", "no"), ("osc", "no"), ("osd-level", "0"),
            ("hr-seek", "no"), ("cache", "no"), ("demuxer-readahead-secs", "0"),
            ("sub-auto", "no"), ("audio-file-auto", "no"), ("vd-lavc-threads", "2"),
            ("vd-lavc-skiploopfilter", "all"), ("sws-scaler", "fast-bilinear"),
            ("input-default-bindings", "no"), ("msg-level", "all=no"),
        })
            Option(name, value);
        if (Native.mpv_initialize(_mpv) < 0)
            return false;

        var api = Native.Utf8("sw");
        var parameters = stackalloc Native.RenderParam[2];
        parameters[0] = new Native.RenderParam(Native.RenderParamType.ApiType, api);
        parameters[1] = new Native.RenderParam(Native.RenderParamType.Invalid, null);
        var err = Native.mpv_render_context_create(out _ctx, _mpv, parameters);
        Native.Free(api);
        if (err < 0)
        {
            _ctx = IntPtr.Zero;
            return false;
        }
        // Só para a libmpv saber que alguém desenha; o desenho faz-se depois de cada salto.
        _onUpdate = _ => { };
        Native.mpv_render_context_set_update_callback(_ctx, Marshal.GetFunctionPointerForDelegate(_onUpdate), IntPtr.Zero);

        Command("loadfile", _path);
        if (!WaitFor(Native.EventId.FileLoaded, 15) || !WaitFor(Native.EventId.PlaybackRestart, 15))
            return false;
        var dw = GetLong("dwidth") ?? 0;
        var dh = GetLong("dheight") ?? 0;
        if (dw <= 0 || dh <= 0)
            return false;
        _w = _width;
        _h = Math.Max(2, (int)Math.Round(_width * (double)dh / dw) & ~1);
        _stride = (_w * 4 + 63) & ~63;
        _buffer = GC.AllocateArray<byte>(_stride * _h, pinned: true);
        return true;
    }

    private ThumbFrame? Grab(double time)
    {
        Command("seek", MpvPlayer.Seconds(Math.Max(0, time)), "absolute+keyframes");
        if (!WaitFor(Native.EventId.PlaybackRestart, 5))
            return null;
        var size = stackalloc int[2] { _w, _h };
        var format = Native.Utf8("bgr0");
        nuint stride = (nuint)_stride;
        fixed (byte* pixels = _buffer)
        {
            var parameters = stackalloc Native.RenderParam[5];
            parameters[0] = new Native.RenderParam(Native.RenderParamType.SwSize, size);
            parameters[1] = new Native.RenderParam(Native.RenderParamType.SwFormat, format);
            parameters[2] = new Native.RenderParam(Native.RenderParamType.SwStride, &stride);
            parameters[3] = new Native.RenderParam(Native.RenderParamType.SwPointer, pixels);
            parameters[4] = new Native.RenderParam(Native.RenderParamType.Invalid, null);
            Native.mpv_render_context_update(_ctx);
            var err = Native.mpv_render_context_render(_ctx, parameters);
            Native.Free(format);
            if (err < 0)
                return null;
        }
        return new ThumbFrame(time, _w, _h, _stride, (byte[])_buffer!.Clone());
    }

    private void Close()
    {
        if (_ctx != IntPtr.Zero)
        {
            Native.mpv_render_context_set_update_callback(_ctx, IntPtr.Zero, IntPtr.Zero);
            Native.mpv_render_context_free(_ctx);
            _ctx = IntPtr.Zero;
        }
        if (_mpv != IntPtr.Zero)
        {
            // Esta thread é a única que lê eventos desta instância: pode destruí-la já.
            Native.mpv_terminate_destroy(_mpv);
            _mpv = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        _stop = true;
        _wake.Set();
        if (_mpv != IntPtr.Zero)
            Native.mpv_wakeup(_mpv);
        if (!_thread.Join(TimeSpan.FromSeconds(3)))
            Log.Write("Miniaturas: a thread não saiu a tempo");
        _wake.Dispose();
    }
}
