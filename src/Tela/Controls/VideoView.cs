using System.Runtime.InteropServices;
using Tela.Mpv;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace Tela.Controls;

/// <summary>
/// Onde o vídeo aparece: a libmpv desenha (pela API de desenho em OpenGL) no framebuffer que o
/// Avalonia dá a este controlo, e o resto da interface desenha-se por cima, sem "buracos" de
/// janelas nativas. Cada imagem nova pede um desenho; não há desenho contínuo.
/// Se não houver OpenGL (o Avalonia a desenhar por software), avisa por <see cref="Unavailable"/>
/// e a janela passa para a <see cref="SoftwareVideoView"/>.
/// </summary>
public sealed class VideoView : OpenGlControlBase
{
    private const int GlColorBufferBit = 0x4000;

    private MpvPlayer? _player;
    private IntPtr _ctx;
    // Os delegados passam para código nativo: guardam-se para o GC não os levar.
    private Native.GetProcAddressFn? _getProc;
    private Native.UpdateCallbackFn? _onUpdate;
    private GlInterface? _gl;
    private int _pending;
    private bool _initialized;
    private bool _unavailable;
    private unsafe delegate* unmanaged<int, int, void> _glBindBuffer;
    // Diagnóstico (TELA_MPV_LOG): quantos desenhos se fazem, de 2 em 2 segundos.
    private static readonly bool CountRenders = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TELA_MPV_LOG"));
    private int _renders;
    private long _rendersSince;
    private unsafe delegate* unmanaged<int, int, void> _glPixelStorei;

    /// <summary>A libmpv falhou a criar o contexto de desenho (ex.: OpenGL antigo demais).</summary>
    public event Action<string>? Failed;
    /// <summary>
    /// O contexto de desenho existe. Só a partir daqui se podem abrir ficheiros: sem ele, a libmpv
    /// desiste do vídeo e toca só o som.
    /// </summary>
    public event Action? Ready;
    /// <summary>Não há OpenGL para o vídeo: é preciso desenhar por software.</summary>
    public event Action<string>? Unavailable;

    public MpvPlayer? Player
    {
        get => _player;
        set
        {
            _player = value;
            RequestNextFrameRendering();
        }
    }

