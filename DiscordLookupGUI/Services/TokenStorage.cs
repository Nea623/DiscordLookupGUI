using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DiscordLookup.Services;

/// <summary>
/// Windows DPAPI (Data Protection API) による安全なトークン暗号化ストレージサービス
/// </summary>
public static class TokenStorage
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiscordLookupGUI"
    );

    // DPAPIで暗号化された安全なトークン保存先
    private static readonly string EncryptedFilePath = Path.Combine(SettingsDirectory, "token.dat");
    
    // 従来の平文ファイル (後方互換・自動移行用)
    private static readonly string LegacyFilePath = Path.Combine(SettingsDirectory, "token_settings.txt");

    // アプリ固有のエントロピー（同一ユーザーの他プロセスからの平易な復号を防止）
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DiscordLookupGUI_Token_Entropy_v1");

    /// <summary>
    /// 保存されているトークンを復号して読み込む。
    /// 旧平文ファイルが存在する場合は自動的に暗号化ファイルへ移行し平文を削除する。
    /// </summary>
    public static string? LoadToken()
    {
        // 1. 暗号化ファイルの読み込み
        if (File.Exists(EncryptedFilePath))
        {
            try
            {
                var base64 = File.ReadAllText(EncryptedFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(base64))
                {
                    var encryptedBytes = Convert.FromBase64String(base64);
                    var decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        Entropy,
                        DataProtectionScope.CurrentUser
                    );
                    var token = Encoding.UTF8.GetString(decryptedBytes).Trim();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        return token;
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogException("TokenStorage.LoadToken (Encrypted)", ex);
            }
        }

        // 2. 旧平文ファイルの読み込み＆自動マイグレーション
        if (File.Exists(LegacyFilePath))
        {
            try
            {
                var legacyToken = File.ReadAllText(LegacyFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(legacyToken))
                {
                    try
                    {
                        // 新形式へ暗号化保存して平文ファイルを削除
                        SaveToken(legacyToken);
                    }
                    catch (Exception ex)
                    {
                        App.LogException("TokenStorage.MigrateLegacyToken", ex);
                    }
                    return legacyToken;
                }
            }
            catch (Exception ex)
            {
                App.LogException("TokenStorage.LoadToken (Legacy)", ex);
            }
        }

        return null;
    }

    /// <summary>
    /// トークンを DPAPI で暗号化して保存する。
    /// </summary>
    public static void SaveToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            DeleteToken();
            return;
        }

        if (!Directory.Exists(SettingsDirectory))
        {
            Directory.CreateDirectory(SettingsDirectory);
        }

        var plainBytes = Encoding.UTF8.GetBytes(token.Trim());
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            Entropy,
            DataProtectionScope.CurrentUser
        );

        var base64 = Convert.ToBase64String(encryptedBytes);
        File.WriteAllText(EncryptedFilePath, base64, Encoding.UTF8);

        // 旧平文ファイルが残っていれば安全に消去
        if (File.Exists(LegacyFilePath))
        {
            try
            {
                File.Delete(LegacyFilePath);
            }
            catch { }
        }
    }

    /// <summary>
    /// 保存されているトークンファイルを削除する。
    /// </summary>
    public static void DeleteToken()
    {
        Exception? lastEx = null;

        if (File.Exists(EncryptedFilePath))
        {
            try
            {
                File.Delete(EncryptedFilePath);
            }
            catch (Exception ex)
            {
                lastEx = ex;
                App.LogException("TokenStorage.DeleteToken (Encrypted)", ex);
            }
        }

        if (File.Exists(LegacyFilePath))
        {
            try
            {
                File.Delete(LegacyFilePath);
            }
            catch (Exception ex)
            {
                lastEx ??= ex;
                App.LogException("TokenStorage.DeleteToken (Legacy)", ex);
            }
        }

        if (lastEx != null)
        {
            throw lastEx;
        }
    }
}
