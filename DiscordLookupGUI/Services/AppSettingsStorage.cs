using System;
using System.IO;
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

    /// <summary>
    /// 設定ファイルを読み込む（存在しない場合はデフォルト値を返す）
    /// </summary>
    public static AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
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

    /// <summary>
    /// 設定ファイルに書き込んで保存する
    /// </summary>
    public static void SaveConfig(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            App.LogException("AppSettingsStorage.SaveConfig", ex);
        }
    }
}
