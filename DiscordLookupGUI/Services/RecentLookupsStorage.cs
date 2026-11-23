using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DiscordLookup.Models;

namespace DiscordLookup.Services;

/// <summary>
/// 検索履歴の永続化管理サービス (%APPDATA%\DiscordLookupGUI\recent_lookups.json)
/// メモリキャッシュ＋バックグラウンドのアトミック書き込みにより UI ブロックとファイル破損を防止する。
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
    private static readonly object _ioLock = new();
    private static List<RecentLookupItem>? _cachedList;
    private static long _saveVersion = 0;

    private static List<RecentLookupItem> LoadInternalUnsafe()
    {
        if (_cachedList != null)
        {
            return _cachedList;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath, Encoding.UTF8);
                var list = JsonSerializer.Deserialize<List<RecentLookupItem>>(json);
                if (list != null)
                {
                    _cachedList = list.OrderByDescending(x => x.LookedUpAt).Take(MaxHistoryCount).ToList();
                    return _cachedList;
                }
            }
        }
        catch (Exception ex)
        {
            App.LogException("RecentLookupsStorage.Load", ex);
        }

        _cachedList = new List<RecentLookupItem>();
        return _cachedList;
    }

    private static long GetCurrentSaveVersion()
    {
        lock (_lock)
        {
            return _saveVersion;
        }
    }

    /// <summary>
    /// 保存されている検索履歴を新しい順に取得（スナップショットのコピーを返す）
    /// </summary>
    public static List<RecentLookupItem> Load()
    {
        lock (_lock)
        {
            return LoadInternalUnsafe().ToList();
        }
    }

    /// <summary>
    /// 検索履歴を追加または最新日時に更新し、非同期でアトミック保存する
    /// </summary>
    public static void AddOrUpdate(string userId, string username, string? displayUsername, string? globalName, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        string jsonSnapshot;
        long targetVersion;

        lock (_lock)
        {
            try
            {
                var list = LoadInternalUnsafe();
                var normalizedId = userId.Trim();

                // 既存の同一ユーザーがあれば削除
                list.RemoveAll(x => x.UserId == normalizedId);

                // 先頭に最新の履歴を追加
                list.Insert(0, new RecentLookupItem
                {
                    UserId = normalizedId,
                    Username = username?.Trim() ?? string.Empty,
                    DisplayUsername = string.IsNullOrWhiteSpace(displayUsername) ? null : displayUsername.Trim(),
                    GlobalName = string.IsNullOrWhiteSpace(globalName) ? null : globalName.Trim(),
                    AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
                    LookedUpAt = DateTime.UtcNow
                });

                // 最大件数にトリム
                if (list.Count > MaxHistoryCount)
                {
                    _cachedList = list.Take(MaxHistoryCount).ToList();
                    list = _cachedList;
                }

                targetVersion = ++_saveVersion;
                jsonSnapshot = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                App.LogException("RecentLookupsStorage.AddOrUpdate (Serialize)", ex);
                return;
            }
        }

        _ = Task.Run(() => WriteJsonAtomic(jsonSnapshot, targetVersion));
    }

    private static void WriteJsonAtomic(string json, long version)
    {
        lock (_ioLock)
        {
            if (GetCurrentSaveVersion() != version)
            {
                return;
            }

            var tempPath = Path.Combine(SettingsDirectory, $"recent_lookups_{Guid.NewGuid():N}.tmp");
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                File.WriteAllText(tempPath, json, Encoding.UTF8);

                if (GetCurrentSaveVersion() == version)
                {
                    File.Move(tempPath, FilePath, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                App.LogException("RecentLookupsStorage.WriteJsonAtomic", ex);
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

    /// <summary>
    /// 全ての検索履歴を消去
    /// </summary>
    public static void Clear()
    {
        long targetVersion;
        lock (_lock)
        {
            _cachedList = new List<RecentLookupItem>();
            targetVersion = ++_saveVersion;
        }

        _ = Task.Run(() =>
        {
            lock (_ioLock)
            {
                if (GetCurrentSaveVersion() != targetVersion)
                {
                    return;
                }

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
        });
    }
}
