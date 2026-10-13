using System.Text.RegularExpressions;
using Tela.Views;

namespace Tela.Tests;

public class TextTests
{
    [Fact]
    public void EveryTextExistsInBothLanguagesWithTheSamePlaceholders()
    {
        static string[] Holes(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Order().ToArray();
        foreach (var (key, (en, pt)) in Loc.Texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(en), key);
            Assert.False(string.IsNullOrWhiteSpace(pt), key);
            Assert.Equal(Holes(en), Holes(pt));
        }
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65.9, "1:05")]
    [InlineData(3600, "1:00:00")]
    [InlineData(5025, "1:23:45")]
    [InlineData(double.NaN, "0:00")]
    public void TimeFormat(double seconds, string expected) => Assert.Equal(expected, MainWindow.FormatTime(seconds));

    [Theory]
    [InlineData(1, "1×")]
    [InlineData(1.25, "1.25×")]
    [InlineData(0.5, "0.5×")]
    public void SpeedFormat(double speed, string expected) => Assert.Equal(expected, MainWindow.FormatSpeed(speed));

    [Theory]
    [InlineData(0, "0 ms")]
    [InlineData(0.3, "+300 ms")]
    [InlineData(-0.1, "−100 ms")]
    public void DelayFormat(double seconds, string expected) => Assert.Equal(expected, MainWindow.FormatDelay(seconds));

    [Fact]
    public void LanguageNamesAreTheirOwn()
    {
        Assert.Null(MainWindow.LanguageName("und"));
        Assert.Null(MainWindow.LanguageName(null));
        var pt = MainWindow.LanguageName("por");
        // Sem ICU (alguns Linux mínimos) só se tem o código; com ICU, o nome da língua.
        Assert.True(pt is "Português" or "POR", pt);
    }

    [Fact]
    public void SettingsAreKeptInRange()
    {
        var s = new AppSettings { Volume = 400, SubScale = double.NaN, WindowWidth = 10, AudioDevice = "" };
        SettingsStore.Sanitize(s);
        Assert.Equal(150, s.Volume);
        Assert.Equal(1, s.SubScale);
        Assert.Equal(320, s.WindowWidth);
        Assert.Equal("auto", s.AudioDevice);
    }
}
