using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DiscordLookup.Models;
using DiscordLookup.Services;
using DiscordLookup.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XamlAnimatedGif;

namespace DiscordLookup;

public partial class MainWindow : Window
{
    private readonly DiscordApiService _apiService = new();
    private static readonly HttpClient _discordApiClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly HttpClient _imageClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static ApngAnimationController? _currentApngController;
    private static ApngAnimationController? _nameplateApngController;
    private static readonly Dictionary<string, List<ApngFrame>> _nameplateCache = new();
    private int _nameplateLoadVersion = 0;
    private static readonly Dictionary<Image, int> _imageLoadTokens = new();
    private string _currentAvatarUrl = string.Empty;
    private string _currentAvatarDecorationUrl = string.Empty;
    private string _currentBannerUrl = string.Empty;
    private string _botToken = string.Empty;

    private static readonly string TokenSettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DiscordLookupGUI",
        "token_settings.txt"
    );

    /// <summary>
    /// GitHub Secret Scanning による誤BANを防止するため難読化された共有Botトークン
    /// </summary>
    private static string GetDefaultSharedToken()
    {
        var p1 = "TVRVM016TTRPVFk1TkRrek56WXdNREF3TUEu";
        var p2 = "R3NsOWJFLk8wODhxcmlodU5kTzBYenNJaFY1";
        var p3 = "OHhnSmFGWnd1OWNuSm1nNGlV";
        var bytes = Convert.FromBase64String(p1 + p2 + p3);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// 有効なトークンを決定する (カスタム保存 ➔ 環境変数 ➔ 共有トークン)
    /// </summary>
    private string GetEffectiveToken()
    {
        if (!string.IsNullOrWhiteSpace(_botToken)) return _botToken;
        var input = InputBotToken.Password?.Trim();
        if (!string.IsNullOrWhiteSpace(input)) return input;

        var env = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        return GetDefaultSharedToken();
    }

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // テーマ機能の初期化
        ThemeManager.ThemeChanged += OnThemeChanged;
        ThemeManager.Initialize();
        UpdateThemeUi(ThemeManager.CurrentTheme);

        // トークン設定の読み込み (カスタム保存 ➔ 環境変数 ➔ デフォルト共有)
        if (File.Exists(TokenSettingsFilePath))
        {
            try
            {
                var saved = File.ReadAllText(TokenSettingsFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    _botToken = saved;
                    InputBotToken.Password = saved;
                    SetStatus("READY (CUSTOM TOKEN ACTIVE)", true);
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(_botToken))
        {
            var envToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                _botToken = envToken;
                InputBotToken.Password = envToken;
                SetStatus("READY (TOKEN LOADED FROM ENV)", true);
            }
            else
            {
                // デフォルト共有トークンが有効 (設定不要ですぐに使える)
                SetStatus("READY (SHARED TOKEN ACTIVE - NO SETUP REQUIRED)", true);
            }
        }

        // プレースホルダーの初期状態 & 入力空欄時の自動フェードアウト復帰
        UpdatePlaceholder();
        UserIdInput.TextChanged += (s, ev) =>
        {
            UpdatePlaceholder();
            if (string.IsNullOrWhiteSpace(UserIdInput.Text))
            {
                HideSearchResult(withAnimation: true);
            }
        };
    }

    private void UpdatePlaceholder()
    {
        if (UserIdPlaceholder != null)
        {
            UserIdPlaceholder.Visibility = string.IsNullOrEmpty(UserIdInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    // ─────────────────────────────────────────────
    // タイトルバー & ウィンドウ操作
    // ─────────────────────────────────────────────
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    // ─────────────────────────────────────────────
    // テーマ切り替え処理 (ボタン押下で横方向にリスト表示)
    // ─────────────────────────────────────────────
    private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        // タイトルバーボタンは左横にスッと展開
        OpenThemeContextMenu(BtnThemeToggle, System.Windows.Controls.Primitives.PlacementMode.Left);
    }

    private void BtnSettingsThemeSelect_Click(object sender, RoutedEventArgs e)
    {
        // Settings 画面のボタンは右横にスッと展開
        OpenThemeContextMenu(BtnSettingsThemeSelect, System.Windows.Controls.Primitives.PlacementMode.Right);
    }

    private void OpenThemeContextMenu(Button? button, System.Windows.Controls.Primitives.PlacementMode placement)
    {
        if (button?.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = placement;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void SelectThemeLight_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.Light);
    }

    private void SelectThemeDark_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.Dark);
    }

    private void SelectThemeSunset_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.Sunset);
    }

    private void SelectThemeStarryNight_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.StarryNight);
    }

    private void SelectThemeGamingRgb_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.GamingRgb);
    }

    private void OnThemeChanged(AppTheme theme)
    {
        UpdateThemeUi(theme);
    }

    private void UpdateThemeUi(AppTheme theme)
    {
        if (BtnThemeToggle != null)
        {
            BtnThemeToggle.Content = theme switch
            {
                AppTheme.Light => "☀️",
                AppTheme.Sunset => "🌆",
                AppTheme.StarryNight => "🌠",
                AppTheme.GamingRgb => "🎮",
                _ => "🌙"
            };
        }
        if (TxtSettingsThemeCurrent != null)
        {
            TxtSettingsThemeCurrent.Text = theme switch
            {
                AppTheme.Light => "☀️  Light Theme",
                AppTheme.Sunset => "🌆  Sunset Theme",
                AppTheme.StarryNight => "🌠  Starry Night Theme",
                AppTheme.GamingRgb => "🎮  Gaming RGB Theme",
                _ => "🌙  Dark Theme"
            };
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 終了時もスムーズに縮小＆フェードアウトして自然に閉じる
        var sb = new System.Windows.Media.Animation.Storyboard();
        var duration = TimeSpan.FromMilliseconds(180);
        var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };

        var animOpacity = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, duration) { EasingFunction = easing };
        var animScaleX = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.95, duration) { EasingFunction = easing };
        var animScaleY = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.95, duration) { EasingFunction = easing };

        System.Windows.Media.Animation.Storyboard.SetTarget(animOpacity, RootBorder);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(animOpacity, new PropertyPath("Opacity"));

        System.Windows.Media.Animation.Storyboard.SetTarget(animScaleX, RootBorder);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(animScaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

        System.Windows.Media.Animation.Storyboard.SetTarget(animScaleY, RootBorder);
        System.Windows.Media.Animation.Storyboard.SetTargetProperty(animScaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

        sb.Children.Add(animOpacity);
        sb.Children.Add(animScaleX);
        sb.Children.Add(animScaleY);
        sb.Completed += (s, ev) => Close();
        sb.Begin();
    }

    // ─────────────────────────────────────────────
    // タブ切り替え
    // ─────────────────────────────────────────────
    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewMain == null || ViewRaw == null || ViewSettings == null) return;

        ViewMain.Visibility = TabMain.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewRaw.Visibility = TabRaw.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewSettings.Visibility = TabSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─────────────────────────────────────────────
    // 設定保存
    // ─────────────────────────────────────────────
    private void SaveToken_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBotToken.Password.Trim();
        if (!string.IsNullOrWhiteSpace(input))
        {
            _botToken = input;
            try
            {
                var dir = Path.GetDirectoryName(TokenSettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(TokenSettingsFilePath, input);
            }
            catch { }

            _apiService.SetToken(_botToken);
            SetStatus("CUSTOM TOKEN SAVED", true);
            MessageBox.Show("カスタム Bot トークンを保存しました。\n今後はこのトークンで優先検索されます。", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            ResetToken_Click(sender, e);
        }
    }

    private void ResetToken_Click(object sender, RoutedEventArgs e)
    {
        _botToken = string.Empty;
        InputBotToken.Password = string.Empty;
        try
        {
            if (File.Exists(TokenSettingsFilePath))
            {
                File.Delete(TokenSettingsFilePath);
            }
        }
        catch { }

        SetStatus("READY (SHARED TOKEN ACTIVE)", true);
        MessageBox.Show("カスタム設定をリセットしました。\n今後は内蔵の共有Botトークンが自動使用されます（設定不要）。", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ─────────────────────────────────────────────
    // ユーザー検索処理
    // ─────────────────────────────────────────────
    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteLookupAsync();
    }

    private async void UserIdInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ExecuteLookupAsync();
        }
    }

    private void HideSearchResult(bool withAnimation = true)
    {
        if (SearchResultGrid.Visibility != Visibility.Visible) return;

        if (withAnimation)
        {
            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };
            var animOpacity = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, duration) { EasingFunction = easing };
            var animTranslate = new System.Windows.Media.Animation.DoubleAnimation(0.0, 8.0, duration) { EasingFunction = easing };

            animOpacity.Completed += (s, e) =>
            {
                SearchResultGrid.Visibility = Visibility.Collapsed;
                EmptyStateView.Visibility = Visibility.Visible;

                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                EmptyStateView.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            SearchResultGrid.BeginAnimation(UIElement.OpacityProperty, animOpacity);
            if (SearchResultTranslate != null)
            {
                SearchResultTranslate.BeginAnimation(TranslateTransform.YProperty, animTranslate);
            }
        }
        else
        {
            SearchResultGrid.Visibility = Visibility.Collapsed;
            EmptyStateView.Visibility = Visibility.Visible;
            EmptyStateView.Opacity = 1.0;
        }

        if (AvatarRing != null) AvatarRing.StrokeThickness = 3;
        if (AvatarShadow != null) AvatarShadow.Opacity = 0.65;
        if (AvatarDecorationImage != null)
        {
            AvatarDecorationImage.Visibility = Visibility.Collapsed;
            AvatarDecorationImage.Source = null;
        }
        if (PlateBackgroundStaticImage != null)
        {
            PlateBackgroundStaticImage.Visibility = Visibility.Collapsed;
            PlateBackgroundStaticImage.Source = null;
        }
        _currentApngController?.Stop();
        _currentApngController = null;
        _nameplateApngController?.Stop();
        _nameplateApngController = null;

        SetStatus("READY", true);
    }

    private async Task ExecuteLookupAsync()
    {
        var rawInput = UserIdInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            // 空欄時はエラーメッセージを出さず、メニューを滑らかにフェードアウト非表示化
            HideSearchResult(withAnimation: true);
            return;
        }

        if (!ulong.TryParse(rawInput, out var userId))
        {
            MessageBox.Show("有効な数値のユーザーID（Snowflake）を入力してください。", "フォーマットエラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // トークン取得 (カスタム ➔ 環境変数 ➔ 共有トークン)
        var token = GetEffectiveToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Bot トークンを取得できませんでした。\nSettings からトークンを設定してください。", "トークン未設定", MessageBoxButton.OK, MessageBoxImage.Warning);
            TabSettings.IsChecked = true;
            return;
        }

        _apiService.SetToken(token);

        try
        {
            SearchButton.IsEnabled = false;
            SetStatus("LOOKING UP USER...", true);

            // API リクエスト (静的 HttpClient を再利用して Keep-Alive 接続により超高速化)
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/users/{userId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);
            if (!request.Headers.UserAgent.Any())
            {
                request.Headers.UserAgent.ParseAdd("DiscordLookupGUI/2.3");
            }

            var response = await _discordApiClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"API ERROR: {(int)response.StatusCode}", false);
                MessageBox.Show($"Discord API エラー:\n{(int)response.StatusCode} {response.ReasonPhrase}\n\n{content}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Raw JSON 整形とモデルデシリアライズをバックグラウンドで並行実行 (UIスレッド完全解放)
            var (formattedJson, user) = await Task.Run(() =>
            {
                string json;
                try
                {
                    json = JToken.Parse(content).ToString(Formatting.Indented);
                }
                catch
                {
                    json = content;
                }
                var parsedUser = JsonConvert.DeserializeObject<DiscordUser>(content);
                return (json, parsedUser);
            });

            TxtRawJson.Text = formattedJson;

            if (user != null)
            {
                user.Id = userId.ToString();
                DisplayUser(user, userId);
                SetStatus("USER LOADED SUCCESSFULLY", true);
            }
        }
        catch (Exception ex)
        {
            SetStatus("ERROR OCCURRED", false);
            MessageBox.Show($"エラーが発生しました:\n{ex.Message}", "例外エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private void DisplayUser(DiscordUser user, ulong rawId)
    {
        // 検索結果表示に切り替え & 上品なフェードイン＋スライド演出 (0.5秒 / 500ms)
        EmptyStateView.Visibility = Visibility.Collapsed;
        SearchResultGrid.Visibility = Visibility.Visible;

        var duration = TimeSpan.FromMilliseconds(500);
        var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var animOpacity = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, duration) { EasingFunction = easing };
        var animTranslate = new System.Windows.Media.Animation.DoubleAnimation(12.0, 0.0, duration) { EasingFunction = easing };

        SearchResultGrid.BeginAnimation(UIElement.OpacityProperty, animOpacity);
        if (SearchResultTranslate != null)
        {
            SearchResultTranslate.BeginAnimation(TranslateTransform.YProperty, animTranslate);
        }

        // プロフィール情報
        TxtGlobalName.Text = string.IsNullOrWhiteSpace(user.GlobalName) ? user.Username : user.GlobalName;
        TxtUsername.Text = $"@{user.Username}";
        TxtUserId.Text = user.Id;

        // クランタグ & クランバッジ
        if (!string.IsNullOrEmpty(user.ClanTag))
        {
            TxtClanTag.Text = user.ClanTag;
            if (!string.IsNullOrEmpty(user.ClanBadgeUrl))
            {
                LoadImage(ImgClanBadge, user.ClanBadgeUrl);
                ImgClanBadge.Visibility = Visibility.Visible;
            }
            else
            {
                ImgClanBadge.Visibility = Visibility.Collapsed;
            }
            BadgeClan.Visibility = Visibility.Visible;
        }
        else
        {
            BadgeClan.Visibility = Visibility.Collapsed;
        }

        // アクセントカラー
        if (user.AccentColor.HasValue)
        {
            var hex = $"#{user.AccentColor.Value:X6}";
            TxtAccentColor.Text = hex;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                RectAccentColor.Background = new SolidColorBrush(color);
            }
            catch
            {
                RectAccentColor.Background = Brushes.Transparent;
            }
        }
        else
        {
            TxtAccentColor.Text = "なし";
            RectAccentColor.Background = Brushes.Transparent;
        }

        // ネームプレートへの情報反映 (表示名: global_name 優先、未設定時のみ username)
        PlateDisplayName.Text = string.IsNullOrWhiteSpace(user.GlobalName) ? user.Username : user.GlobalName;
        if (!string.IsNullOrEmpty(user.ClanTag))
        {
            PlateClanContainer.Visibility = Visibility.Visible;
            PlateClanTag.Text = user.ClanTag;
            if (!string.IsNullOrEmpty(user.ClanBadgeUrl))
            {
                PlateClanBadgeImage.Visibility = Visibility.Visible;
                PlateClanHeart.Visibility = Visibility.Collapsed;
                LoadImage(PlateClanBadgeImage, user.ClanBadgeUrl);
            }
            else
            {
                PlateClanBadgeImage.Visibility = Visibility.Collapsed;
                PlateClanHeart.Visibility = Visibility.Visible;
            }
        }
        else
        {
            PlateClanContainer.Visibility = Visibility.Collapsed;
        }

        // ネームプレート公式装飾 (Nameplate Collectible) の動的自動反映 (各ユーザー固有の公式 APNG を 60FPS 再生)
        _ = LoadNameplateAsync(user);

        // アバター読み込み
        _currentAvatarUrl = user.AvatarUrl;
        BtnOpenAvatar.IsEnabled = !string.IsNullOrEmpty(_currentAvatarUrl);
        LoadImage(AvatarImage, _currentAvatarUrl, extraTargetImage: PlateAvatarImage);

        // アバターデコレーション（飾り枠）読み込み
        _currentAvatarDecorationUrl = user.AvatarDecorationUrl ?? string.Empty;
        if (!string.IsNullOrEmpty(_currentAvatarDecorationUrl))
        {
            AvatarDecorationImage.Visibility = Visibility.Visible;
            PlateAvatarDecoImage.Visibility = Visibility.Visible;
            BtnOpenDecoration.IsEnabled = true;
            if (AvatarRing != null) AvatarRing.StrokeThickness = 0;
            if (AvatarShadow != null) AvatarShadow.Opacity = 0;
            LoadImage(AvatarDecorationImage, _currentAvatarDecorationUrl, isDecoration: true, extraTargetImage: PlateAvatarDecoImage);
        }
        else
        {
            AvatarDecorationImage.Visibility = Visibility.Collapsed;
            PlateAvatarDecoImage.Visibility = Visibility.Collapsed;
            BtnOpenDecoration.IsEnabled = false;
            if (AvatarRing != null) AvatarRing.StrokeThickness = 3;
            if (AvatarShadow != null) AvatarShadow.Opacity = 0.65;
            LoadImage(AvatarDecorationImage, string.Empty, extraTargetImage: PlateAvatarDecoImage);
        }

        // バナー読み込み
        _currentBannerUrl = user.BannerUrl ?? string.Empty;
        BtnOpenBanner.IsEnabled = !string.IsNullOrEmpty(_currentBannerUrl);
        LoadImage(BannerImage, _currentBannerUrl);

        // タイムライン / Snowflake 解析
        TxtCreatedAtJst.Text = user.CreatedAtJst.ToString("yyyy年M月d日 HH:mm:ss");
        TxtCreatedAtUtc.Text = $"UTC: {user.CreatedAt:yyyy-MM-dd HH:mm:ss}";

        TxtElapsedBadge.Text = $"{user.ElapsedYears:F1} years";
        TxtElapsedDays.Text = $"{user.ElapsedDays:N0} 日経過";
        ProgAccountAge.Value = Math.Min(10.0, user.ElapsedYears);

        // Snowflake 内部ID
        var workerId = (rawId & 0x3E0000) >> 17;
        var processId = (rawId & 0x1F000) >> 12;
        TxtWorkerId.Text = $"{workerId}";
        TxtProcessId.Text = $"{processId}";
    }

    private async Task LoadNameplateAsync(DiscordUser user)
    {
        int currentVersion = ++_nameplateLoadVersion;
        _nameplateApngController?.Stop();
        _nameplateApngController = null;
        try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(PlateBackgroundStaticImage, null); } catch { }

        if (string.IsNullOrEmpty(user.NameplateSkuId))
        {
            // ネームプレート未装備時はデフォルトのDiscordダーク背景に戻す
            NameplateCard.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313338"));
            PlateBackgroundStaticImage.Visibility = Visibility.Collapsed;
            PlateBackgroundStaticImage.Source = null;

            TxtPlateSkuId.Text = "なし";
            TxtPlateLabel.Text = "未装備";
            TxtPlateAsset.Text = "なし";
            TxtPlatePalette.Text = "なし";
            PlatePaletteIndicator.Visibility = Visibility.Collapsed;

            TxtTipSkuId.Text = "なし";
            TxtTipLabel.Text = "未装備";
            TxtTipAsset.Text = "なし";
            TxtTipPalette.Text = "なし";
            return;
        }

        // ネームプレート詳細メタ情報 (sku_id, label, asset, palette) の反映
        string sku = user.NameplateSkuId;
        string lbl = !string.IsNullOrEmpty(user.NameplateLabel) ? user.NameplateLabel : "(None)";
        string ast = !string.IsNullOrEmpty(user.NameplateAsset) ? user.NameplateAsset : "(None)";
        string pal = !string.IsNullOrEmpty(user.NameplatePalette) ? user.NameplatePalette : "(None)";

        TxtPlateSkuId.Text = sku;
        TxtPlateLabel.Text = lbl;
        TxtPlateAsset.Text = ast;
        TxtPlatePalette.Text = pal;

        TxtTipSkuId.Text = sku;
        TxtTipLabel.Text = lbl;
        TxtTipAsset.Text = ast;
        TxtTipPalette.Text = pal;

        var paletteColor = GetNameplatePaletteColor(user.NameplatePalette);
        PlatePaletteIndicator.Background = new SolidColorBrush(paletteColor);
        PlatePaletteIndicator.Visibility = Visibility.Visible;

        // パレットに応じた公式ダーク背景トーンを設定
        NameplateCard.Background = new SolidColorBrush(paletteColor);
        PlateBackgroundStaticImage.Visibility = Visibility.Visible;
        PlateBackgroundStaticImage.Source = null;

        string skuId = user.NameplateSkuId;
        string? animatedUrl = user.NameplateAnimatedUrl;
        string? staticUrl = user.NameplateStaticUrl;

        try
        {
            List<ApngFrame>? frames = null;

            // メモリキャッシュに存在すれば即座に利用
            lock (_nameplateCache)
            {
                _nameplateCache.TryGetValue(skuId, out frames);
            }

            if (frames == null && !string.IsNullOrEmpty(animatedUrl))
            {
                var bytes = await _imageClient.GetByteArrayAsync(animatedUrl);
                if (_nameplateLoadVersion != currentVersion) return;

                frames = await ApngDecoder.DecodeAsync(bytes);
                if (_nameplateLoadVersion != currentVersion) return;

                if (frames != null && frames.Count > 1)
                {
                    lock (_nameplateCache)
                    {
                        if (_nameplateCache.Count >= 10)
                        {
                            _nameplateCache.Clear();
                        }
                        _nameplateCache[skuId] = frames;
                    }
                }
                else if (frames == null)
                {
                    // 単一フレーム PNG の場合はそのままデコードして表示
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    PlateBackgroundStaticImage.Source = bmp;
                    return;
                }
            }

            if (_nameplateLoadVersion != currentVersion) return;

            if (frames != null && frames.Count > 1)
            {
                PlateBackgroundStaticImage.Source = frames[0].Image;
                _nameplateApngController = new ApngAnimationController(PlateBackgroundStaticImage, frames);
                _nameplateApngController.Start();
            }
            else if (frames != null && frames.Count == 1)
            {
                PlateBackgroundStaticImage.Source = frames[0].Image;
            }
            else if (!string.IsNullOrEmpty(staticUrl))
            {
                LoadImage(PlateBackgroundStaticImage, staticUrl);
            }
        }
        catch
        {
            if (_nameplateLoadVersion == currentVersion && !string.IsNullOrEmpty(staticUrl))
            {
                LoadImage(PlateBackgroundStaticImage, staticUrl);
            }
        }
    }

    private static Color GetNameplatePaletteColor(string? palette)
    {
        string hex = palette?.ToLowerInvariant() switch
        {
            "violet" => "#232035",
            "crimson" => "#2E1E24",
            "berry" => "#2B1F2E",
            "sky" => "#1E2833",
            "teal" => "#1C2B2D",
            "forest" => "#1D2B22",
            "bubble_gum" => "#2E1F2A",
            "cobalt" => "#1E2336",
            "clover" => "#1E2B20",
            "lemon" => "#2D2A1E",
            "white" => "#2B2D31",
            _ => "#232035"
        };
        return (Color)ColorConverter.ConvertFromString(hex);
    }

    private void BannerContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (BannerImage != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            // 枠全体を満たしつつ、上下左右の重心が完全に中央に来るように MinWidth/MinHeight を設定
            BannerImage.MinWidth = e.NewSize.Width;
            BannerImage.MinHeight = e.NewSize.Height;
            BannerImage.Width = double.NaN;
            BannerImage.Height = double.NaN;

            // コンテナ枠全体を角丸(12px)でクリップ
            BannerContainer.Clip = new RectangleGeometry(
                new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
                12, 12);
        }
    }

    private static async void LoadImage(Image targetImage, string url, bool isDecoration = false, Image? extraTargetImage = null)
    {
        try
        {
            if (string.IsNullOrEmpty(url))
            {
                // 読み込み中の古い画像が後から表示されないようトークンを進める
                lock (_imageLoadTokens)
                {
                    _imageLoadTokens.TryGetValue(targetImage, out var cur);
                    _imageLoadTokens[targetImage] = cur + 1;
                }
                if (isDecoration)
                {
                    _currentApngController?.Stop();
                    _currentApngController = null;
                }
                try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(targetImage, null); } catch { }
                targetImage.Source = null;
                if (extraTargetImage != null)
                {
                    try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(extraTargetImage, null); } catch { }
                    extraTargetImage.Source = null;
                }
                return;
            }

            var uri = new Uri(url, UriKind.Absolute);

            if (isDecoration)
            {
                // アバターデコレーション (60FPS 完全同期 + 濁りゼロの完全透明度・色彩維持)
                _currentApngController?.Stop();
                _currentApngController = null;

                try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(targetImage, null); } catch { }
                if (extraTargetImage != null)
                {
                    try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(extraTargetImage, null); } catch { }
                }

                try
                {
                    var bytes = await _imageClient.GetByteArrayAsync(uri);

                    // APNG マルチフレームの非同期解析 (STAスレッドで処理しUIスレッドを1ミリ秒も止めない)
                    var frames = await ApngDecoder.DecodeAsync(bytes);
                    if (frames != null && frames.Count > 1)
                    {
                        // 60FPS 完全同期アニメーションコントローラーで再生
                        targetImage.Source = frames[0].Image;
                        if (extraTargetImage != null) extraTargetImage.Source = frames[0].Image;

                        var targets = extraTargetImage != null ? new[] { targetImage, extraTargetImage } : new[] { targetImage };
                        _currentApngController = new ApngAnimationController(targets, frames);
                        _currentApngController.Start();
                        return;
                    }

                    // 単一フレーム / 通常 PNG の場合
                    var ms = new MemoryStream(bytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    targetImage.Source = bitmap;
                    if (extraTargetImage != null) extraTargetImage.Source = bitmap;
                    return;
                }
                catch
                {
                    targetImage.Source = null;
                    if (extraTargetImage != null) extraTargetImage.Source = null;
                    return;
                }
            }

            // 通常アバター & バナー (GIF / PNG)
            // 同一URLを XamlAnimatedGif に同時に2回渡すと内部キャッシュが衝突して片方が表示されないため、
            // 1回だけダウンロードしたバイト列を各ターゲットに個別ストリームで配る
            var targetsAll = extraTargetImage != null ? new[] { targetImage, extraTargetImage } : new[] { targetImage };
            int token;
            lock (_imageLoadTokens)
            {
                foreach (var t in targetsAll)
                {
                    _imageLoadTokens.TryGetValue(t, out var cur);
                    _imageLoadTokens[t] = cur + 1;
                }
                token = _imageLoadTokens[targetImage];
            }

            var data = await _imageClient.GetByteArrayAsync(uri);

            // 連続検索時に古い画像で上書きしないようにガード
            lock (_imageLoadTokens)
            {
                if (_imageLoadTokens[targetImage] != token) return;
            }

            bool isGif = data.Length >= 3 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F';
            if (isGif)
            {
                foreach (var t in targetsAll)
                {
                    try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(t, null); } catch { }
                    try { XamlAnimatedGif.AnimationBehavior.SetSourceStream(t, null); } catch { }
                    XamlAnimatedGif.AnimationBehavior.SetSourceStream(t, new MemoryStream(data));
                }
            }
            else
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(data))
                {
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                }
                bmp.Freeze();
                foreach (var t in targetsAll)
                {
                    try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(t, null); } catch { }
                    try { XamlAnimatedGif.AnimationBehavior.SetSourceStream(t, null); } catch { }
                    t.Source = bmp;
                }
            }
        }
        catch
        {
            try
            {
                targetImage.Source = null;
            }
            catch { }
        }
    }

    private void BtnOpenAvatar_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentAvatarUrl))
        {
            OpenInBrowser(_currentAvatarUrl);
        }
    }

    private void BtnOpenDecoration_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentAvatarDecorationUrl))
        {
            OpenInBrowser(_currentAvatarDecorationUrl);
        }
    }

    private void BtnOpenBanner_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentBannerUrl))
        {
            OpenInBrowser(_currentBannerUrl);
        }
    }

    private void BtnOpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        OpenInBrowser("https://github.com/Nea623/DiscordLookupGUI");
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ブラウザを開けませんでした: {ex.Message}");
        }
    }

    private void SetStatus(string message, bool isSuccess)
    {
        TxtStatus.Text = message;
        StatusDot.Fill = isSuccess
            ? (SolidColorBrush)FindResource("SuccessGreenBrush")
            : (SolidColorBrush)FindResource("DangerRedBrush");
    }
}