    public bool IsReady => _ctx != IntPtr.Zero;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _ = CheckOpenGlAsync();
    }

    /// <summary>
    /// O Avalonia só dá OpenGL a este controlo se desenhar a janela pela placa gráfica (no Linux
    /// recusa o llvmpipe, o OpenGL por software das máquinas virtuais). Sem isso, nunca chega a
    /// haver OnOpenGlInit: vê-se aqui e avisa-se.
    /// </summary>
    /// <summary>Pedido nas definições (ou por TELA_VIDEO=software): nem se tenta o OpenGL.</summary>
    public bool ForceSoftware { get; set; }

    private async Task CheckOpenGlAsync()
    {
        if (ForceSoftware || Environment.GetEnvironmentVariable("TELA_VIDEO") == "software")
        {
            Report(ForceSoftware ? "definições" : "TELA_VIDEO=software");
            return;
        }
        var visual = ElementComposition.GetElementVisual(this);
        if (visual is null)
            return;
        var interop = await visual.Compositor.TryGetCompositionGpuInterop();
        if (interop is null)
        {
            Report("a janela é desenhada sem placa gráfica");
            return;
        }
        // Há sistemas que passam esta verificação e mesmo assim nunca dão o contexto.
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (!_initialized && VisualRoot is not null)
            Report("o contexto OpenGL não arrancou");
    }

    private void Report(string why)
    {
        if (_unavailable)
            return;
        _unavailable = true;
        Log.Write("Desenho do vídeo: sem OpenGL (" + why + "); passa a software");
        Dispatcher.UIThread.Post(() => Unavailable?.Invoke(why));
    }

    protected override unsafe void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        _gl = gl;
        _initialized = true;
        // A libmpv só aceita um contexto de desenho: se já se passou para software, fica para ele.
        if (_player is null || _unavailable)
            return;
        _getProc = (_, name) => _gl.GetProcAddress(Marshal.PtrToStringAnsi(name) ?? "");
        _glBindBuffer = (delegate* unmanaged<int, int, void>)gl.GetProcAddress("glBindBuffer");
        _glPixelStorei = (delegate* unmanaged<int, int, void>)gl.GetProcAddress("glPixelStorei");
        var init = new Native.OpenGlInitParams { GetProcAddress = Marshal.GetFunctionPointerForDelegate(_getProc) };
        var api = Native.Utf8("opengl");
        var parameters = stackalloc Native.RenderParam[3];
        parameters[0] = new Native.RenderParam(Native.RenderParamType.ApiType, api);
        parameters[1] = new Native.RenderParam(Native.RenderParamType.OpenGlInitParams, &init);
        parameters[2] = new Native.RenderParam(Native.RenderParamType.Invalid, null);
        var err = Native.mpv_render_context_create(out _ctx, _player.Handle, parameters);
        Native.Free(api);
        if (err < 0)
        {
            _ctx = IntPtr.Zero;
            var message = Native.ErrorText(err);
            Log.Write($"Desenho do vídeo: mpv_render_context_create falhou ({message}); OpenGL {GlVersion.Type} {GlVersion.Major}.{GlVersion.Minor}");
            Dispatcher.UIThread.Post(() => Failed?.Invoke(message));
            Report(message);
            return;
        }
        Log.Write($"Desenho do vídeo: OpenGL {GlVersion.Type} {GlVersion.Major}.{GlVersion.Minor}");
        // Chamado de threads da libmpv: aqui só se agenda um desenho (nada de chamadas à libmpv).
        _onUpdate = _ =>
        {
            if (Interlocked.Exchange(ref _pending, 1) == 0)
                Dispatcher.UIThread.Post(() =>
                {
                    Volatile.Write(ref _pending, 0);
                    RequestNextFrameRendering();
                }, DispatcherPriority.Render);
        };
        Native.mpv_render_context_set_update_callback(_ctx, Marshal.GetFunctionPointerForDelegate(_onUpdate), IntPtr.Zero);
        Dispatcher.UIThread.Post(() => Ready?.Invoke());
    }

    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_ctx == IntPtr.Zero)
        {
            gl.ClearColor(0, 0, 0, 1);
            gl.Clear(GlColorBufferBit);
            return;
        }
        var size = PixelSize.FromSize(Bounds.Size, VisualRoot?.RenderScaling ?? 1);
        if (size.Width <= 0 || size.Height <= 0)
            return;
        var fbo = new Native.OpenGlFbo { Fbo = fb, W = size.Width, H = size.Height, InternalFormat = 0 };
        var flip = 1;
        // Sem esperar pela hora da imagem: quem marca o ritmo é a libmpv, com o pedido de desenho.
        var block = 0;
        var parameters = stackalloc Native.RenderParam[4];
        parameters[0] = new Native.RenderParam(Native.RenderParamType.OpenGlFbo, &fbo);
        parameters[1] = new Native.RenderParam(Native.RenderParamType.FlipY, &flip);
        parameters[2] = new Native.RenderParam(Native.RenderParamType.BlockForTargetTime, &block);
        parameters[3] = new Native.RenderParam(Native.RenderParamType.Invalid, null);
        ResetUploadState();
        if (CountRenders)
        {
            _renders++;
            var now = Environment.TickCount64;
            if (now - _rendersSince >= 2000)
            {
                Log.Write($"Desenho do vídeo: {_renders} desenhos em {(now - _rendersSince) / 1000.0:0.0} s, {size.Width}×{size.Height}, fb {fb}");
                _renders = 0;
                _rendersSince = now;
            }
        }
        Native.mpv_render_context_update(_ctx);
        Native.mpv_render_context_render(_ctx, parameters);
        Native.mpv_render_context_report_swap(_ctx);
    }

    /// <summary>
    /// O contexto pode vir com estado deixado pelo desenho da interface (Skia), como um buffer de
    /// píxeis ligado: aí a libmpv envia as imagens do vídeo para o sítio errado e o vídeo fica
    /// preto (só as legendas aparecem). Repõe-se o essencial antes de cada desenho.
    /// </summary>
    private unsafe void ResetUploadState()
    {
        if (_glBindBuffer != null)
        {
            _glBindBuffer(0x88EC, 0); // GL_PIXEL_UNPACK_BUFFER
            _glBindBuffer(0x88EB, 0); // GL_PIXEL_PACK_BUFFER
        }
        if (_glPixelStorei != null)
        {
            _glPixelStorei(0x0CF5, 4); // GL_UNPACK_ALIGNMENT
            _glPixelStorei(0x0CF2, 0); // GL_UNPACK_ROW_LENGTH
            _glPixelStorei(0x0CF3, 0); // GL_UNPACK_SKIP_PIXELS
            _glPixelStorei(0x0CF4, 0); // GL_UNPACK_SKIP_ROWS
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        FreeContext();
        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlLost()
    {
        // O contexto já não existe: não se pode chamar o OpenGL, mas o contexto da libmpv tem de
        // ser libertado antes de destruir o leitor.
        FreeContext();
        base.OnOpenGlLost();
    }

    private void FreeContext()
    {
        if (_ctx == IntPtr.Zero)
            return;
        Native.mpv_render_context_set_update_callback(_ctx, IntPtr.Zero, IntPtr.Zero);
        Native.mpv_render_context_free(_ctx);
        _ctx = IntPtr.Zero;
    }
}
