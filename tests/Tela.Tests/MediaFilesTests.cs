using Tela.Library;

namespace Tela.Tests;

public sealed class MediaFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tela-testes-" + Guid.NewGuid().ToString("N")[..8]);

    public MediaFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Touch(string relative)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void NaturalOrderPutsEpisode2BeforeEpisode10()
    {
        var names = new List<string> { "Ep10.mkv", "Ep2.mkv", "ep1.mkv", "Ep02b.mkv" };
        names.Sort(NaturalComparer.Instance);
        Assert.Equal(["ep1.mkv", "Ep2.mkv", "Ep02b.mkv", "Ep10.mkv"], names);
    }

    [Fact]
    public void OneVideoBringsItsFolderAlong()
    {
        Touch("Show.S01E10.mkv");
        var e2 = Touch("Show.S01E02.mkv");
        Touch("Show.S01E01.mkv");
        Touch("notes.txt");
        Touch("Show.S01E01.srt");
        Touch(".hidden.mkv");

        var (items, start, subs) = MediaFiles.Expand([e2]);

        Assert.Equal(["Show.S01E01.mkv", "Show.S01E02.mkv", "Show.S01E10.mkv"], items.Select(Path.GetFileName));
        Assert.Equal(1, start);
        Assert.Empty(subs);
    }

    [Fact]
    public void SeveralFilesStayJustThoseInNaturalOrder()
    {
        var b = Touch("b10.mp4");
        var a = Touch("b9.mp4");
        Touch("other.mp4");

        var (items, start, _) = MediaFiles.Expand([b, a]);

        Assert.Equal(["b9.mp4", "b10.mp4"], items.Select(Path.GetFileName));
        Assert.Equal(0, start);
    }

    [Fact]
    public void FolderOpensSeasonsInOrder()
    {
        Touch("Season 2/E1.mkv");
        Touch("Season 1/E2.mkv");
        Touch("Season 1/E1.mkv");
        Touch("Season 10/E1.mkv");
        Touch("Season 1/cover.jpg");

        var (items, _, _) = MediaFiles.Expand([_dir]);

        Assert.Equal(
            ["Season 1/E1.mkv", "Season 1/E2.mkv", "Season 2/E1.mkv", "Season 10/E1.mkv"],
            items.Select(p => Path.GetRelativePath(_dir, p).Replace('\\', '/')));
    }

    [Fact]
    public void SubtitlesAloneAreSetApart()
    {
        var sub = Touch("movie.pt.srt");
        var (items, _, subs) = MediaFiles.Expand([sub]);
        Assert.Empty(items);
        Assert.Equal([sub], subs);
    }

    [Fact]
    public void AudioFileBringsTheOtherAudioFiles()
    {
        var a = Touch("02 Two.flac");
        Touch("01 One.mp3");
        Touch("clip.mkv");
        var (items, start, _) = MediaFiles.Expand([a]);
        Assert.Equal(["01 One.mp3", "02 Two.flac"], items.Select(Path.GetFileName));
        Assert.Equal(1, start);
    }

    [Theory]
    [InlineData("Show.S01E02.1080p.mkv", "Show S01E02 1080p")]
    [InlineData("My_Holiday_2019.mp4", "My Holiday 2019")]
    [InlineData("Night Train - Part 1.mkv", "Night Train - Part 1")]
    [InlineData("Mr.Robot.mkv", "Mr Robot")]
    public void DisplayNameIsReadable(string file, string expected) =>
        Assert.Equal(expected, MediaFiles.DisplayName(file));
}
