using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using DiscordLookup.Models;

namespace DiscordLookup.Services;

/// <summary>
/// Discord API v10 を使ってユーザー情報を取得するサービス
/// </summary>
public class DiscordApiService : IDisposable
{
    private readonly HttpClient _client;
    private bool _disposed;

    public DiscordApiService()
    {
        _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordLookupGUI/2.5");
    }

    /// <summary>Bot トークンを設定する</summary>
    public void SetToken(string botToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalized = botToken?.TrimStart() ?? string.Empty;
        if (normalized.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(4);
        }
        else if (string.Equals(normalized.TrimEnd(), "Bot", StringComparison.OrdinalIgnoreCase))
        {
            normalized = string.Empty;
        }
        normalized = normalized.Trim();

        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c > 0x7F))
        {
            throw new ArgumentException("有効な Bot トークンを指定してください。", nameof(botToken));
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bot", normalized);
    }

    /// <summary>ユーザーIDからユーザー情報を取得する</summary>
    public async Task<DiscordUser> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!ulong.TryParse(userId, out var parsedId) || parsedId == 0)
        {
            throw new ArgumentException("有効な数値のユーザーID（Snowflake）を指定してください。", nameof(userId));
        }

        using var response = await _client.GetAsync($"https://discord.com/api/v10/users/{parsedId}", cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            throw new HttpRequestException(
                $"Discord API エラー: {status} {response.ReasonPhrase}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var user = JsonConvert.DeserializeObject<DiscordUser>(json)
                   ?? throw new InvalidOperationException("ユーザー情報のパースに失敗しました。");
        return user;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
