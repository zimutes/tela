using System.Text.Json;

namespace Tela;

/// <summary>Escolhas que ficam de uma vez para a outra.</summary>
public sealed class AppSettings
{
    /// <summary>"en" (por omissão) ou "pt".</summary>
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "purple";
    /// <summary>Volume da libmpv, de 0 a 150 (100 = original).</summary>
    public double Volume { get; set; } = 100;
    public bool Muted { get; set; }
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public double WindowWidth { get; set; } = 960;
    public double WindowHeight { get; set; } = 600;
    public bool Maximized { get; set; }
    /// <summary>No fim de um vídeo, passar ao seguinte da pasta (com contagem decrescente).</summary>
    public bool AutoplayNext { get; set; } = true;
    /// <summary>Continuar cada vídeo onde ficou.</summary>
    public bool Resume { get; set; } = true;
    /// <summary>Ajustar a janela à forma do vídeo ao abrir (sem barras pretas).</summary>
    public bool FitWindow { get; set; } = true;
    public bool AlwaysOnTop { get; set; }
    /// <summary>Clique no vídeo põe em pausa (duplo clique: ecrã inteiro).</summary>
    public bool ClickToPause { get; set; } = true;
    /// <summary>Tamanho das legendas (1 = normal).</summary>
    public double SubScale { get; set; } = 1;
    /// <summary>Legendas com caixa escura por trás (lêem-se melhor em cima de imagens claras).</summary>
    public bool SubBackground { get; set; }
    /// <summary>Línguas preferidas das legendas, por ordem, em códigos da libmpv (ex.: "pt,por,en,eng").</summary>
    public string SubLanguages { get; set; } = "";
    public string AudioLanguages { get; set; } = "";
    public bool HardwareDecoding { get; set; } = true;
    /// <summary>Desenhar o vídeo por software mesmo havendo OpenGL (para controladores gráficos com problemas).</summary>
    public bool SoftwareVideo { get; set; }
    /// <summary>Nivelar o volume (falas e explosões mais perto umas das outras).</summary>
    public bool Normalize { get; set; }
    /// <summary>Saída de som da libmpv ("auto" = a do sistema).</summary>
    public string AudioDevice { get; set; } = "auto";
    public string LastFolder { get; set; } = "";
    /// <summary>Vazio: Imagens/Tela.</summary>
    public string ScreenshotFolder { get; set; } = "";
    public bool TimeRemaining { get; set; }
}

/// <summary>
/// Guarda as definições em %APPDATA%\Tela (Windows) ou ~/.config/tela (Linux).
/// A variável TELA_DATA permite usar outra pasta (testes, modo portátil).
/// </summary>
public static class SettingsStore
{
    public static readonly string Folder =
        Environment.GetEnvironmentVariable("TELA_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                OperatingSystem.IsWindows() ? "Tela" : "tela");

    public static readonly string FilePath = Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
                Sanitize(s);
                return s;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Falha a ler as definições: " + ex.Message);
        }
        return new AppSettings();
    }

    internal static void Sanitize(AppSettings s)
    {
        static double Fix(double v, double fallback, double min, double max) =>
            Math.Clamp(double.IsFinite(v) ? v : fallback, min, max);
        s.Volume = Fix(s.Volume, 100, 0, 150);
        s.SubScale = Fix(s.SubScale, 1, 0.5, 3);
        s.WindowWidth = Fix(s.WindowWidth, 960, 320, 8000);
        s.WindowHeight = Fix(s.WindowHeight, 600, 180, 8000);
        s.Language ??= "en";
        s.Theme ??= "purple";
        s.SubLanguages ??= "";
        s.AudioLanguages ??= "";
        s.AudioDevice = string.IsNullOrWhiteSpace(s.AudioDevice) ? "auto" : s.AudioDevice;
        s.LastFolder ??= "";
        s.ScreenshotFolder ??= "";
    }

    private static string? _lastSaved;

    /// <summary>Grava só se mudou alguma coisa desde a última vez (é chamado de tempos a tempos).</summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, Options);
            if (json == _lastSaved && File.Exists(FilePath))
                return;
            Directory.CreateDirectory(Folder);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, FilePath, overwrite: true);
            _lastSaved = json;
        }
        catch (Exception ex)
        {
            Log.Write("Falha a gravar as definições: " + ex.Message);
        }
    }
}

public static class Log
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                var path = Path.Combine(SettingsStore.Folder, "log.txt");
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024)
                    File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
