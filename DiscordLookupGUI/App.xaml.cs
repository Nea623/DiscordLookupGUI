using System.IO;
using System.Windows;

namespace DiscordLookup;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const long MaxErrorLogBytes = 512L * 1024;
    private const int MaxDispatcherExceptionsInWindow = 5;
    private static readonly TimeSpan DispatcherExceptionWindow = TimeSpan.FromSeconds(5);

    private static readonly object _logLock = new();
    private static readonly Queue<DateTime> _recentDispatcherExceptionTimes = new();

    private static readonly string ErrorLogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiscordLookupGUI",
        "error.log"
    );
    private static readonly string ErrorLogBackupFilePath = ErrorLogFilePath + ".old";

    private static bool IsFatalException(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is OutOfMemoryException or StackOverflowException or AccessViolationException)
            {
                return true;
            }
            if (ex is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    if (IsFatalException(inner)) return true;
                }
            }
            ex = ex.InnerException;
        }
        return false;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 未処理の例外でアプリケーションが突然クラッシュするのを防止しつつログを記録
        DispatcherUnhandledException += (s, args) =>
        {
            LogException("DispatcherUnhandledException", args.Exception);

            bool shouldCrash = IsFatalException(args.Exception);
            if (!shouldCrash)
            {
                var now = DateTime.UtcNow;
                lock (_logLock)
                {
                    while (_recentDispatcherExceptionTimes.Count > 0 &&
                           now - _recentDispatcherExceptionTimes.Peek() > DispatcherExceptionWindow)
                    {
                        _recentDispatcherExceptionTimes.Dequeue();
                    }
                    _recentDispatcherExceptionTimes.Enqueue(now);
                    if (_recentDispatcherExceptionTimes.Count >= MaxDispatcherExceptionsInWindow)
                    {
                        shouldCrash = true;
                    }
                }
            }

            args.Handled = !shouldCrash;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogException("AppDomainUnhandledException", ex);
            }
            else
            {
                LogMessage("AppDomainUnhandledException", args.ExceptionObject?.ToString() ?? "(Unknown non-Exception object)");
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogException("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    public static void LogException(string source, Exception? ex)
    {
        LogMessage(source, ex?.ToString() ?? "(Unknown non-Exception object)");
    }

    public static void LogMessage(string source, string message)
    {
        try
        {
            lock (_logLock)
            {
                var dir = Path.GetDirectoryName(ErrorLogFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}{Environment.NewLine}";
                if (File.Exists(ErrorLogFilePath) && new FileInfo(ErrorLogFilePath).Length > MaxErrorLogBytes)
                {
                    try
                    {
                        File.Move(ErrorLogFilePath, ErrorLogBackupFilePath, overwrite: true);
                        File.WriteAllText(ErrorLogFilePath, entry);
                    }
                    catch
                    {
                        File.WriteAllText(ErrorLogFilePath, entry);
                    }
                }
                else
                {
                    File.AppendAllText(ErrorLogFilePath, entry);
                }
            }
        }
        catch
        {
            // ログ記録自体が失敗した場合は握りつぶす
        }
    }
}

