using System.IO;
using System.Windows;

namespace DiscordLookup;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const long MaxErrorLogBytes = 512L * 1024;
    private static readonly string ErrorLogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiscordLookupGUI",
        "error.log"
    );

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 未処理の例外でアプリケーションが突然クラッシュするのを防止しつつログを記録
        DispatcherUnhandledException += (s, args) =>
        {
            LogException("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogException("AppDomainUnhandledException", args.ExceptionObject as Exception);
        };
    }

    public static void LogException(string source, Exception? ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(ErrorLogFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {ex?.ToString() ?? "(Unknown non-Exception object)"}{Environment.NewLine}";
            if (File.Exists(ErrorLogFilePath) && new FileInfo(ErrorLogFilePath).Length > MaxErrorLogBytes)
            {
                File.WriteAllText(ErrorLogFilePath, entry);
            }
            else
            {
                File.AppendAllText(ErrorLogFilePath, entry);
            }
        }
        catch
        {
            // ログ記録自体が失敗した場合は握りつぶす
        }
    }
}

