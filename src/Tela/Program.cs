using Tela.Platform;
using Avalonia;

namespace Tela;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest")
            return SelfTest.Run(args.Skip(1).ToArray());

        // Já há uma Tela aberta (ou a abrir): entrega-lhe os ficheiros e sai.
        if (SingleInstance.HandOff(args))
            return 0;

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Erro: " + e.ExceptionObject);
        App.StartupArgs = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>().UsePlatformDetect();
        // TELA_GLX_ANY=1 (testes): aceita o OpenGL por software (llvmpipe), que o Avalonia recusa.
        if (Environment.GetEnvironmentVariable("TELA_GLX_ANY") == "1")
            builder = builder.With(new X11PlatformOptions { GlxRendererBlacklist = [] });
        // TELA_AVALONIA_LOG=1: os avisos do Avalonia (OpenGL, janela) vão para o terminal.
        return Environment.GetEnvironmentVariable("TELA_AVALONIA_LOG") == "1"
            ? builder.LogToTextWriter(Console.Error, Avalonia.Logging.LogEventLevel.Debug)
            : builder.LogToTrace();
    }
}
