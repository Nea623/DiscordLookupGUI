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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XamlAnimatedGif;

namespace DiscordLookup;

public partial class MainWindow : Window
{
    private readonly DiscordApiService _apiService = new();
    private string _currentAvatarUrl = string.Empty;
    private string _currentBannerUrl = string.Empty;
    private string _botToken = string.Empty;

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

        // 1. 環境変数 DISCORD_BOT_TOKEN から初期トークンを読み込む
        var envToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            _botToken = envToken;
            InputBotToken.Password = envToken;
            SetStatus("READY (TOKEN LOADED FROM ENV)", true);
        }
        else
        {
            SetStatus("WARNING: BOT TOKEN NOT SET (CHECK SETTINGS)", false);
        }

        // プレースホルダーの初期状態
        UpdatePlaceholder();
        UserIdInput.TextChanged += (s, ev) => UpdatePlaceholder();
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
        _botToken = InputBotToken.Password.Trim();
        if (!string.IsNullOrWhiteSpace(_botToken))
        {
            _apiService.SetToken(_botToken);
            SetStatus("TOKEN SAVED", true);
            MessageBox.Show("Bot トークンを保存しました。", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            SetStatus("TOKEN CLEARED", false);
        }
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

    private async Task ExecuteLookupAsync()
    {
        var rawInput = UserIdInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            MessageBox.Show("ユーザーIDを入力してください。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!ulong.TryParse(rawInput, out var userId))
        {
            MessageBox.Show("有効な数値のユーザーID（Snowflake）を入力してください。", "フォーマットエラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // トークン確認
        var token = !string.IsNullOrWhiteSpace(_botToken) ? _botToken : InputBotToken.Password.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Discord Bot トークンが設定されていません。\n右上の「Settings」タブから設定してください。", "トークン未設定", MessageBoxButton.OK, MessageBoxImage.Warning);
            TabSettings.IsChecked = true;
            return;
        }

        _apiService.SetToken(token);

        try
        {
            SearchButton.IsEnabled = false;
            SetStatus("LOOKING UP USER...", true);

            // API リクエスト
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);

            var response = await client.GetAsync($"https://discord.com/api/v10/users/{userId}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"API ERROR: {(int)response.StatusCode}", false);
                MessageBox.Show($"Discord API エラー:\n{(int)response.StatusCode} {response.ReasonPhrase}\n\n{content}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Raw JSON をインデントして整形表示
            var parsedJson = JToken.Parse(content);
            TxtRawJson.Text = parsedJson.ToString(Formatting.Indented);

            // モデルへデシリアライズ
            var user = JsonConvert.DeserializeObject<DiscordUser>(content);
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
        // 検索結果表示に切り替え
        EmptyStateView.Visibility = Visibility.Collapsed;
        SearchResultGrid.Visibility = Visibility.Visible;

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

        // アバター読み込み
        _currentAvatarUrl = user.AvatarUrl;
        BtnOpenAvatar.IsEnabled = !string.IsNullOrEmpty(_currentAvatarUrl);
        LoadImage(AvatarImage, _currentAvatarUrl);

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

    private static void LoadImage(Image targetImage, string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url))
            {
                AnimationBehavior.SetSourceUri(targetImage, null);
                targetImage.Source = null;
                return;
            }

            var uri = new Uri(url, UriKind.Absolute);
            // XamlAnimatedGif を使用して GIF アニメーションを滑らかにループ再生
            AnimationBehavior.SetSourceUri(targetImage, uri);
        }
        catch
        {
            try
            {
                AnimationBehavior.SetSourceUri(targetImage, null);
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
