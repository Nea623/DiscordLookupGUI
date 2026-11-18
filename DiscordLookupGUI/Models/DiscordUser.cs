namespace DiscordLookup.Models;

/// <summary>
/// Discord APIから取得したユーザー情報を保持するモデル
/// </summary>
public class DiscordUser
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? GlobalName { get; set; }
    public string? Avatar { get; set; }
    public string? Banner { get; set; }
    public int? AccentColor { get; set; }
    public string? BannerColor { get; set; }
    public ClanInfo? Clan { get; set; }

    // ── 算出プロパティ ──

    /// <summary>Snowflake IDからアカウント作成日時を算出</summary>
    public DateTimeOffset CreatedAt
    {
        get
        {
            if (!ulong.TryParse(Id, out var id)) return DateTimeOffset.MinValue;
            var timestamp = id >> 22;
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(timestamp + 1420070400000UL));
        }
    }

    /// <summary>日本時間での作成日時</summary>
    public DateTimeOffset CreatedAtJst => CreatedAt.ToOffset(TimeSpan.FromHours(9));

    /// <summary>作成からの経過日数</summary>
    public double ElapsedDays => (DateTimeOffset.UtcNow - CreatedAt).TotalDays;

    /// <summary>作成からの経過年数</summary>
    public double ElapsedYears => ElapsedDays / 365.0;

    /// <summary>アバターURL（null の場合はデフォルトアバター）</summary>
    public string AvatarUrl
    {
        get
        {
            if (string.IsNullOrEmpty(Avatar))
            {
                // デフォルトアバター
                var index = ulong.TryParse(Id, out var id) ? (id >> 22) % 6 : 0;
                return $"https://cdn.discordapp.com/embed/avatars/{index}.png";
            }
            var ext = Avatar.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
            return $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.{ext}?size=256";
        }
    }

    /// <summary>バナーURL（バナーがない場合は null）</summary>
    public string? BannerUrl
    {
        get
        {
            if (string.IsNullOrEmpty(Banner)) return null;
            var ext = Banner.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
            return $"https://cdn.discordapp.com/banners/{Id}/{Banner}.{ext}?size=600";
        }
    }

    /// <summary>クランタグ（なし → null）</summary>
    public string? ClanTag => Clan?.Tag;
}

public class ClanInfo
{
    public string? Tag { get; set; }
}
