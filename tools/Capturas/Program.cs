using System.Text.Json;
using Tela;
using Tela.Controls;
using Tela.Library;
using Tela.Themes;
using Tela.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

// As definições e o histórico de demonstração ficam numa pasta à parte (nunca os do utilizador).
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "capturas");
Directory.CreateDirectory(output);
var data = Path.Combine(output, "dados");
if (Directory.Exists(data))
    Directory.Delete(data, recursive: true);
Directory.CreateDirectory(data);
Environment.SetEnvironmentVariable("TELA_DATA", data);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

App.Prepare();
MakeHistory(data);

const int W = 960, H = 540;
var frames = new List<(ThemeDef Theme, string File)>();
foreach (var theme in Catalog.All)
{
    ThemeManager.Apply(theme);
    var window = new MainWindow(demo: true) { Width = W, Height = H };
    window.Show();
    Pump();
    window.DemoPlaying();
    Pump();
    var file = Path.Combine(output, $"{theme.Key}.png");
    window.CaptureRenderedFrame()!.Save(file);
    frames.Add((theme, file));
    Console.WriteLine($"{theme.Name}: {file}");
    window.Close();
    Pump();
}

// O ecrã inicial com o "continuar a ver", no tema por omissão.
ThemeManager.Apply(Catalog.All[0]);
var home = new MainWindow(demo: true) { Width = W, Height = 600 };
home.Show();
Pump();
var homeFile = Path.Combine(output, "home.png");
home.CaptureRenderedFrame()!.Save(homeFile);
Console.WriteLine($"Ecrã inicial: {homeFile}");
home.Close();
Pump();

// Os seis temas numa grelha de 3 × 2, para o README.
const double scale = 0.5;
var cell = new Size(W * scale, H * scale);
const int gap = 12;
var grid = new RenderTargetBitmap(new PixelSize((int)(cell.Width * 3 + gap * 2), (int)(cell.Height * 2 + gap)));
using (var ctx = grid.CreateDrawingContext())
{
    for (var i = 0; i < frames.Count; i++)
    {
        using var bmp = new Bitmap(frames[i].File);
        var x = i % 3 * (cell.Width + gap);
        var y = i / 3 * (cell.Height + gap);
        ctx.DrawImage(bmp, new Rect(bmp.Size), new Rect(new Point(x, y), cell));
    }
}
var themesFile = Path.Combine(output, "themes.png");
grid.Save(themesFile);
Console.WriteLine($"Grelha: {themesFile}");

static void Pump()
{
    for (var i = 0; i < 6; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}

// Quatro vídeos "a meio", com imagens desenhadas (DemoScene) e ficheiros vazios (para existirem).
static void MakeHistory(string data)
{
    var videos = Path.Combine(data, "videos");
    Directory.CreateDirectory(videos);
    var history = new History(data);
    (string File, string Title, double Pos, double Dur)[] items =
    [
        ("Night Train S01E03.mkv", "Night Train · S01E03", 1130, 2580),
        ("Morning Lake.mp4", "Morning Lake", 540, 5400),
        ("Moonrise.mkv", "Moonrise", 3300, 6900),
        ("Desert Road S02E07.mkv", "Desert Road · S02E07", 400, 2700),
    ];
    Directory.CreateDirectory(history.ThumbFolder);
    for (var i = items.Length - 1; i >= 0; i--)
    {
        var path = Path.Combine(videos, items[i].File);
        File.WriteAllText(path, "");
        var e = history.Update(path, items[i].Title, items[i].Pos, items[i].Dur);
        e.Thumb = History.ThumbName(path);
        var scene = new DemoScene { Variant = i, Width = 320, Height = 180 };
        scene.Measure(new Size(320, 180));
        scene.Arrange(new Rect(0, 0, 320, 180));
        using var rtb = new RenderTargetBitmap(new PixelSize(320, 180));
        rtb.Render(scene);
        rtb.Save(Path.Combine(history.ThumbFolder, e.Thumb));
    }
    history.Save();
}
