namespace DiscordLookup.Models;

/// <summary>
/// Discord APIから取得したユーザー情報を保持するモデル
/// </summary>
public class DiscordUser
{
    [Newtonsoft.Json.JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [Newtonsoft.Json.JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [Newtonsoft.Json.JsonProperty("discriminator")]
    public string? Discriminator { get; set; }

    [Newtonsoft.Json.JsonProperty("global_name")]
    public string? GlobalName { get; set; }

    [Newtonsoft.Json.JsonProperty("avatar")]
    public string? Avatar { get; set; }

    [Newtonsoft.Json.JsonProperty("banner")]
    public string? Banner { get; set; }

    [Newtonsoft.Json.JsonProperty("accent_color")]
    public int? AccentColor { get; set; }

    [Newtonsoft.Json.JsonProperty("banner_color")]
    public string? BannerColor { get; set; }

    [Newtonsoft.Json.JsonProperty("primary_guild")]
    public ClanInfo? PrimaryGuild { get; set; }

    [Newtonsoft.Json.JsonProperty("clan")]
    public ClanInfo? Clan { get; set; }

    [Newtonsoft.Json.JsonProperty("avatar_decoration_data")]
    public AvatarDecorationData? AvatarDecorationData { get; set; }

    [Newtonsoft.Json.JsonProperty("collectibles")]
    public CollectiblesInfo? Collectibles { get; set; }

    [Newtonsoft.Json.JsonProperty("public_flags")]
    public long? PublicFlags { get; set; }

    [Newtonsoft.Json.JsonProperty("flags")]
    public long? Flags { get; set; }

    [Newtonsoft.Json.JsonProperty("bot")]
    public bool? Bot { get; set; }

    [Newtonsoft.Json.JsonProperty("premium_type")]
    public int? PremiumType { get; set; }

    // ── 算出プロパティ ──

    private ClanInfo? EffectiveClan => PrimaryGuild ?? Clan;

    /// <summary>判別子を考慮したユーザー名表示（Pomelo方式は @username、レガシー・Bot は name#0000）</summary>
    public string DisplayUsername =>
        !string.IsNullOrEmpty(Discriminator) && Discriminator != "0"
            ? $"{Username}#{Discriminator}"
            : $"@{Username}";

    /// <summary>Snowflake IDからアカウント作成日時を算出</summary>
    public DateTimeOffset CreatedAt
    {
        get
        {
            if (!ulong.TryParse(Id, out var id) || id == 0) return DateTimeOffset.MinValue;
            var timestamp = id >> 22;
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(timestamp + 1420070400000UL));
        }
    }

    /// <summary>日本時間での作成日時</summary>
    public DateTimeOffset CreatedAtJst => CreatedAt == DateTimeOffset.MinValue
        ? DateTimeOffset.MinValue
        : CreatedAt.ToOffset(TimeSpan.FromHours(9));

    /// <summary>作成からの経過日数</summary>
    public double ElapsedDays => CreatedAt == DateTimeOffset.MinValue
        ? 0.0
        : Math.Max(0.0, (DateTimeOffset.UtcNow - CreatedAt).TotalDays);

    /// <summary>作成からの経過年数</summary>
    public double ElapsedYears => ElapsedDays / 365.2425;

    internal static bool IsSafeAssetSegment(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value != "." &&
        value != ".." &&
        !value.Contains('/') &&
        !value.Contains('\\');

    /// <summary>アバターURL（null の場合はデフォルトアバター）</summary>
    public string AvatarUrl
    {
        get
        {
            if (string.IsNullOrEmpty(Avatar) ||
                !ulong.TryParse(Id, out var avatarUserId) ||
                avatarUserId == 0 ||
                !IsSafeAssetSegment(Avatar))
            {
                // デフォルトアバター (レガシー/Bot は discriminator % 5、Pomelo は (id >> 22) % 6)
                ulong index;
                if (!string.IsNullOrEmpty(Discriminator) && Discriminator != "0" && uint.TryParse(Discriminator, out var disc))
                {
                    index = disc % 5;
                }
                else
                {
                    index = ulong.TryParse(Id, out var id) && id != 0 ? (id >> 22) % 6 : 0;
                }
                return $"https://cdn.discordapp.com/embed/avatars/{index}.png";
            }
            var ext = Avatar.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
            return $"https://cdn.discordapp.com/avatars/{avatarUserId}/{Uri.EscapeDataString(Avatar)}.{ext}?size=256";
        }
    }

    /// <summary>バナーURL（バナーがない場合は null）</summary>
    public string? BannerUrl
    {
        get
        {
            if (string.IsNullOrEmpty(Banner) ||
                !ulong.TryParse(Id, out var bannerUserId) ||
                bannerUserId == 0 ||
                !IsSafeAssetSegment(Banner))
            {
                return null;
            }
            var ext = Banner.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
            return $"https://cdn.discordapp.com/banners/{bannerUserId}/{Uri.EscapeDataString(Banner)}.{ext}?size=600";
        }
    }

    /// <summary>クランタグ（なし・非公開 → null）</summary>
    public string? ClanTag =>
        EffectiveClan?.IdentityEnabled == true && !string.IsNullOrEmpty(EffectiveClan.Tag)
            ? EffectiveClan.Tag
            : null;

    /// <summary>クランバッジ画像URL（なし・非公開 → null）</summary>
    public string? ClanBadgeUrl =>
        EffectiveClan?.IdentityEnabled == true ? EffectiveClan.BadgeUrl : null;

    /// <summary>アバターデコレーション画像URL（なし → null）</summary>
    public string? AvatarDecorationUrl
    {
        get
        {
            if (!IsSafeAssetSegment(AvatarDecorationData?.Asset)) return null;
            return $"https://cdn.discordapp.com/avatar-decoration-presets/{Uri.EscapeDataString(AvatarDecorationData!.Asset!)}.png?size=240&passthrough=true";
        }
    }

    /// <summary>ネームプレート装飾SKU ID（未装備 → null）</summary>
    public string? NameplateSkuId => Collectibles?.Nameplate?.SkuId;

    /// <summary>ネームプレート装飾 アセット識別子（未装備 → null）</summary>
    public string? NameplateAsset => Collectibles?.Nameplate?.Asset;

    /// <summary>ネームプレート装飾 表示名ラベル（未装備 → null）</summary>
    public string? NameplateLabel => Collectibles?.Nameplate?.Label;

    /// <summary>ネームプレート装飾 パレット名（未装備 → null）</summary>
    public string? NameplatePalette => Collectibles?.Nameplate?.Palette;

    /// <summary>ネームプレート装飾 静止画URL（448x84 PNG）</summary>
    public string? NameplateStaticUrl => ulong.TryParse(NameplateSkuId, out var skuId) && skuId != 0
        ? $"https://cdn.discordapp.com/media/v1/collectibles-shop/{skuId}/static"
        : null;

    /// <summary>ネームプレート装飾 アニメーションAPNG URL（448x84 フルアルファ APNG）</summary>
    public string? NameplateAnimatedUrl => ulong.TryParse(NameplateSkuId, out var skuId) && skuId != 0
        ? $"https://cdn.discordapp.com/media/v1/collectibles-shop/{skuId}/animated"
        : null;

    /// <summary>ネームプレート装飾 動画URL（アルファ透過 WebM ループ）</summary>
    public string? NameplateVideoUrl => ulong.TryParse(NameplateSkuId, out var skuId) && skuId != 0
        ? $"https://cdn.discordapp.com/media/v1/collectibles-shop/{skuId}/video"
        : null;

    /// <summary>所持しているDiscord公式バッジ一覧</summary>
    public List<UserBadge> Badges
    {
        get
        {
            var list = new List<UserBadge>();
            long f = (PublicFlags ?? 0) | (Flags ?? 0);

            // 1 << 0: Discord Staff
            if ((f & (1L << 0)) != 0)
                list.Add(new UserBadge("staff", "Discord Staff", "Discord スタッフ", "pack://application:,,,/Assets/Badges/staff.png"));

            // 1 << 1: Partnered Server Owner
            if ((f & (1L << 1)) != 0)
                list.Add(new UserBadge("partnered_server_owner", "Partnered Server Owner", "パートナーサーバーオーナー", "pack://application:,,,/Assets/Badges/partnered_server_owner.png"));

            // 1 << 2: HypeSquad Events
            if ((f & (1L << 2)) != 0)
                list.Add(new UserBadge("hypesquad_events", "HypeSquad Events", "HypeSquad Events メンバー", "pack://application:,,,/Assets/Badges/hypesquad_events.png"));

            // 1 << 3: Bug Hunter Level 1
            if ((f & (1L << 3)) != 0)
                list.Add(new UserBadge("bug_hunter_level_1", "Bug Hunter Level 1", "Discord Bug Hunter (Level 1)", "pack://application:,,,/Assets/Badges/bug_hunter_level_1.png"));

            // 1 << 6: HypeSquad Bravery
            if ((f & (1L << 6)) != 0)
                list.Add(new UserBadge("hypesquad_bravery", "HypeSquad Bravery", "HypeSquad Bravery ハウス", "pack://application:,,,/Assets/Badges/hypesquad_bravery.png"));

            // 1 << 7: HypeSquad Brilliance
            if ((f & (1L << 7)) != 0)
                list.Add(new UserBadge("hypesquad_brilliance", "HypeSquad Brilliance", "HypeSquad Brilliance ハウス", "pack://application:,,,/Assets/Badges/hypesquad_brilliance.png"));

            // 1 << 8: HypeSquad Balance
            if ((f & (1L << 8)) != 0)
                list.Add(new UserBadge("hypesquad_balance", "HypeSquad Balance", "HypeSquad Balance ハウス", "pack://application:,,,/Assets/Badges/hypesquad_balance.png"));

            // 1 << 9: Early Supporter
            if ((f & (1L << 9)) != 0)
                list.Add(new UserBadge("early_supporter", "Early Supporter", "初期サポーター (Early Supporter)", "pack://application:,,,/Assets/Badges/early_supporter.png"));

            // 1 << 14: Bug Hunter Level 2
            if ((f & (1L << 14)) != 0)
                list.Add(new UserBadge("bug_hunter_level_2", "Bug Hunter Level 2", "Discord Bug Hunter (Level 2)", "pack://application:,,,/Assets/Badges/bug_hunter_level_2.png"));

            // 1 << 17: Early Verified Bot Developer
            if ((f & (1L << 17)) != 0)
                list.Add(new UserBadge("early_verified_developer", "Early Verified Bot Developer", "初期認証済みBot開発者", "pack://application:,,,/Assets/Badges/early_verified_developer.png"));

            // 1 << 18: Certified Moderator
            if ((f & (1L << 18)) != 0)
                list.Add(new UserBadge("certified_moderator", "Discord Certified Moderator", "Discord認定モデレーター (ALUMNI)", "pack://application:,,,/Assets/Badges/certified_moderator.png"));

            // 1 << 22: Active Developer
            if ((f & (1L << 22)) != 0)
                list.Add(new UserBadge("active_developer", "Active Developer", "アクティブ開発者 (Active Developer)", "pack://application:,,,/Assets/Badges/active_developer.png"));

            // Nitro 判定 (PremiumType > 0、またはアニメーションアバター/アニメーションバナー/デコレーション/ネームプレート所持)
            bool isNitro = (PremiumType.HasValue && PremiumType.Value > 0) ||
                           (!string.IsNullOrEmpty(Avatar) && Avatar.StartsWith("a_", StringComparison.Ordinal)) ||
                           (!string.IsNullOrEmpty(Banner) && Banner.StartsWith("a_", StringComparison.Ordinal)) ||
                           (AvatarDecorationData != null && !string.IsNullOrEmpty(AvatarDecorationData.Asset)) ||
                           (!string.IsNullOrEmpty(NameplateSkuId));

            if (isNitro)
            {
                list.Add(new UserBadge("nitro", "Discord Nitro", "Discord Nitro サブスクライバー", "pack://application:,,,/Assets/Badges/nitro.png"));
            }

            return list;
        }
    }
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
    public string? Asset { get; set; }

    [Newtonsoft.Json.JsonProperty("sku_id")]
    public string? SkuId { get; set; }
}

public class ClanInfo
{
    [Newtonsoft.Json.JsonProperty("identity_guild_id")]
    public string? IdentityGuildId { get; set; }

    [Newtonsoft.Json.JsonProperty("identity_enabled")]
    public bool? IdentityEnabled { get; set; }

    [Newtonsoft.Json.JsonProperty("tag")]
    public string? Tag { get; set; }

    [Newtonsoft.Json.JsonProperty("badge")]
    public string? Badge { get; set; }

    public string? BadgeUrl => ulong.TryParse(IdentityGuildId, out var guildId) && guildId != 0 && DiscordUser.IsSafeAssetSegment(Badge)
        ? $"https://cdn.discordapp.com/guild-tag-badges/{guildId}/{Uri.EscapeDataString(Badge!)}.png"
        : null;
}

public class UserBadge
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconAssetPath { get; set; } = string.Empty;

    public UserBadge() { }

    public UserBadge(string id, string name, string description, string iconAssetPath)
    {
        Id = id;
        Name = name;
        Description = description;
        IconAssetPath = iconAssetPath;
    }
}
