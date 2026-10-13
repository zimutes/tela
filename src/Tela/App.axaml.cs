using Tela.Platform;
using Tela.Themes;
using Tela.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Tela;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static string[] StartupArgs { get; set; } = [];

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Carrega definições, língua e tema antes de haver janela (as capturas também usam).</summary>
    public static void Prepare()
    {
        Settings = SettingsStore.Load();
        var theme = Catalog.Get(Settings.Theme);
        Loc.Apply(Loc.Parse(Settings.Language), theme.Uppercase);
        ThemeManager.Apply(theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Prepare();
            var window = new MainWindow();
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.HandleArgs(StartupArgs, startup: true);
            SingleInstance.Listen(args => Dispatcher.UIThread.Post(() => window.HandleArgs(args, startup: false)));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
