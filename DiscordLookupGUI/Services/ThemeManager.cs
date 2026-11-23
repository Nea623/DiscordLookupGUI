using System.IO;
using System.Text;
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
    private static readonly object _saveLock = new();

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

        try
        {
            ApplyTheme(AppTheme.Dark, save: false);
        }
        catch
        {
            // フォールバックも失敗した場合は既定のリソースを維持
        }
    }

    private static bool IsThemeDictionary(ResourceDictionary dict)
    {
        var source = dict.Source?.OriginalString;
        if (string.IsNullOrEmpty(source)) return false;

        return source.Contains("Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("Sunset.xaml", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("StarryNight.xaml", StringComparison.OrdinalIgnoreCase) ||
               source.Contains("GamingRgb.xaml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// テーマを動的に切り替える
    /// </summary>
    public static void ApplyTheme(AppTheme theme, bool save = true)
    {
        var app = Application.Current;
        if (app == null) return;

        var themeUri = theme switch
        {
            AppTheme.Light => new Uri("Themes/Light.xaml", UriKind.Relative),
            AppTheme.Sunset => new Uri("Themes/Sunset.xaml", UriKind.Relative),
            AppTheme.StarryNight => new Uri("Themes/StarryNight.xaml", UriKind.Relative),
            AppTheme.GamingRgb => new Uri("Themes/GamingRgb.xaml", UriKind.Relative),
            _ => new Uri("Themes/Dark.xaml", UriKind.Relative)
        };

        var merged = app.Resources.MergedDictionaries;
        var existingIndices = new List<int>();
        for (int i = 0; i < merged.Count; i++)
        {
            if (IsThemeDictionary(merged[i]))
            {
                existingIndices.Add(i);
            }
        }

        if (existingIndices.Count == 1 &&
            CurrentTheme == theme &&
            merged[existingIndices[0]].Source?.OriginalString.Equals(themeUri.OriginalString, StringComparison.OrdinalIgnoreCase) == true)
        {
            if (save)
            {
                SaveTheme(theme);
            }
            return;
        }

        ResourceDictionary newDict;
        try
        {
            newDict = new ResourceDictionary { Source = themeUri };
        }
        catch (Exception ex)
        {
            App.LogException("ThemeManager.ApplyTheme", ex);
            return;
        }

        int targetIndex = existingIndices.Count > 0 ? existingIndices[0] : 0;
        for (int i = existingIndices.Count - 1; i >= 0; i--)
        {
            merged.RemoveAt(existingIndices[i]);
        }
        if (targetIndex >= merged.Count)
        {
            merged.Add(newDict);
        }
        else
        {
            merged.Insert(targetIndex, newDict);
        }

        CurrentTheme = theme;

        if (save)
        {
            SaveTheme(theme);
        }

        ThemeChanged?.Invoke(theme);
    }

    private static void SaveTheme(AppTheme theme)
    {
        lock (_saveLock)
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            var tempPath = string.IsNullOrEmpty(dir)
                ? $"theme_settings_{Guid.NewGuid():N}.tmp"
                : Path.Combine(dir, $"theme_settings_{Guid.NewGuid():N}.tmp");

            try
            {
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = new ThemeSettings { SelectedTheme = theme.ToString() };
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(tempPath, json, Encoding.UTF8);
                File.Move(tempPath, SettingsFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                App.LogException("ThemeManager.SaveTheme", ex);
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

    private class ThemeSettings
    {
        public string SelectedTheme { get; set; } = nameof(AppTheme.Dark);
    }
}
