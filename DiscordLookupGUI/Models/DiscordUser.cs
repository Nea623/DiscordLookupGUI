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

    [Newtonsoft.Json.JsonProperty("avatar_decoration_data")]
    public AvatarDecorationData? AvatarDecorationData { get; set; }

    [Newtonsoft.Json.JsonProperty("collectibles")]
    public CollectiblesInfo? Collectibles { get; set; }

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

    /// <summary>クランバッジ画像URL（なし → null）</summary>
    public string? ClanBadgeUrl => Clan?.BadgeUrl;

    /// <summary>アバターデコレーション画像URL（なし → null）</summary>
    public string? AvatarDecorationUrl
    {
        get
        {
            if (string.IsNullOrEmpty(AvatarDecorationData?.Asset)) return null;
            return $"https://cdn.discordapp.com/avatar-decoration-presets/{AvatarDecorationData.Asset}.png?size=240&passthrough=true";
        }
    }

    /// <summary>ネームプレート装飾SKU ID（未装備 → null）</summary>
    public string? NameplateSkuId => Collectibles?.Nameplate?.SkuId;

    /// <summary>ネームプレート装飾 パレット名（未装備 → null）</summary>
    public string? NameplatePalette => Collectibles?.Nameplate?.Palette;

    /// <summary>ネームプレート装飾 静止画URL（448x84 PNG）</summary>
    public string? NameplateStaticUrl => !string.IsNullOrEmpty(NameplateSkuId)
        ? $"https://cdn.discordapp.com/media/v1/collectibles-shop/{NameplateSkuId}/static"
        : null;

    /// <summary>ネームプレート装飾 アニメーションAPNG URL（448x84 フルアルファ APNG）</summary>
    public string? NameplateAnimatedUrl => !string.IsNullOrEmpty(NameplateSkuId)
        ? $"https://cdn.discordapp.com/media/v1/collectibles-shop/{NameplateSkuId}/animated"
        : null;

    /// <summary>ネームプレート装飾 動画URL（アルファ透過 WebM ループ）</summary>
    public string? NameplateVideoUrl => !string.IsNullOrEmpty(NameplateSkuId)
        ? $"https://cdn.discordapp.com/media/v1/collectibles-shop/{NameplateSkuId}/video"
        : null;
}

public class CollectiblesInfo
{
    [Newtonsoft.Json.JsonProperty("nameplate")]
    public NameplateInfo? Nameplate { get; set; }
}

public class NameplateInfo
{
    [Newtonsoft.Json.JsonProperty("sku_id")]
    public string? SkuId { get; set; }

    [Newtonsoft.Json.JsonProperty("asset")]
    public string? Asset { get; set; }

    [Newtonsoft.Json.JsonProperty("label")]
    public string? Label { get; set; }

    [Newtonsoft.Json.JsonProperty("palette")]
    public string? Palette { get; set; }
}

public class AvatarDecorationData
{
    [Newtonsoft.Json.JsonProperty("asset")]
    public string Asset { get; set; } = string.Empty;

    [Newtonsoft.Json.JsonProperty("sku_id")]
    public string? SkuId { get; set; }
}

public class ClanInfo
{
    [Newtonsoft.Json.JsonProperty("identity_guild_id")]
    public string? IdentityGuildId { get; set; }

    [Newtonsoft.Json.JsonProperty("identity_enabled")]
    public bool IdentityEnabled { get; set; }

    [Newtonsoft.Json.JsonProperty("tag")]
    public string? Tag { get; set; }

    [Newtonsoft.Json.JsonProperty("badge")]
    public string? Badge { get; set; }

    public string? BadgeUrl => !string.IsNullOrEmpty(IdentityGuildId) && !string.IsNullOrEmpty(Badge)
        ? $"https://cdn.discordapp.com/clan-badges/{IdentityGuildId}/{Badge}.png"
        : null;
}
