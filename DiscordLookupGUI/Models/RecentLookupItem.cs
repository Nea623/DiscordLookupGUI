using System;

namespace DiscordLookup.Models;

/// <summary>
/// 直近の検索履歴アイテム
/// </summary>
public sealed class RecentLookupItem
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? GlobalName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime LookedUpAt { get; set; } = DateTime.UtcNow;
}
