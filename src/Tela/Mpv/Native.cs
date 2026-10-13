using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Tela.Mpv;

/// <summary>
/// Ligação mínima à libmpv (API 2.x): o cliente (client.h) e a API de desenho (render.h), só o
/// que a Tela usa. No Windows a biblioteca é a libmpv-2.dll ao lado do executável; no Linux, a
/// libmpv.so.2 da distribuição.
/// </summary>
public static unsafe class Native
{
    private const string Lib = "mpv";

    static Native()
    {
        NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, Resolve);
    }

    private static IntPtr _handle;

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? path)
    {
        if (name != Lib)
            return IntPtr.Zero;
        if (_handle != IntPtr.Zero)
            return _handle;
        var names = OperatingSystem.IsWindows()
            ? new[] { Path.Combine(AppContext.BaseDirectory, "libmpv-2.dll"), "libmpv-2.dll", "mpv-2.dll", "mpv-1.dll" }
            : OperatingSystem.IsMacOS()
                ? new[] { "libmpv.2.dylib", "libmpv.dylib" }
                : new[] { "libmpv.so.2", "libmpv.so" };
        foreach (var candidate in names)
            if (NativeLibrary.TryLoad(candidate, assembly, path, out _handle))
                return _handle;
        return IntPtr.Zero;
    }

    /// <summary>Verdadeiro se a libmpv existe e tem a API 2.x (a única que a Tela sabe usar).</summary>
    public static bool IsAvailable(out string? error)
    {
        try
        {
            var version = mpv_client_api_version();
            if ((version >> 16) < 2)
            {
                error = $"libmpv {version >> 16}.{version & 0xffff} (2.0 or newer needed)";
                return false;
            }
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            error = ex.Message;
            return false;
        }
    }

    // ── Cliente ───────────────────────────────────────────────────────────────

    public enum Format
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5,
        Node = 6,
    }

    public enum EventId
    {
        None = 0,
        Shutdown = 1,
        LogMessage = 2,
        GetPropertyReply = 3,
        SetPropertyReply = 4,
        CommandReply = 5,
        StartFile = 6,
        EndFile = 7,
        FileLoaded = 8,
        ClientMessage = 16,
        VideoReconfig = 17,
        AudioReconfig = 18,
        Seek = 20,
        PlaybackRestart = 21,
        PropertyChange = 22,
        QueueOverflow = 24,
        Hook = 25,
    }

    public enum EndFileReason
    {
        Eof = 0,
        Stop = 2,
        Quit = 3,
        Error = 4,
        Redirect = 5,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Event
    {
        public EventId EventId;
        public int Error;
        public ulong ReplyUserdata;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventProperty
    {
        public IntPtr Name;
        public Format Format;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventEndFile
    {
        public EndFileReason Reason;
        public int Error;
        public long PlaylistEntryId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EventLogMessage
    {
        public IntPtr Prefix;
        public IntPtr Level;
        public IntPtr Text;
        public int LogLevel;
    }

    [DllImport(Lib)] public static extern ulong mpv_client_api_version();
    [DllImport(Lib)] public static extern IntPtr mpv_create();
    [DllImport(Lib)] public static extern int mpv_initialize(IntPtr ctx);
    [DllImport(Lib)] public static extern void mpv_terminate_destroy(IntPtr ctx);
    [DllImport(Lib)] public static extern IntPtr mpv_error_string(int error);
    [DllImport(Lib)] public static extern void mpv_free(IntPtr data);
    [DllImport(Lib)] public static extern int mpv_set_option_string(IntPtr ctx, byte* name, byte* data);
    [DllImport(Lib)] public static extern int mpv_set_property_string(IntPtr ctx, byte* name, byte* data);
    [DllImport(Lib)] public static extern int mpv_set_property(IntPtr ctx, byte* name, Format format, void* data);
    [DllImport(Lib)] public static extern int mpv_get_property(IntPtr ctx, byte* name, Format format, void* data);
    [DllImport(Lib)] public static extern IntPtr mpv_get_property_string(IntPtr ctx, byte* name);
    [DllImport(Lib)] public static extern int mpv_command(IntPtr ctx, byte** args);
    [DllImport(Lib)] public static extern int mpv_command_async(IntPtr ctx, ulong replyUserdata, byte** args);
    [DllImport(Lib)] public static extern int mpv_observe_property(IntPtr ctx, ulong replyUserdata, byte* name, Format format);
    [DllImport(Lib)] public static extern int mpv_request_log_messages(IntPtr ctx, byte* minLevel);
    [DllImport(Lib)] public static extern Event* mpv_wait_event(IntPtr ctx, double timeout);
    [DllImport(Lib)] public static extern void mpv_wakeup(IntPtr ctx);

    // ── Desenho (render.h / render_gl.h) ──────────────────────────────────────

    public enum RenderParamType
    {
        Invalid = 0,
        ApiType = 1,
        OpenGlInitParams = 2,
        OpenGlFbo = 3,
        FlipY = 4,
        Depth = 5,
        AdvancedControl = 10,
        BlockForTargetTime = 12,
        SkipRendering = 13,
        SwSize = 17,
        SwFormat = 18,
        SwStride = 19,
        SwPointer = 20,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RenderParam
    {
        public RenderParamType Type;
        public void* Data;

        public RenderParam(RenderParamType type, void* data)
        {
            Type = type;
            Data = data;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OpenGlInitParams
    {
        public IntPtr GetProcAddress;
        public IntPtr GetProcAddressCtx;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OpenGlFbo
    {
        public int Fbo;
        public int W;
        public int H;
        public int InternalFormat;
    }

    public const ulong RenderUpdateFrame = 1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr GetProcAddressFn(IntPtr ctx, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void UpdateCallbackFn(IntPtr ctx);

    [DllImport(Lib)] public static extern int mpv_render_context_create(out IntPtr res, IntPtr mpv, RenderParam* parameters);
    [DllImport(Lib)] public static extern void mpv_render_context_set_update_callback(IntPtr ctx, IntPtr callback, IntPtr callbackCtx);
    [DllImport(Lib)] public static extern ulong mpv_render_context_update(IntPtr ctx);
    [DllImport(Lib)] public static extern int mpv_render_context_render(IntPtr ctx, RenderParam* parameters);
    [DllImport(Lib)] public static extern void mpv_render_context_report_swap(IntPtr ctx);
    [DllImport(Lib)] public static extern void mpv_render_context_free(IntPtr ctx);

    // ── Texto UTF-8 ───────────────────────────────────────────────────────────

    /// <summary>Uma cadeia UTF-8 terminada em zero, em memória não gerida (libertar com Free).</summary>
    public static byte* Utf8(string s)
    {
        var count = Encoding.UTF8.GetByteCount(s);
        var p = (byte*)NativeMemory.Alloc((nuint)count + 1);
        fixed (char* c = s)
            Encoding.UTF8.GetBytes(c, s.Length, p, count);
        p[count] = 0;
        return p;
    }

    public static void Free(byte* p) => NativeMemory.Free(p);

    public static string? FromUtf8(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

    public static string ErrorText(int error) => FromUtf8(mpv_error_string(error)) ?? error.ToString();
}
