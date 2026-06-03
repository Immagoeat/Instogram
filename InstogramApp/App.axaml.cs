using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using InstogramApp.Services;
using InstogramApp.ViewModels;
using InstogramApp.Views;

namespace InstogramApp;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeService.Apply(ThemeService.LoadSaved());

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = mainVm };

            // Auto-connect to server if a saved session exists
            _ = TryAutoConnectAsync(mainVm);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task TryAutoConnectAsync(MainWindowViewModel mainVm)
    {
        var cfg = ServerConfig.Load();
        if (cfg == null || string.IsNullOrEmpty(cfg.Token))
            return;

        try
        {
            ServerClient.Instance.Configure(cfg.ServerUrl, cfg.Token);
            var me = await ServerClient.Instance.GetMeAsync();
            if (me == null) { ServerConfig.Clear(); return; }

            await ServerClient.Instance.ConnectHubAsync();

            AppState.Instance.IsServerMode  = true;
            AppState.Instance.ServerUserId  = me.Id.ToString();
            AppState.Instance.ServerUsername = me.Username;
            AppState.Instance.ServerDisplay  = me.DisplayName;
            AppState.Instance.ServerAccent   = me.AccentColor;

            ServerConfig.Save(new ServerSettings(cfg.ServerUrl, cfg.Token,
                me.Username, me.DisplayName, me.AccentColor));

            Dispatcher.UIThread.Post(() => mainVm.OnServerLogin(me.Username, me.DisplayName));
        }
        catch
        {
            ServerConfig.Clear();
        }
    }
}
