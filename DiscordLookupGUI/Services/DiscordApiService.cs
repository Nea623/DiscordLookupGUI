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
    private readonly HttpClient _client = new();
    private bool _disposed;

    /// <summary>Bot トークンを設定する</summary>
    public void SetToken(string botToken)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bot", botToken);
    }

    /// <summary>ユーザーIDからユーザー情報を取得する</summary>
    public async Task<DiscordUser> GetUserAsync(string userId)
    {
        using var response = await _client.GetAsync($"https://discord.com/api/v10/users/{userId}");

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            throw new HttpRequestException(
                $"Discord API エラー: {status} {response.ReasonPhrase}");
        }

        var json = await response.Content.ReadAsStringAsync();
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
