using Tela.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Tela.Views;

/// <summary>Estados de demonstração para as capturas do README (sem libmpv nem vídeos reais).</summary>
public partial class MainWindow
{
    /// <summary>Um "vídeo" a meio, com os controlos à vista, uma legenda e uma mensagem.</summary>
    internal void DemoPlaying()
    {
        HideHome();
        var scene = new DemoScene { IsHitTestVisible = false };
        Root.Children.Insert(Root.Children.IndexOf(Surface), scene);
        var subtitle = new TextBlock
        {
            Text = Loc.Language == UiLanguage.Pt ? "Devíamos ter apanhado o comboio das oito." : "We should have caught the eight o'clock train.",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 112),
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, OffsetX = 0, OffsetY = 1, Opacity = 1 },
        };
        Root.Children.Insert(Root.Children.IndexOf(Surface), subtitle);

        _loaded = true;
        _paused = false;
        _duration = 2580;
        TitleText.Text = "Night Train";
        QueueText.Text = "3/8";
        Title = "Night Train · Tela";
        Seek.Maximum = _duration;
        Seek.IsEnabled = true;
        Seek.Value = 1130;
        Seek.Marks = [0, 610, 1320, 2100];
        TimeText.Text = $"{FormatTime(1130)} / {FormatTime(_duration)}";
        PlayGlyph.Kind = GlyphKind.Pause;
        SubsButton.IsEnabled = AudioButton.IsEnabled = PrevButton.IsEnabled = NextButton.IsEnabled = true;
        ShowChrome(temporary: false);
        ShowOsd(Loc.F("SubtitlesOsd", "Português"), seconds: 600);
    }
}
