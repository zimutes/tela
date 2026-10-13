using Tela.Library;

namespace Tela.Tests;

public sealed class HistoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tela-hist-" + Guid.NewGuid().ToString("N")[..8]);

    public HistoryTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData(7200, 7100, true)]   // filme: os últimos 2 min são créditos
    [InlineData(7200, 7000, false)]
    [InlineData(40, 36, true)]       // vídeo curto: os últimos 5 s
    [InlineData(40, 30, false)]
    [InlineData(1500, 1430, true)]   // episódio de 25 min: os últimos 5%
    [InlineData(1500, 1400, false)]
    [InlineData(0, 10, false)]       // sem duração (direto): nunca acaba
    public void FinishedMeansReachingTheCredits(double duration, double position, bool finished) =>
        Assert.Equal(finished, History.IsFinished(position, duration));

    [Fact]
    public void ResumesALittleBeforeWhereItStopped()
    {
        var h = new History(_dir);
        var e = h.Update("/v/a.mkv", "A", 600, 3000);
        Assert.Equal(597, History.ResumePoint(e));
    }

    [Fact]
    public void NoResumeAtTheVeryStartOrAfterTheEnd()
    {
        var h = new History(_dir);
        Assert.Null(History.ResumePoint(h.Update("/v/a.mkv", "A", 5, 3000)));
        var done = h.Update("/v/b.mkv", "B", 2990, 3000);
        Assert.True(done.Finished);
        Assert.Equal(0, done.Position);
        Assert.Null(History.ResumePoint(done));
    }

    [Fact]
    public void ContinueWatchingIsRecentFirstAndOnlyExistingFiles()
    {
        var a = Path.Combine(_dir, "a.mkv");
        var b = Path.Combine(_dir, "b.mkv");
        File.WriteAllText(a, "");
        File.WriteAllText(b, "");
        var h = new History(_dir);
        h.Update(a, "A", 300, 3000);
        h.Update(Path.Combine(_dir, "gone.mkv"), "Gone", 300, 3000);
        h.Update(b, "B", 300, 3000);
        Assert.Equal(["B", "A"], h.ContinueWatching(8).Select(e => e.Title));
    }

    [Fact]
    public void SavesAndLoads()
    {
        var h = new History(_dir);
        var e = h.Update("/v/a.mkv", "A", 120, 3000);
        e.Aid = "2";
        e.Sid = "no";
        e.SubDelay = -0.3;
        h.Save();
        var loaded = History.Load(_dir).Find("/v/a.mkv");
        Assert.NotNull(loaded);
        Assert.Equal(120, loaded!.Position);
        Assert.Equal("2", loaded.Aid);
        Assert.Equal("no", loaded.Sid);
        Assert.Equal(-0.3, loaded.SubDelay);
    }

    [Fact]
    public void KeepsAtMost300()
    {
        var h = new History(_dir);
        for (var i = 0; i < 320; i++)
            h.Update($"/v/{i}.mkv", $"{i}", 100, 3000);
        Assert.Equal(300, h.Entries.Count);
        Assert.Equal("319", h.Entries[0].Title);
    }
}
