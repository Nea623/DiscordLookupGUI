using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DiscordLookup.Models;

namespace DiscordLookup.Services;

/// <summary>
/// 検索履歴の永続化管理サービス (%APPDATA%\DiscordLookupGUI\recent_lookups.json)
/// </summary>
public static class RecentLookupsStorage
{
    private const int MaxHistoryCount = 20;

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiscordLookupGUI"
    );

    private static readonly string FilePath = Path.Combine(SettingsDirectory, "recent_lookups.json");
    private static readonly object _lock = new();

    /// <summary>
    /// 保存されている検索履歴を新しい順に取得
    /// </summary>
    public static List<RecentLookupItem> Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var list = JsonSerializer.Deserialize<List<RecentLookupItem>>(json);
                    if (list != null)
                    {
                        return list.OrderByDescending(x => x.LookedUpAt).Take(MaxHistoryCount).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogException("RecentLookupsStorage.Load", ex);
            }

            return new List<RecentLookupItem>();
        }
    }

    /// <summary>
    /// 検索履歴を追加または最新日時に更新して保存
    /// </summary>
    public static void AddOrUpdate(string userId, string username, string? globalName, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        lock (_lock)
        {
            try
            {
                var list = Load();

                // 既存の同一ユーザーがあれば削除
                list.RemoveAll(x => x.UserId == userId);

                // 先頭に最新の履歴を追加
                list.Insert(0, new RecentLookupItem
                {
                    UserId = userId.Trim(),
                    Username = username?.Trim() ?? string.Empty,
                    GlobalName = string.IsNullOrWhiteSpace(globalName) ? null : globalName.Trim(),
                    AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
                    LookedUpAt = DateTime.UtcNow
                });

                // 最大件数にトリム
                if (list.Count > MaxHistoryCount)
                {
                    list = list.Take(MaxHistoryCount).ToList();
                }

                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                App.LogException("RecentLookupsStorage.AddOrUpdate", ex);
            }
        }
    }

    /// <summary>
    /// 全ての検索履歴を消去
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception ex)
            {
                App.LogException("RecentLookupsStorage.Clear", ex);
            }
        }
    }
}
