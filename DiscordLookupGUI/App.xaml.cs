using System.Configuration;
using System.Data;
using System.Windows;

namespace DiscordLookup;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 未処理の例外でアプリケーションが突然クラッシュするのを防止
        DispatcherUnhandledException += (s, args) =>
        {
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
        };
    }
}

