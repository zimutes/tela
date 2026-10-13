namespace Tela.Library;

/// <summary>Que ficheiros a Tela abre e como se juntam numa lista (a pasta, pela ordem natural).</summary>
public static class MediaFiles
{
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".m4v", ".mov", ".avi", ".webm", ".wmv", ".asf", ".flv", ".f4v", ".mpg", ".mpeg",
        ".mpe", ".m2v", ".m2ts", ".mts", ".ts", ".tp", ".vob", ".ogv", ".ogm", ".3gp", ".3g2", ".divx",
        ".xvid", ".rm", ".rmvb", ".mxf", ".dv", ".y4m", ".ivf", ".hevc", ".h264", ".h265", ".264", ".265",
        ".av1", ".mk3d", ".nut", ".gif", ".apng",
    };

    public static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".mp2", ".flac", ".ogg", ".oga", ".opus", ".m4a", ".m4b", ".aac", ".wav", ".w64", ".wma",
        ".ac3", ".eac3", ".dts", ".mka", ".ape", ".wv", ".aif", ".aiff", ".caf", ".tta", ".mpc", ".dsf",
    };

    public static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx", ".sup", ".smi", ".sami", ".lrc", ".ttml", ".dfxp",
    };

    public static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    public static bool IsAudio(string path) => AudioExtensions.Contains(Path.GetExtension(path));

    public static bool IsPlayable(string path) => IsVideo(path) || IsAudio(path);

    public static bool IsSubtitle(string path) => SubtitleExtensions.Contains(Path.GetExtension(path));

    /// <summary>Até onde se vai numa pasta: o bastante para uma série inteira, não para um disco.</summary>
    private const int MaxItems = 5000;

    /// <summary>
    /// Os vídeos da pasta de um ficheiro, pela ordem natural (Ep2 antes de Ep10), para o
    /// "seguinte" e o "anterior" funcionarem como numa série. Se o ficheiro é de som, os sons.
    /// </summary>
    public static List<string> Siblings(string file)
    {
        var folder = Path.GetDirectoryName(file);
        var same = IsAudio(file) ? (Func<string, bool>)IsAudio : IsVideo;
        var list = new List<string>();
        try
        {
            if (folder is not null)
                list.AddRange(Directory.EnumerateFiles(folder).Where(f => same(f) && !IsHidden(f)).Take(MaxItems));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Pasta {folder}: {ex.Message}");
        }
        if (!list.Any(f => SamePath(f, file)))
            list.Add(file);
        list.Sort(NaturalComparer.Instance);
        return list;
    }

    /// <summary>Os vídeos de uma pasta e das subpastas (Temporada 1, Temporada 2…), pela ordem natural.</summary>
    public static List<string> FromFolder(string folder)
    {
        var list = new List<string>();
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
            list.AddRange(Directory.EnumerateFiles(folder, "*", options).Where(IsVideo).Take(MaxItems));
            // Uma pasta só com música também se toca.
            if (list.Count == 0)
                list.AddRange(Directory.EnumerateFiles(folder, "*", options).Where(IsAudio).Take(MaxItems));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Pasta {folder}: {ex.Message}");
        }
        list.Sort((a, b) => NaturalComparer.Instance.Compare(Path.GetRelativePath(folder, a), Path.GetRelativePath(folder, b)));
        return list;
    }

    /// <summary>
    /// O que chegou (largado, escolhido, da linha de comandos) separado em lista para tocar e
    /// legendas. Um só vídeo traz a pasta com ele; vários ficheiros ficam só esses; pastas abrem-se.
    /// </summary>
    public static (List<string> Items, int Start, List<string> Subtitles) Expand(IReadOnlyList<string> paths)
    {
        var subtitles = paths.Where(p => File.Exists(p) && IsSubtitle(p)).ToList();
        var files = paths.Where(p => File.Exists(p) && IsPlayable(p)).ToList();
        var folders = paths.Where(Directory.Exists).ToList();

        if (files.Count == 1 && folders.Count == 0)
        {
            var siblings = Siblings(files[0]);
            return (siblings, Math.Max(0, siblings.FindIndex(f => SamePath(f, files[0]))), subtitles);
        }
        var items = new List<string>(files);
        items.Sort(NaturalComparer.Instance);
        foreach (var folder in folders.OrderBy(f => f, NaturalComparer.Instance))
            items.AddRange(FromFolder(folder));
        // Ficheiros de tipos que não conhecemos, escolhidos à mão: tenta-se na mesma (a libmpv lê muita coisa).
        if (items.Count == 0)
            items.AddRange(paths.Where(p => File.Exists(p) && !IsSubtitle(p)));
        return (items, 0, subtitles);
    }

    public static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsHidden(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith('.'))
            return true;
        try
        {
            return OperatingSystem.IsWindows() && (File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Nome para mostrar quando o ficheiro não tem título: o nome sem extensão, com pontos e traços como espaços.</summary>
    public static string DisplayName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        // "Show.S01E02.1080p" lê-se melhor como "Show S01E02 1080p"; nomes com espaços ficam como estão.
        if (!name.Contains(' ') && name.Any(c => c is '.' or '_'))
            name = name.Replace('.', ' ').Replace('_', ' ');
        return name;
    }
}

/// <summary>Compara como as pessoas: "Ep2" antes de "Ep10".</summary>
public sealed class NaturalComparer : IComparer<string?>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? a, string? b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                var si = i;
                var sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;
                var na = a.AsSpan(si, i - si).TrimStart('0');
                var nb = b.AsSpan(sj, j - sj).TrimStart('0');
                if (na.Length != nb.Length)
                    return na.Length.CompareTo(nb.Length);
                var c = na.CompareTo(nb, StringComparison.Ordinal);
                if (c != 0)
                    return c;
            }
            else
            {
                var c = string.Compare(a, i, b, j, 1, StringComparison.CurrentCultureIgnoreCase);
                if (c != 0)
                    return c;
                i++;
                j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
