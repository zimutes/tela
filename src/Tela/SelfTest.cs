using System.Diagnostics;
using Tela.Library;
using Tela.Mpv;

namespace Tela;

/// <summary>
/// "Tela --selftest ficheiro…": passa os ficheiros pela libmpv sem janela (sem imagem nem som),
/// e confirma que abrem, que a posição anda, que o salto vai ao sítio, que as faixas e os
/// capítulos se leem e que as miniaturas saem. Serve para testar numa máquina sem ecrã.
/// </summary>
internal static class SelfTest
{
    public static int Run(string[] files)
    {
        if (!Native.IsAvailable(out var error))
        {
            Console.WriteLine($"libmpv: não encontrada ({error})");
            return 2;
        }
        if (files.Length == 0)
        {
            Console.WriteLine("Uso: Tela --selftest ficheiro…");
            return 2;
        }
        var failures = 0;
        void Check(bool ok, string what)
        {
            if (!ok)
                failures++;
            Console.WriteLine($"  {(ok ? "ok   " : "FALHA")} {what}");
        }

        var (queue, start, _) = MediaFiles.Expand(files);
        Console.WriteLine($"lista: {queue.Count} item(s), começa em {start}: {string.Join(" | ", queue.Select(Path.GetFileName))}");

        var loaded = new AutoResetEvent(false);
        var restarted = new AutoResetEvent(false);
        var ended = new AutoResetEvent(false);
        using var player = MpvPlayer.Create(new Dictionary<string, string>
        {
            ["vo"] = "null", ["ao"] = "null", ["keep-open"] = "yes", ["idle"] = "yes", ["config"] = "no",
            ["terminal"] = "no", ["load-scripts"] = "no", ["ytdl"] = "no", ["sub-auto"] = "fuzzy",
        }, a => a());
        player.FileLoaded += () => loaded.Set();
        player.PlaybackRestart += () => restarted.Set();
        player.EndFile += (reason, _) =>
        {
            if (reason == Native.EndFileReason.Error)
                ended.Set();
        };
        player.Observe("time-pos", Native.Format.Double);
        Console.WriteLine($"libmpv: {player.GetString("mpv-version")} · ffmpeg {player.GetString("ffmpeg-version")}");

        foreach (var file in files.Where(File.Exists))
        {
            Console.WriteLine(Path.GetFileName(file));
            player.Set("pause", false);
            player.CommandAsync("loadfile", file, "replace");
            var which = WaitHandle.WaitAny([loaded, ended], TimeSpan.FromSeconds(15));
            Check(which == 0, "abriu");
            if (which != 0)
                continue;
            restarted.WaitOne(TimeSpan.FromSeconds(5));
            var duration = player.GetDouble("duration") ?? 0;
            var tracks = player.ReadTracks();
            var chapters = player.ReadChapters();
            Console.WriteLine($"  duração {duration:0.00} s · faixas: {string.Join(", ", tracks.Select(t => $"{t.Type}#{t.Id}{(t.Lang is null ? "" : ":" + t.Lang)}{(t.External ? "(ext)" : "")}"))} · capítulos: {chapters.Count}");
            Check(duration > 0, "duração conhecida");

            var clock = Stopwatch.StartNew();
            var p0 = player.TimePos;
            Thread.Sleep(1500);
            var p1 = player.TimePos;
            Check(double.IsFinite(p1) && p1 > (double.IsFinite(p0) ? p0 : 0) + 0.5, $"a posição anda ({p0:0.00} → {p1:0.00} s)");

            if (duration > 4)
            {
                var target = Math.Round(duration * 0.5, 1);
                restarted.Reset();
                player.CommandAsync("seek", MpvPlayer.Seconds(target), "absolute+exact");
                restarted.WaitOne(TimeSpan.FromSeconds(5));
                Thread.Sleep(150);
                var p2 = player.TimePos;
                Check(Math.Abs(p2 - target) < 1.0, $"salto para {target:0.0} s → {p2:0.00} s");
            }

            if (tracks.Any(t => t.Type == "video" && !t.AlbumArt))
            {
                var frame = Thumbnailer.Capture(file, duration * 0.3, 256, TimeSpan.FromSeconds(10));
                var bright = frame is null ? 0 : Brightness(frame);
                Check(frame is not null && bright > 8, frame is null ? "miniatura" : $"miniatura {frame.Width}×{frame.Height} (brilho {bright:0})");
            }
            Console.WriteLine($"  ({clock.Elapsed.TotalSeconds:0.0} s)");
        }
        Console.WriteLine(failures == 0 ? "OK" : $"{failures} falha(s)");
        return failures == 0 ? 0 : 1;
    }

    private static double Brightness(ThumbFrame f)
    {
        long sum = 0;
        var n = 0;
        for (var y = 0; y < f.Height; y += 4)
            for (var x = 0; x < f.Width; x += 4)
            {
                var i = y * f.Stride + x * 4;
                sum += f.Pixels[i] + f.Pixels[i + 1] + f.Pixels[i + 2];
                n += 3;
            }
        return n == 0 ? 0 : (double)sum / n;
    }
}
