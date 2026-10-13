using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tela.Library;

/// <summary>O que se lembra de um vídeo: onde ficou, que faixas se escolheram, os atrasos.</summary>
public sealed class HistoryEntry
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public double Position { get; set; }
    public double Duration { get; set; }
    public DateTime Updated { get; set; }
    public bool Finished { get; set; }
    /// <summary>Faixa de áudio escolhida à mão (id da libmpv), ou nulo.</summary>
    public string? Aid { get; set; }
    /// <summary>Legendas escolhidas à mão (id, ou "no"), ou nulo.</summary>
    public string? Sid { get; set; }
    public double SubDelay { get; set; }
    public double AudioDelay { get; set; }
    /// <summary>A imagem do "continuar a ver" (nome do ficheiro na pasta thumbs).</summary>
    public string? Thumb { get; set; }

    public double Remaining => Math.Max(0, Duration - Position);
    public double Progress => Duration > 0 ? Math.Clamp(Position / Duration, 0, 1) : 0;
}

/// <summary>
/// A memória da Tela: onde ficou cada vídeo e o "continuar a ver" do ecrã inicial.
/// Fica em history.json, na pasta das definições; as imagens, em thumbs/.
/// </summary>
public sealed class History
{
    private const int MaxEntries = 300;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _file;
    private readonly List<HistoryEntry> _entries = [];

    public History(string folder)
    {
        Folder = folder;
        ThumbFolder = System.IO.Path.Combine(folder, "thumbs");
        _file = System.IO.Path.Combine(folder, "history.json");
    }

    public string Folder { get; }
    public string ThumbFolder { get; }
    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public static History Load(string folder)
    {
        var h = new History(folder);
        try
        {
            if (File.Exists(h._file))
                h._entries.AddRange(JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(h._file), Options)?
                    .Where(e => !string.IsNullOrEmpty(e.Path)) ?? []);
        }
        catch (Exception ex)
        {
            Log.Write("Falha a ler o histórico: " + ex.Message);
        }
        return h;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, Options));
            File.Move(tmp, _file, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Write("Falha a gravar o histórico: " + ex.Message);
        }
    }

    private static bool Same(string a, string b) =>
        string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public HistoryEntry? Find(string path) => _entries.FirstOrDefault(e => Same(e.Path, path));

    /// <summary>
    /// Já se viu até ao fim? Conta como visto quem chegou aos créditos: os últimos 5% (entre 5 s
    /// e 2 min), para um filme não ficar "a meio" por causa do genérico.
    /// </summary>
    public static bool IsFinished(double position, double duration)
    {
        if (duration <= 0 || !double.IsFinite(duration))
            return false;
        var tail = Math.Max(5, Math.Min(120, duration * 0.05));
        return duration - position <= tail;
    }

    /// <summary>De onde continuar: um pouco antes de onde ficou, para se perceber onde se estava.</summary>
    public static double? ResumePoint(HistoryEntry? entry)
    {
        if (entry is null || entry.Finished || entry.Position < 10 || !double.IsFinite(entry.Position))
            return null;
        if (entry.Duration > 0 && IsFinished(entry.Position, entry.Duration))
            return null;
        return Math.Max(0, entry.Position - 3);
    }

    /// <summary>Atualiza (ou cria) a entrada de um vídeo e põe-na à frente.</summary>
    public HistoryEntry Update(string path, string title, double position, double duration)
    {
        var e = Find(path);
        if (e is null)
        {
            e = new HistoryEntry { Path = path };
            _entries.Insert(0, e);
        }
        else
        {
            _entries.Remove(e);
            _entries.Insert(0, e);
        }
        e.Title = title;
        if (double.IsFinite(duration) && duration > 0)
            e.Duration = duration;
        if (double.IsFinite(position))
        {
            e.Finished = IsFinished(position, e.Duration);
            e.Position = e.Finished ? 0 : Math.Max(0, position);
        }
        e.Updated = DateTime.UtcNow;
        Trim();
        return e;
    }

    /// <summary>O "continuar a ver": o que ficou a meio, do mais recente para o mais antigo.</summary>
    public List<HistoryEntry> ContinueWatching(int max) =>
        _entries.Where(e => ResumePoint(e) is not null && File.Exists(e.Path))
            .OrderByDescending(e => e.Updated)
            .Take(max)
            .ToList();

    public void Remove(string path)
    {
        var e = Find(path);
        if (e is null)
            return;
        _entries.Remove(e);
        DeleteThumb(e);
    }

    /// <summary>Esquece onde ficou (o vídeo passa a abrir do princípio), mas guarda as faixas escolhidas.</summary>
    public void ForgetPosition(string path)
    {
        if (Find(path) is { } e)
        {
            e.Position = 0;
            e.Finished = false;
            DeleteThumb(e);
        }
    }

    public string ThumbPath(HistoryEntry e) => System.IO.Path.Combine(ThumbFolder, e.Thumb ?? ThumbName(e.Path));

    public static string ThumbName(string path) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16].ToLowerInvariant() + ".png";

    private void DeleteThumb(HistoryEntry e)
    {
        if (e.Thumb is null)
            return;
        try
        {
            File.Delete(System.IO.Path.Combine(ThumbFolder, e.Thumb));
        }
        catch { }
        e.Thumb = null;
    }

    private void Trim()
    {
        while (_entries.Count > MaxEntries)
        {
            var last = _entries[^1];
            _entries.RemoveAt(_entries.Count - 1);
            DeleteThumb(last);
        }
    }
}
