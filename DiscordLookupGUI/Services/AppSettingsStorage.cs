using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DiscordLookup.Services;

public sealed class AppConfig
{
    public bool AutoLookupEnabled { get; set; } = false;
}

/// <summary>
/// アプリケーション一般設定の永続化サービス (%APPDATA%\DiscordLookupGUI\app_settings.json)
/// </summary>
public static class AppSettingsStorage
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiscordLookupGUI"
    );

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "app_settings.json");
    private static readonly object _lock = new();

    /// <summary>
    /// 設定ファイルを読み込む（存在しない場合はデフォルト値を返す）
    /// </summary>
    public static AppConfig LoadConfig()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogException("AppSettingsStorage.LoadConfig", ex);
            }

            return new AppConfig();
        }
    }

    /// <summary>
    /// 設定ファイルに書き込んで保存する（一時ファイル経由のアトミック書き込み）
    /// </summary>
    public static void SaveConfig(AppConfig config)
    {
        lock (_lock)
        {
            var tempPath = Path.Combine(SettingsDirectory, $"app_settings_{Guid.NewGuid():N}.tmp");
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(tempPath, json, Encoding.UTF8);
                File.Move(tempPath, SettingsFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                App.LogException("AppSettingsStorage.SaveConfig", ex);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch { }
                }
            }
        }
    }
}
