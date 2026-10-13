using System.Runtime.InteropServices;
using Tela.Mpv;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Tela.Controls;

/// <summary>
/// O vídeo sem OpenGL (máquinas virtuais, ambientes remotos, controladores avariados): a libmpv
/// desenha cada imagem em memória (API de desenho por software), numa thread própria, para um
/// de três bitmaps que se vão alternando; a interface só mostra o último pronto. Gasta mais
/// processador do que o OpenGL, por isso só se usa quando ele falta.
/// </summary>
public sealed unsafe class SoftwareVideoView : Control
{
    // Acima disto a imagem desenha-se mais pequena e o Avalonia estica-a: o processador agradece.
    private const int RenderMaxWidth = 1920;
    private const int RenderMaxHeight = 1200;

    private MpvPlayer? _player;
    private IntPtr _ctx;
    private Native.UpdateCallbackFn? _onUpdate;
    private Thread? _thread;
    private readonly AutoResetEvent _wake = new(false);
    private volatile bool _stop;
    private readonly WriteableBitmap?[] _buffers = new WriteableBitmap?[3];
    private int _next;
    private WriteableBitmap? _front;
    private PixelSize _target;
    private int _posted;

    /// <summary>O contexto de desenho existe: já se podem abrir ficheiros.</summary>
    public event Action? Ready;

    public bool IsRunning => _ctx != IntPtr.Zero;

    public bool Start(MpvPlayer player)
    {
        if (_ctx != IntPtr.Zero)
            return true;
        _player = player;
        var api = Native.Utf8("sw");
        var parameters = stackalloc Native.RenderParam[2];
        parameters[0] = new Native.RenderParam(Native.RenderParamType.ApiType, api);
        parameters[1] = new Native.RenderParam(Native.RenderParamType.Invalid, null);
        var err = Native.mpv_render_context_create(out _ctx, player.Handle, parameters);
        Native.Free(api);
        if (err < 0)
        {
            _ctx = IntPtr.Zero;
            Log.Write("Vídeo por software: mpv_render_context_create falhou (" + Native.ErrorText(err) + ")");
            return false;
        }
        Log.Write("Desenho do vídeo: por software (sem OpenGL)");
        _onUpdate = _ => _wake.Set();
        Native.mpv_render_context_set_update_callback(_ctx, Marshal.GetFunctionPointerForDelegate(_onUpdate), IntPtr.Zero);
        UpdateTarget();
        _thread = new Thread(Loop) { IsBackground = true, Name = "mpv-desenho-software" };
        _thread.Start();
        Dispatcher.UIThread.Post(() => Ready?.Invoke());
        return true;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateTarget();
        _wake.Set();
    }

    private void UpdateTarget()
    {
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var w = Math.Max(2, (int)(Bounds.Width * scale));
        var h = Math.Max(2, (int)(Bounds.Height * scale));
        var shrink = Math.Min(1, Math.Min((double)RenderMaxWidth / w, (double)RenderMaxHeight / h));
        _target = new PixelSize(Math.Max(2, (int)(w * shrink)), Math.Max(2, (int)(h * shrink)));
    }

    private void Loop()
    {
        var size = PixelSize.Empty;
        var dims = stackalloc int[2];
        var parameters = stackalloc Native.RenderParam[5];
        var format = Native.Utf8("bgr0");
        nuint stride = 0;
        while (!_stop)
        {
            _wake.WaitOne();
            if (_stop)
                break;
            var flags = Native.mpv_render_context_update(_ctx);
            var target = _target;
            var resized = target != size;
            if ((flags & Native.RenderUpdateFrame) == 0 && !resized)
                continue;
            size = target;
            var bitmap = _buffers[_next];
            if (bitmap is null || bitmap.PixelSize != size)
            {
                bitmap?.Dispose();
                bitmap = _buffers[_next] = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            }
            using (var fb = bitmap.Lock())
            {
                dims[0] = size.Width;
                dims[1] = size.Height;
                stride = (nuint)fb.RowBytes;
                parameters[0] = new Native.RenderParam(Native.RenderParamType.SwSize, dims);
                parameters[1] = new Native.RenderParam(Native.RenderParamType.SwFormat, format);
                parameters[2] = new Native.RenderParam(Native.RenderParamType.SwStride, &stride);
                parameters[3] = new Native.RenderParam(Native.RenderParamType.SwPointer, (void*)fb.Address);
                parameters[4] = new Native.RenderParam(Native.RenderParamType.Invalid, null);
                Native.mpv_render_context_render(_ctx, parameters);
                // O "0" do bgr0 vem por preencher (muitas vezes a 0, que seria transparente): fica opaco.
                Opaque((uint*)fb.Address, fb.RowBytes / 4 * size.Height);
            }
            Volatile.Write(ref _front, bitmap);
            _next = (_next + 1) % _buffers.Length;
            if (Interlocked.Exchange(ref _posted, 1) == 0)
                Dispatcher.UIThread.Post(() =>
                {
                    Volatile.Write(ref _posted, 0);
                    InvalidateVisual();
                }, DispatcherPriority.Render);
        }
        Native.Free(format);
    }

    private static void Opaque(uint* pixels, int count)
    {
        var span = new Span<uint>(pixels, count);
        var vectors = System.Runtime.InteropServices.MemoryMarshal.Cast<uint, System.Numerics.Vector<uint>>(span);
        var alpha = new System.Numerics.Vector<uint>(0xFF000000);
        for (var i = 0; i < vectors.Length; i++)
            vectors[i] |= alpha;
        for (var i = vectors.Length * System.Numerics.Vector<uint>.Count; i < span.Length; i++)
            span[i] |= 0xFF000000;
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Black, rect);
        if (Volatile.Read(ref _front) is { } bitmap)
            context.DrawImage(bitmap, new Rect(bitmap.Size), rect);
    }

    /// <summary>Liberta o contexto de desenho (tem de ser antes de destruir o leitor).</summary>
    public void Stop()
    {
        if (_ctx == IntPtr.Zero)
            return;
        _stop = true;
        _wake.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        Native.mpv_render_context_set_update_callback(_ctx, IntPtr.Zero, IntPtr.Zero);
        Native.mpv_render_context_free(_ctx);
        _ctx = IntPtr.Zero;
    }
}
