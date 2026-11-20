using System.IO;
using System.Text.Json;
using System.Windows;

namespace DiscordLookup.Services;

public enum AppTheme
{
    Dark,
    Light,
    Sunset,
    StarryNight,
    GamingRgb
}

/// <summary>
/// アプリ全体のテーマ(ダーク / ライト)を動的に切り替えて保存するマネージャー
/// </summary>
public static class ThemeManager
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public static event Action<AppTheme>? ThemeChanged;

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiscordLookupGUI",
        "theme_settings.json"
    );

    /// <summary>
    /// 保存されているテーマを読み込んで適用する
    /// </summary>
    public static void Initialize()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
                if (settings != null && Enum.TryParse<AppTheme>(settings.SelectedTheme, out var savedTheme))
                {
                    ApplyTheme(savedTheme, save: false);
                    return;
                }
            }
        }
        catch
        {
            // 読み込み失敗時はデフォルトのダークテーマを使用
        }

        ApplyTheme(AppTheme.Dark, save: false);
    }

    /// <summary>
    /// テーマを動的に切り替える
    /// </summary>
    public static void ApplyTheme(AppTheme theme, bool save = true)
    {
        CurrentTheme = theme;
        var themeUri = theme switch
        {
            AppTheme.Light => new Uri("Themes/Light.xaml", UriKind.Relative),
            AppTheme.Sunset => new Uri("Themes/Sunset.xaml", UriKind.Relative),
            AppTheme.StarryNight => new Uri("Themes/StarryNight.xaml", UriKind.Relative),
            AppTheme.GamingRgb => new Uri("Themes/GamingRgb.xaml", UriKind.Relative),
            _ => new Uri("Themes/Dark.xaml", UriKind.Relative)
        };

        var newDict = new ResourceDictionary { Source = themeUri };

        // 既存のテーマ辞書を探して置き換える
        var appResources = Application.Current.Resources;
        ResourceDictionary? oldDict = null;

        foreach (var dict in appResources.MergedDictionaries)
        {
            if (dict.Source != null && (
                dict.Source.OriginalString.Contains("Dark.xaml") ||
                dict.Source.OriginalString.Contains("Light.xaml") ||
                dict.Source.OriginalString.Contains("Sunset.xaml") ||
                dict.Source.OriginalString.Contains("StarryNight.xaml") ||
                dict.Source.OriginalString.Contains("GamingRgb.xaml")))
            {
                oldDict = dict;
                break;
            }
        }

        if (oldDict != null)
        {
            appResources.MergedDictionaries.Remove(oldDict);
        }

        appResources.MergedDictionaries.Insert(0, newDict);

        if (save)
        {
            SaveTheme(theme);
        }

        ThemeChanged?.Invoke(theme);
    }

    private static void SaveTheme(AppTheme theme)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var settings = new ThemeSettings { SelectedTheme = theme.ToString() };
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // 保存失敗時は無視
        }
    }

    private class ThemeSettings
    {
        public string SelectedTheme { get; set; } = nameof(AppTheme.Dark);
    }
}
