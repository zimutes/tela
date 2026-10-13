using Tela.Controls;
using Tela.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Tela.Views;

public partial class MainWindow
{
    private readonly List<Bitmap> _homeBitmaps = [];
    private readonly List<Button> _cards = [];

    private void SetupHome()
    {
        OpenFileButton.Click += (_, _) => _ = OpenFilesAsync();
        OpenFolderButton.Click += (_, _) => _ = OpenFolderAsync();
        HomeScroll.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty)
                HomeGrid.MinHeight = Math.Max(0, HomeScroll.Bounds.Height);
        };
    }

    private void ShowHome()
    {
        HideUpNext();
        Home.IsVisible = true;
        // No ecrã inicial o nome grande já está ao centro: em cima ficam só os botões da janela.
        TopLogo.IsVisible = TitleText.IsVisible = false;
        AudioView.IsVisible = false;
        BottomBar.IsVisible = false;
        InfoPanel.IsVisible = false;
        Preview.IsVisible = false;
        _firstFit = true;
        UpdateTitle();
        UpdateQueueButtons();
        RefreshHome();
        ShowChrome(temporary: false);
    }

    private void HideHome()
    {
        if (!Home.IsVisible)
            return;
        Home.IsVisible = false;
        TopLogo.IsVisible = TitleText.IsVisible = true;
        BottomBar.IsVisible = true;
        foreach (var b in _homeBitmaps)
            b.Dispose();
        _homeBitmaps.Clear();
        ContinueList.ItemsSource = null;
        _cards.Clear();
    }

    private void ShowMissingLibmpv(string? error)
    {
        MissingBox.IsVisible = true;
        OpenButtons.IsEnabled = false;
        HomeHint.IsVisible = false;
        var how = OperatingSystem.IsWindows() ? Loc.T("MpvMissingWindows") : Loc.T("MpvMissingLinux");
        MissingText.Text = string.IsNullOrEmpty(error) ? how : $"{how}\n\n{error}";
    }

    /// <summary>Os cartões do "continuar a ver": imagem, barra do que já se viu, título e o que falta.</summary>
    private void RefreshHome()
    {
        foreach (var b in _homeBitmaps)
            b.Dispose();
        _homeBitmaps.Clear();
        _cards.Clear();
        var entries = _player is null && !_demo ? [] : _history.ContinueWatching(8);
        ContinuePanel.IsVisible = entries.Count > 0;
        ContinueList.ItemsSource = entries.Select(Card).ToList();
    }

    private Button Card(HistoryEntry entry)
    {
        Bitmap? image = null;
        try
        {
            var file = _history.ThumbPath(entry);
            if (entry.Thumb is not null && File.Exists(file))
            {
                image = new Bitmap(file);
                _homeBitmaps.Add(image);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Imagem do continuar a ver: " + ex.Message);
        }

        var thumb = new Border
        {
            Classes = { "thumb" },
            Height = 108,
            ClipToBounds = true,
            Child = image is null
                ? new Glyph { Kind = GlyphKind.Logo, Width = 34, Height = 34, Opacity = 0.35 }
                : new Image { Source = image, Stretch = Stretch.UniformToFill },
        };
        var progress = new Panel { Height = 4, VerticalAlignment = VerticalAlignment.Bottom };
        progress.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)) });
        var fill = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 190 * entry.Progress };
        fill.Bind(Border.BackgroundProperty, this.GetResourceObservable("Accent"));
        progress.Children.Add(fill);

        var forget = new Button { Classes = { "forget" }, Content = new Glyph { Kind = GlyphKind.Close } };
        ToolTip.SetTip(forget, Loc.T("ForgetTip"));
        forget.Click += (_, e) =>
        {
            e.Handled = true;
            _history.Remove(entry.Path);
            _history.Save();
            RefreshHome();
        };

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(entry.Title) ? MediaFiles.DisplayName(entry.Path) : entry.Title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(2, 8, 2, 0),
        };
        var left = new TextBlock
        {
            Text = Loc.F("TimeLeft", RoughDuration(entry.Remaining)),
            FontSize = 11.5,
            Classes = { "muted" },
            Margin = new Thickness(2, 2, 2, 0),
        };
        var card = new Button
        {
            Classes = { "resume" },
            Content = new StackPanel
            {
                Children =
                {
                    new Panel { Children = { thumb, progress, forget } },
                    title,
                    left,
                },
            },
        };
        ToolTip.SetTip(card, entry.Path);
        card.Click += (_, _) => Open([entry.Path]);
        _cards.Add(card);
        return card;
    }

    /// <summary>"faltam 23 min", "faltam 1 h 05".</summary>
    internal static string RoughDuration(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(60, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00}" : $"{Math.Max(1, (int)Math.Round(t.TotalMinutes))} min";
    }

    /// <summary>No ecrã inicial: O abre, as setas escolhem um cartão, Enter abre-o, Delete esquece-o.</summary>
    private bool HomeKey(Key key, bool ctrl, bool shift)
    {
        switch (key)
        {
            case Key.O when !ctrl:
                _ = shift ? OpenFolderAsync() : OpenFilesAsync();
                return true;
            case Key.Left or Key.Right or Key.Down or Key.Up when _cards.Count > 0:
            {
                var focused = _cards.FindIndex(c => c.IsFocused);
                var next = focused < 0 ? 0 : Math.Clamp(focused + (key is Key.Left or Key.Up ? -1 : 1), 0, _cards.Count - 1);
                _cards[next].Focus(NavigationMethod.Directional);
                return true;
            }
            case Key.Delete when _cards.FindIndex(c => c.IsFocused) is var i and >= 0:
            {
                var path = ToolTip.GetTip(_cards[i]) as string;
                if (path is not null)
                {
                    _history.Remove(path);
                    _history.Save();
                    RefreshHome();
                    if (_cards.Count > 0)
                        _cards[Math.Min(i, _cards.Count - 1)].Focus(NavigationMethod.Directional);
                }
                return true;
            }
            default:
                return false;
        }
    }
}
