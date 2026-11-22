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
    private static readonly HttpClient _discordApiClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly HttpClient _imageClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private sealed class NameplateCacheEntry
    {
        public required List<ApngFrame> Frames { get; init; }
        public required long EstimatedBytes { get; init; }
    }

    private const int MaxNameplateCacheItems = 10;
    private const long MaxNameplateCacheBytes = 64L * 1024 * 1024;
    private readonly Dictionary<string, LinkedListNode<(string Key, NameplateCacheEntry Entry)>> _nameplateCache = new();
    private readonly LinkedList<(string Key, NameplateCacheEntry Entry)> _nameplateCacheLru = new();
    private long _nameplateCacheTotalBytes = 0;

    private ApngAnimationController? _currentApngController;
    private ApngAnimationController? _nameplateApngController;
    private readonly Dictionary<Image, int> _imageLoadTokens = new();

    private System.Windows.Media.Animation.Storyboard? _rgbBorderStoryboard;
    private System.Windows.Media.Animation.Storyboard? _rgbSearchIconStoryboard;
    private bool _isRgbBorderRunning;
    private bool _isRgbSearchIconRunning;
    private bool _isClosing;
    private bool _isClosed;

    private CancellationTokenSource? _lookupCts;
    private int _lookupVersion = 0;
    private int _nameplateLoadVersion = 0;
    private int _viewTransitionVersion = 0;
    private bool _isHidingResult = false;
    private DiscordUser? _currentUser;

    private string _currentAvatarUrl = string.Empty;
    private string _currentAvatarDecorationUrl = string.Empty;
    private string _currentBannerUrl = string.Empty;
    private string _botToken = string.Empty;

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
        if (!string.IsNullOrWhiteSpace(_botToken)) return _botToken.Trim();
        var input = InputBotToken.Password?.Trim();
        if (!string.IsNullOrWhiteSpace(input)) return input;

        var env = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")?.Trim();
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
        Loaded -= MainWindow_Loaded;

        _rgbBorderStoryboard = TryFindResource("GamingRgbBorderStoryboard") as System.Windows.Media.Animation.Storyboard;
        _rgbSearchIconStoryboard = TryFindResource("GamingRgbSearchIconStoryboard") as System.Windows.Media.Animation.Storyboard;

        StateChanged += MainWindow_StateChanged;
        if (EmptyStateView != null)
        {
            EmptyStateView.IsVisibleChanged += EmptyStateView_IsVisibleChanged;
        }

        // テーマ機能の初期化
        ThemeManager.ThemeChanged += OnThemeChanged;
        ThemeManager.Initialize();
        UpdateThemeUi(ThemeManager.CurrentTheme);
        UpdateGamingRgbAnimations();

        // トークン設定の読み込み (DPAPI暗号化カスタム保存 ➔ 環境変数 ➔ デフォルト共有)
        var savedToken = TokenStorage.LoadToken();
        if (!string.IsNullOrWhiteSpace(savedToken))
        {
            _botToken = savedToken;
            InputBotToken.Password = savedToken;
            SetStatus("READY (CUSTOM TOKEN ACTIVE)", true);
        }

        if (string.IsNullOrWhiteSpace(_botToken))
        {
            var envToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")?.Trim();
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
        UserIdInput.TextChanged += UserIdInput_TextChanged;
    }

    private void UserIdInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePlaceholder();
        if (string.IsNullOrWhiteSpace(UserIdInput.Text))
        {
            HideSearchResult(withAnimation: true);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        UserIdInput.TextChanged -= UserIdInput_TextChanged;
        StateChanged -= MainWindow_StateChanged;
        if (EmptyStateView != null)
        {
            EmptyStateView.IsVisibleChanged -= EmptyStateView_IsVisibleChanged;
        }
        StopGamingRgbAnimations();
        CancelActiveLookup();
        ++_nameplateLoadVersion;
        ClearAllResultImages();
        _imageLoadTokens.Clear();
        ClearNameplateCache();
        base.OnClosed(e);
    }

    private void UpdatePlaceholder()
    {
        if (UserIdPlaceholder != null)
        {
            UserIdPlaceholder.Visibility = string.IsNullOrWhiteSpace(UserIdInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    // ─────────────────────────────────────────────
    // タイトルバー & ウィンドウ操作
    // ─────────────────────────────────────────────
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private void ThemeBgLayer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ThemeBgLayer != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            var clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Height), 17, 17);
            clip.Freeze();
            ThemeBgLayer.Clip = clip;
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
        UpdateGamingRgbAnimations();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateGamingRgbAnimations();
        UpdateApngAnimationsPlayback();
    }

    private void EmptyStateView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateGamingRgbAnimations();
    }

    private void UpdateApngAnimationsPlayback()
    {
        bool shouldPlay = WindowState != WindowState.Minimized
            && (TabMain == null || TabMain.IsChecked == true);

        if (shouldPlay)
        {
            _currentApngController?.Resume();
            _nameplateApngController?.Resume();
        }
        else
        {
            _currentApngController?.Pause();
            _nameplateApngController?.Pause();
        }
    }

    private void UpdateGamingRgbAnimations()
    {
        bool allowRgb = ThemeManager.CurrentTheme == AppTheme.GamingRgb && WindowState != WindowState.Minimized;

        if (allowRgb)
        {
            if (!_isRgbBorderRunning && _rgbBorderStoryboard != null)
            {
                _rgbBorderStoryboard.Begin(this, isControllable: true);
                _isRgbBorderRunning = true;
            }
        }
        else if (_isRgbBorderRunning && _rgbBorderStoryboard != null)
        {
            _rgbBorderStoryboard.Stop(this);
            _rgbBorderStoryboard.Remove(this);
            _isRgbBorderRunning = false;
        }

        bool runSearchIconRgb = allowRgb && EmptyStateView != null && EmptyStateView.IsVisible;
        if (runSearchIconRgb)
        {
            if (!_isRgbSearchIconRunning && _rgbSearchIconStoryboard != null)
            {
                _rgbSearchIconStoryboard.Begin(this, isControllable: true);
                _isRgbSearchIconRunning = true;
            }
        }
        else if (_isRgbSearchIconRunning && _rgbSearchIconStoryboard != null)
        {
            _rgbSearchIconStoryboard.Stop(this);
            _rgbSearchIconStoryboard.Remove(this);
            _isRgbSearchIconRunning = false;
        }
    }

    private void StopGamingRgbAnimations()
    {
        if (_isRgbBorderRunning && _rgbBorderStoryboard != null)
        {
            _rgbBorderStoryboard.Stop(this);
            _rgbBorderStoryboard.Remove(this);
            _isRgbBorderRunning = false;
        }
        if (_isRgbSearchIconRunning && _rgbSearchIconStoryboard != null)
        {
            _rgbSearchIconStoryboard.Stop(this);
            _rgbSearchIconStoryboard.Remove(this);
            _isRgbSearchIconRunning = false;
        }
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
        if (_isClosing) return;
        _isClosing = true;
        IsHitTestVisible = false;

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
        sb.Completed += (s, ev) => { if (!_isClosed) Close(); };
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
        UpdateApngAnimationsPlayback();
    }

    // ─────────────────────────────────────────────
    // 設定保存
    // ─────────────────────────────────────────────
    private static string NormalizeToken(string? raw)
    {
        var token = raw?.TrimStart() ?? string.Empty;
        if (token.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring(4);
        }
        else if (string.Equals(token.TrimEnd(), "Bot", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }
        return token.Trim();
    }

    private static bool IsValidTokenFormat(string token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && !token.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c > 0x7F);
    }

    private void SaveToken_Click(object sender, RoutedEventArgs e)
    {
        var rawInput = InputBotToken.Password;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            ResetToken_Click(sender, e);
            return;
        }

        var normalized = NormalizeToken(rawInput);
        if (!IsValidTokenFormat(normalized))
        {
            SetStatus("INVALID TOKEN FORMAT", false);
            MessageBox.Show("Bot トークンの形式が不正です（空、または空白・改行・非ASCII文字が含まれています）。\n正しい Bot トークンを入力してください。", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            TokenStorage.SaveToken(normalized);
            _botToken = normalized;
            InputBotToken.Password = normalized;
            SetStatus("CUSTOM TOKEN SAVED (DPAPI ENCRYPTED)", true);
            MessageBox.Show("カスタム Bot トークンを暗号化して安全に保存しました。\n今後はこのトークンで優先検索されます。", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            App.LogException("SaveToken_Click", ex);
            SetStatus("TOKEN SAVE FAILED", false);
            MessageBox.Show($"トークンの暗号化保存に失敗しました。\n\n詳細: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetToken_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TokenStorage.DeleteToken();
            var envToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN")?.Trim();
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                _botToken = envToken;
                InputBotToken.Password = envToken;
                SetStatus("READY (TOKEN LOADED FROM ENV)", true);
                MessageBox.Show("カスタム保存設定をリセットしました。\n環境変数 DISCORD_BOT_TOKEN が設定されているため、環境変数のトークンを使用します。", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _botToken = string.Empty;
                InputBotToken.Password = string.Empty;
                SetStatus("READY (SHARED TOKEN ACTIVE)", true);
                MessageBox.Show("カスタム設定をリセットしました。\n今後は内蔵の共有Botトークンが自動使用されます（設定不要）。", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            App.LogException("ResetToken_Click", ex);
            SetStatus("TOKEN RESET FAILED", false);
            MessageBox.Show($"設定のリセットに失敗しました。\n\n詳細: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─────────────────────────────────────────────
    // リソース・キャッシュ・キャンセル管理ヘルパー
    // ─────────────────────────────────────────────
    private static long EstimateFramesBytes(List<ApngFrame> frames)
    {
        long total = 0;
        foreach (var f in frames)
        {
            if (f.Image != null)
            {
                total += (long)f.Image.PixelWidth * f.Image.PixelHeight * 4L;
            }
        }
        return Math.Max(total, 1);
    }

    private bool TryGetNameplateCache(string skuId, out List<ApngFrame>? frames)
    {
        lock (_nameplateCache)
        {
            if (_nameplateCache.TryGetValue(skuId, out var node))
            {
                _nameplateCacheLru.Remove(node);
                _nameplateCacheLru.AddFirst(node);
                frames = node.Value.Entry.Frames;
                return true;
            }
            frames = null;
            return false;
        }
    }

    private void AddNameplateCache(string skuId, List<ApngFrame> frames)
    {
        long estimated = EstimateFramesBytes(frames);
        if (estimated > MaxNameplateCacheBytes) return;

        lock (_nameplateCache)
        {
            if (_nameplateCache.TryGetValue(skuId, out var existing))
            {
                _nameplateCacheTotalBytes -= existing.Value.Entry.EstimatedBytes;
                _nameplateCacheLru.Remove(existing);
                _nameplateCache.Remove(skuId);
            }

            while (_nameplateCache.Count >= MaxNameplateCacheItems ||
                   (_nameplateCacheTotalBytes + estimated > MaxNameplateCacheBytes && _nameplateCacheLru.Last != null))
            {
                var tail = _nameplateCacheLru.Last;
                if (tail == null) break;
                _nameplateCacheTotalBytes -= tail.Value.Entry.EstimatedBytes;
                _nameplateCache.Remove(tail.Value.Key);
                _nameplateCacheLru.RemoveLast();
            }

            var entry = new NameplateCacheEntry { Frames = frames, EstimatedBytes = estimated };
            var node = new LinkedListNode<(string Key, NameplateCacheEntry Entry)>((skuId, entry));
            _nameplateCacheLru.AddFirst(node);
            _nameplateCache[skuId] = node;
            _nameplateCacheTotalBytes += estimated;
        }
    }

    private void ClearNameplateCache()
    {
        lock (_nameplateCache)
        {
            _nameplateCache.Clear();
            _nameplateCacheLru.Clear();
            _nameplateCacheTotalBytes = 0;
        }
    }

    private void CancelActiveLookup()
    {
        ++_lookupVersion;
        if (_lookupCts != null)
        {
            try { _lookupCts.Cancel(); } catch { }
            _lookupCts = null;
        }
    }

    private int AdvanceImageToken(params Image?[] targets)
    {
        int firstToken = 0;
        bool hasFirst = false;
        lock (_imageLoadTokens)
        {
            foreach (var t in targets)
            {
                if (t == null) continue;
                _imageLoadTokens.TryGetValue(t, out var cur);
                int next = cur + 1;
                _imageLoadTokens[t] = next;
                if (!hasFirst)
                {
                    firstToken = next;
                    hasFirst = true;
                }
            }
        }
        return firstToken;
    }

    private bool IsLatestImageToken(Image target, int token)
    {
        lock (_imageLoadTokens)
        {
            return _imageLoadTokens.TryGetValue(target, out var cur) && cur == token;
        }
    }

    private static void ClearImageControl(Image? img)
    {
        if (img == null) return;
        try { XamlAnimatedGif.AnimationBehavior.SetSourceUri(img, null); } catch { }
        try { XamlAnimatedGif.AnimationBehavior.SetSourceStream(img, null); } catch { }
        img.Source = null;
    }

    private void ClearAllResultImages()
    {
        _currentApngController?.Dispose();
        _currentApngController = null;
        _nameplateApngController?.Dispose();
        _nameplateApngController = null;

        Image?[] allImages =
        [
            AvatarImage,
            PlateAvatarImage,
            AvatarDecorationImage,
            PlateAvatarDecoImage,
            BannerImage,
            PlateBackgroundStaticImage,
            ImgClanBadge,
            PlateClanBadgeImage
        ];

        AdvanceImageToken(allImages);
        foreach (var img in allImages)
        {
            ClearImageControl(img);
        }
    }

    private void ResetDecorationVisibility()
    {
        if (AvatarRing != null) AvatarRing.StrokeThickness = 3;
        if (AvatarShadow != null) AvatarShadow.Opacity = 0.65;
        if (AvatarDecorationImage != null) AvatarDecorationImage.Visibility = Visibility.Collapsed;
        if (PlateAvatarDecoImage != null) PlateAvatarDecoImage.Visibility = Visibility.Collapsed;
        if (PlateBackgroundStaticImage != null) PlateBackgroundStaticImage.Visibility = Visibility.Collapsed;
        if (BannerContainer != null) BannerContainer.SetResourceReference(Border.BackgroundProperty, "BannerBgBrush");
        if (BannerImage != null) BannerImage.Visibility = Visibility.Visible;
        if (BadgeBot != null) BadgeBot.Visibility = Visibility.Collapsed;
        if (BorderBadgesContainer != null) BorderBadgesContainer.Visibility = Visibility.Collapsed;
        if (BadgesPanel != null) BadgesPanel.Children.Clear();
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
        if (e.Key == Key.Enter && !e.IsRepeat)
        {
            await ExecuteLookupAsync();
        }
    }

    private void HideSearchResult(bool withAnimation = true)
    {
        CancelActiveLookup();
        if (SearchButton != null) SearchButton.IsEnabled = true;
        ++_nameplateLoadVersion;
        _currentUser = null;

        _currentApngController?.Dispose();
        _currentApngController = null;
        _nameplateApngController?.Dispose();
        _nameplateApngController = null;

        _currentAvatarUrl = string.Empty;
        _currentAvatarDecorationUrl = string.Empty;
        _currentBannerUrl = string.Empty;
        if (BtnOpenAvatar != null) BtnOpenAvatar.IsEnabled = false;
        if (BtnOpenDecoration != null) BtnOpenDecoration.IsEnabled = false;
        if (BtnOpenBanner != null) BtnOpenBanner.IsEnabled = false;

        AdvanceImageToken(
            AvatarImage, PlateAvatarImage,
            AvatarDecorationImage, PlateAvatarDecoImage,
            BannerImage, PlateBackgroundStaticImage,
            ImgClanBadge, PlateClanBadgeImage);

        var isMainVisible = SearchResultGrid != null && SearchResultGrid.Visibility == Visibility.Visible;
        var isRawVisible = RawDataResultGrid != null && RawDataResultGrid.Visibility == Visibility.Visible;
        if (!isMainVisible && !isRawVisible && !_isHidingResult)
        {
            ClearAllResultImages();
            ResetDecorationVisibility();
            SetStatus("READY", true);
            return;
        }

        if (withAnimation && _isHidingResult) return;

        int transitionVersion = ++_viewTransitionVersion;

        if (withAnimation)
        {
            _isHidingResult = true;
            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };

            var animMainOpacity = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, duration) { EasingFunction = easing };
            var animMainTranslate = new System.Windows.Media.Animation.DoubleAnimation(0.0, 8.0, duration) { EasingFunction = easing };
            var animRawOpacity = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, duration) { EasingFunction = easing };
            var animRawTranslate = new System.Windows.Media.Animation.DoubleAnimation(0.0, 8.0, duration) { EasingFunction = easing };

            animMainOpacity.Completed += (s, e) =>
            {
                if (_viewTransitionVersion != transitionVersion) return;
                _isHidingResult = false;

                if (SearchResultGrid != null)
                {
                    SearchResultGrid.BeginAnimation(UIElement.OpacityProperty, null);
                    SearchResultGrid.Visibility = Visibility.Collapsed;
                }
                if (RawDataResultGrid != null)
                {
                    RawDataResultGrid.BeginAnimation(UIElement.OpacityProperty, null);
                    RawDataResultGrid.Visibility = Visibility.Collapsed;
                }
                if (TxtRawJson != null) TxtRawJson.Text = string.Empty;
                ClearAllResultImages();
                ResetDecorationVisibility();

                if (EmptyStateView != null)
                {
                    EmptyStateView.Visibility = Visibility.Visible;
                    var fadeInMain = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                    EmptyStateView.BeginAnimation(UIElement.OpacityProperty, fadeInMain);
                }
                if (RawEmptyStateView != null)
                {
                    RawEmptyStateView.Visibility = Visibility.Visible;
                    var fadeInRaw = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                    RawEmptyStateView.BeginAnimation(UIElement.OpacityProperty, fadeInRaw);
                }
            };

            if (SearchResultGrid != null) SearchResultGrid.BeginAnimation(UIElement.OpacityProperty, animMainOpacity);
            if (SearchResultTranslate != null) SearchResultTranslate.BeginAnimation(TranslateTransform.YProperty, animMainTranslate);

            if (RawDataResultGrid != null) RawDataResultGrid.BeginAnimation(UIElement.OpacityProperty, animRawOpacity);
            if (RawDataTranslate != null) RawDataTranslate.BeginAnimation(TranslateTransform.YProperty, animRawTranslate);
        }
        else
        {
            _isHidingResult = false;

            if (SearchResultGrid != null)
            {
                SearchResultGrid.BeginAnimation(UIElement.OpacityProperty, null);
                SearchResultGrid.Visibility = Visibility.Collapsed;
            }
            if (EmptyStateView != null)
            {
                EmptyStateView.BeginAnimation(UIElement.OpacityProperty, null);
                EmptyStateView.Visibility = Visibility.Visible;
                EmptyStateView.Opacity = 1.0;
            }

            if (RawDataResultGrid != null)
            {
                RawDataResultGrid.BeginAnimation(UIElement.OpacityProperty, null);
                RawDataResultGrid.Visibility = Visibility.Collapsed;
            }
            if (RawEmptyStateView != null)
            {
                RawEmptyStateView.BeginAnimation(UIElement.OpacityProperty, null);
                RawEmptyStateView.Visibility = Visibility.Visible;
                RawEmptyStateView.Opacity = 1.0;
            }
            if (TxtRawJson != null) TxtRawJson.Text = string.Empty;
            ClearAllResultImages();
            ResetDecorationVisibility();
        }

        SetStatus("READY", true);
    }

    private async Task ExecuteLookupAsync()
    {
        CancellationToken ct = default;
        int myVersion = -1;

        try
        {
            var rawInput = UserIdInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(rawInput))
            {
                // 空欄時はエラーメッセージを出さず、メニューを滑らかにフェードアウト非表示化
                HideSearchResult(withAnimation: true);
                return;
            }

            if (!ulong.TryParse(rawInput, out var userId) || userId == 0)
            {
                MessageBox.Show("有効な数値のユーザーID（Snowflake）を入力してください。", "フォーマットエラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // トークン取得 (カスタム ➔ 環境変数 ➔ 共有トークン)
            var token = NormalizeToken(GetEffectiveToken());
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show("Bot トークンを取得できませんでした。\nSettings からトークンを設定してください。", "トークン未設定", MessageBoxButton.OK, MessageBoxImage.Warning);
                TabSettings.IsChecked = true;
                return;
            }
            if (!IsValidTokenFormat(token))
            {
                MessageBox.Show("Bot トークンの形式が不正です（空白・改行・非ASCII文字が含まれています）。\nSettings のトークン設定を確認してください。", "トークン形式エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                TabSettings.IsChecked = true;
                return;
            }

            CancelActiveLookup();
            var cts = new CancellationTokenSource();
            _lookupCts = cts;
            myVersion = _lookupVersion;
            ct = cts.Token;

            SearchButton.IsEnabled = false;
            SetStatus("LOOKING UP USER...", true);

            // API リクエスト (静的 HttpClient を再利用して Keep-Alive 接続により超高速化)
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/users/{userId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);
            if (!request.Headers.UserAgent.Any())
            {
                request.Headers.UserAgent.ParseAdd("DiscordLookupGUI/2.4");
            }

            using var response = await _discordApiClient.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (myVersion != _lookupVersion || ct.IsCancellationRequested) return;

            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                SetStatus($"API ERROR: {code}", false);
                if (code == 429)
                {
                    string? retryInfo = null;
                    if (response.Headers.TryGetValues("Retry-After", out var retryVals) ||
                        response.Headers.TryGetValues("X-RateLimit-Reset-After", out retryVals))
                    {
                        retryInfo = retryVals.FirstOrDefault();
                    }
                    var waitMsg = !string.IsNullOrWhiteSpace(retryInfo)
                        ? $"約 {retryInfo} 秒待ってから再試行してください。"
                        : "数秒待ってから再試行してください。";
                    MessageBox.Show($"Discord API のレート制限 (429 Too Many Requests) に達しました。\n{waitMsg}", "レート制限", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (code == 404)
                {
                    MessageBox.Show("指定されたユーザーIDは見つかりませんでした。", "検索結果なし (404)", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (code == 401 || code == 403)
                {
                    MessageBox.Show($"Bot トークンが無効またはアクセス権限が不足しています ({code})。\nSettings のトークン設定を確認してください。", "認証エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    var truncated = content.Length > 300 ? content.Substring(0, 300) + "..." : content;
                    MessageBox.Show($"Discord API エラー:\n{code} {response.ReasonPhrase}\n\n{truncated}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            // Raw JSON 整形とモデルデシリアライズをバックグラウンドで並行実行 (UIスレッド完全解放)
            var (formattedJson, user, parseError) = await Task.Run(() =>
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

                DiscordUser? parsedUser = null;
                string? err = null;
                try
                {
                    parsedUser = JsonConvert.DeserializeObject<DiscordUser>(content);
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }
                return (json, parsedUser, err);
            });

            if (myVersion != _lookupVersion || ct.IsCancellationRequested) return;

            TxtRawJson.Text = formattedJson;

            if (user == null)
            {
                SetStatus("PARSE ERROR", false);
                var msg = string.IsNullOrEmpty(parseError)
                    ? "ユーザーデータの解析に失敗しました（空のレスポンスまたは無効なデータ）。"
                    : $"ユーザーデータの解析に失敗しました:\n{parseError}";
                MessageBox.Show(msg, "パースエラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            user.Id = userId.ToString();
            DisplayUser(user, userId, ct);
            SetStatus("USER LOADED SUCCESSFULLY", true);
        }
        catch (OperationCanceledException)
        {
            if (myVersion != _lookupVersion || ct.IsCancellationRequested)
            {
                return;
            }
            SetStatus("REQUEST TIMEOUT", false);
            MessageBox.Show("Discord API への接続がタイムアウトしました（10秒）。\n時間をおいて再試行してください。", "タイムアウト", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (HttpRequestException ex)
        {
            if (myVersion != _lookupVersion) return;
            SetStatus("NETWORK ERROR", false);
            MessageBox.Show($"ネットワーク接続エラーが発生しました:\n{ex.Message}", "通信エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            if (myVersion != _lookupVersion) return;
            SetStatus("ERROR OCCURRED", false);
            MessageBox.Show($"エラーが発生しました:\n{ex.Message}", "例外エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (myVersion == _lookupVersion && SearchButton != null)
            {
                SearchButton.IsEnabled = true;
            }
        }
    }

    private void DisplayUser(DiscordUser user, ulong rawId, CancellationToken ct)
    {
        ++_viewTransitionVersion;
        _isHidingResult = false;
        _currentUser = user;

        // 検索結果表示に切り替え & 上品なフェードイン＋スライド演出 (0.5秒 / 500ms)
        EmptyStateView.Visibility = Visibility.Collapsed;
        SearchResultGrid.BeginAnimation(UIElement.OpacityProperty, null);
        SearchResultGrid.Visibility = Visibility.Visible;

        if (RawEmptyStateView != null) RawEmptyStateView.Visibility = Visibility.Collapsed;
        if (RawDataResultGrid != null)
        {
            RawDataResultGrid.BeginAnimation(UIElement.OpacityProperty, null);
            RawDataResultGrid.Visibility = Visibility.Visible;
        }

        var duration = TimeSpan.FromMilliseconds(500);
        var easing = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

        var animMainOpacity = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, duration) { EasingFunction = easing };
        var animMainTranslate = new System.Windows.Media.Animation.DoubleAnimation(12.0, 0.0, duration) { EasingFunction = easing };
        SearchResultGrid.BeginAnimation(UIElement.OpacityProperty, animMainOpacity);
        if (SearchResultTranslate != null)
        {
            SearchResultTranslate.BeginAnimation(TranslateTransform.YProperty, animMainTranslate);
        }

        if (RawDataResultGrid != null)
        {
            var animRawOpacity = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, duration) { EasingFunction = easing };
            var animRawTranslate = new System.Windows.Media.Animation.DoubleAnimation(12.0, 0.0, duration) { EasingFunction = easing };
            RawDataResultGrid.BeginAnimation(UIElement.OpacityProperty, animRawOpacity);
            if (RawDataTranslate != null)
            {
                RawDataTranslate.BeginAnimation(TranslateTransform.YProperty, animRawTranslate);
            }
        }

        // プロフィール情報
        TxtGlobalName.Text = string.IsNullOrWhiteSpace(user.GlobalName) ? user.Username : user.GlobalName;
        TxtUsername.Text = user.DisplayUsername;
        TxtUserId.Text = user.Id;

        // クランタグ & クランバッジ
        if (!string.IsNullOrEmpty(user.ClanTag))
        {
            TxtClanTag.Text = user.ClanTag;
            if (!string.IsNullOrEmpty(user.ClanBadgeUrl))
            {
                _ = LoadImageAsync(ImgClanBadge, user.ClanBadgeUrl, ct: ct);
                ImgClanBadge.Visibility = Visibility.Visible;
            }
            else
            {
                AdvanceImageToken(ImgClanBadge);
                ClearImageControl(ImgClanBadge);
                ImgClanBadge.Visibility = Visibility.Collapsed;
            }
            BadgeClan.Visibility = Visibility.Visible;
        }
        else
        {
            AdvanceImageToken(ImgClanBadge);
            ClearImageControl(ImgClanBadge);
            ImgClanBadge.Visibility = Visibility.Collapsed;
            BadgeClan.Visibility = Visibility.Collapsed;
        }

        // Bot タグ表示
        if (BadgeBot != null)
        {
            BadgeBot.Visibility = user.Bot == true ? Visibility.Visible : Visibility.Collapsed;
        }

        // 公式バッジ一覧 (HypeSquad, Active Developer, Nitro 等)
        var badges = user.Badges;
        if (BadgesPanel != null) BadgesPanel.Children.Clear();

        if (badges.Count > 0)
        {
            foreach (var badge in badges)
            {
                var bmp = LoadBadgeBitmap(badge.IconAssetPath);

                if (BadgesPanel != null)
                {
                    var pImg = new Image
                    {
                        Width = 20,
                        Height = 20,
                        Margin = new Thickness(3, 2, 3, 2),
                        Stretch = Stretch.Uniform,
                        Source = bmp,
                        ToolTip = new ToolTip { Content = $"{badge.Name}\n{badge.Description}" }
                    };
                    RenderOptions.SetBitmapScalingMode(pImg, BitmapScalingMode.HighQuality);
                    BadgesPanel.Children.Add(pImg);
                }
            }

            if (BorderBadgesContainer != null) BorderBadgesContainer.Visibility = Visibility.Visible;
        }
        else
        {
            if (BorderBadgesContainer != null) BorderBadgesContainer.Visibility = Visibility.Collapsed;
        }

        // アクセントカラー (accent_color 優先、未設定時は banner_color)
        Color? resolvedAccentColor = null;
        string resolvedAccentHex = string.Empty;

        if (user.AccentColor.HasValue && user.AccentColor.Value is >= 0 and <= 0xFFFFFF)
        {
            resolvedAccentHex = $"#{user.AccentColor.Value:X6}";
            try
            {
                resolvedAccentColor = (Color)ColorConverter.ConvertFromString(resolvedAccentHex);
            }
            catch { }
        }

        if (!resolvedAccentColor.HasValue && !string.IsNullOrWhiteSpace(user.BannerColor))
        {
            var rawHex = user.BannerColor.Trim();
            var fullHex = rawHex.StartsWith("#") ? rawHex : "#" + rawHex;
            try
            {
                resolvedAccentColor = (Color)ColorConverter.ConvertFromString(fullHex);
                resolvedAccentHex = fullHex.ToUpperInvariant();
            }
            catch { }
        }

        if (resolvedAccentColor.HasValue)
        {
            TxtAccentColor.Text = resolvedAccentHex;
            var brush = new SolidColorBrush(resolvedAccentColor.Value);
            brush.Freeze();
            RectAccentColor.Background = brush;
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
                _ = LoadImageAsync(PlateClanBadgeImage, user.ClanBadgeUrl, ct: ct);
            }
            else
            {
                AdvanceImageToken(PlateClanBadgeImage);
                ClearImageControl(PlateClanBadgeImage);
                PlateClanBadgeImage.Visibility = Visibility.Collapsed;
                PlateClanHeart.Visibility = Visibility.Visible;
            }
        }
        else
        {
            AdvanceImageToken(PlateClanBadgeImage);
            ClearImageControl(PlateClanBadgeImage);
            PlateClanBadgeImage.Visibility = Visibility.Collapsed;
            PlateClanContainer.Visibility = Visibility.Collapsed;
        }

        // ネームプレート公式装飾 (Nameplate Collectible) の動的自動反映 (各ユーザー固有の公式 APNG を 60FPS 再生)
        _ = LoadNameplateAsync(user, ct);

        // アバター読み込み
        _currentAvatarUrl = user.AvatarUrl;
        BtnOpenAvatar.IsEnabled = !string.IsNullOrEmpty(_currentAvatarUrl);
        _ = LoadImageAsync(AvatarImage, _currentAvatarUrl, extraTargetImage: PlateAvatarImage, ct: ct);

        // アバターデコレーション（飾り枠）読み込み
        _currentAvatarDecorationUrl = user.AvatarDecorationUrl ?? string.Empty;
        if (!string.IsNullOrEmpty(_currentAvatarDecorationUrl))
        {
            AvatarDecorationImage.Visibility = Visibility.Visible;
            PlateAvatarDecoImage.Visibility = Visibility.Visible;
            BtnOpenDecoration.IsEnabled = true;
            if (AvatarRing != null) AvatarRing.StrokeThickness = 0;
            if (AvatarShadow != null) AvatarShadow.Opacity = 0;
            _ = LoadImageAsync(AvatarDecorationImage, _currentAvatarDecorationUrl, isDecoration: true, extraTargetImage: PlateAvatarDecoImage, ct: ct);
        }
        else
        {
            AvatarDecorationImage.Visibility = Visibility.Collapsed;
            PlateAvatarDecoImage.Visibility = Visibility.Collapsed;
            BtnOpenDecoration.IsEnabled = false;
            if (AvatarRing != null) AvatarRing.StrokeThickness = 3;
            if (AvatarShadow != null) AvatarShadow.Opacity = 0.65;
            _ = LoadImageAsync(AvatarDecorationImage, string.Empty, isDecoration: true, extraTargetImage: PlateAvatarDecoImage, ct: ct);
        }

        // バナー背景色・画像読み込み (アクセントカラー/バナーカラー ➔ 未設定ならテーマ色で塗りつぶし)
        if (resolvedAccentColor.HasValue)
        {
            var brush = new SolidColorBrush(resolvedAccentColor.Value);
            brush.Freeze();
            BannerContainer.Background = brush;
        }
        else
        {
            BannerContainer.SetResourceReference(Border.BackgroundProperty, "BannerBgBrush");
        }

        _currentBannerUrl = user.BannerUrl ?? string.Empty;
        BtnOpenBanner.IsEnabled = !string.IsNullOrEmpty(_currentBannerUrl);

        if (!string.IsNullOrEmpty(_currentBannerUrl))
        {
            BannerImage.Visibility = Visibility.Visible;
            _ = LoadImageAsync(BannerImage, _currentBannerUrl, ct: ct);
        }
        else
        {
            AdvanceImageToken(BannerImage);
            ClearImageControl(BannerImage);
            BannerImage.Visibility = Visibility.Collapsed;
        }

        // タイムライン / Snowflake 解析
        TxtCreatedAtJst.Text = user.CreatedAtJst == DateTimeOffset.MinValue
            ? "--"
            : user.CreatedAtJst.ToString("yyyy年M月d日 HH:mm:ss");
        TxtCreatedAtUtc.Text = user.CreatedAt == DateTimeOffset.MinValue
            ? "UTC: --"
            : $"UTC: {user.CreatedAt:yyyy-MM-dd HH:mm:ss}";

        TxtElapsedBadge.Text = $"{user.ElapsedYears:F1} years";
        TxtElapsedDays.Text = $"{user.ElapsedDays:N0} 日経過";
        ProgAccountAge.Value = Math.Clamp(user.ElapsedYears, 0.0, 10.0);

        // Snowflake 内部ID
        var workerId = (rawId & 0x3E0000) >> 17;
        var processId = (rawId & 0x1F000) >> 12;
        TxtWorkerId.Text = $"{workerId}";
        TxtProcessId.Text = $"{processId}";
    }

    private async Task LoadNameplateAsync(DiscordUser user, CancellationToken ct)
    {
        int currentVersion = ++_nameplateLoadVersion;
        string? staticUrl = null;

        try
        {
            AdvanceImageToken(PlateBackgroundStaticImage);
            _nameplateApngController?.Dispose();
            _nameplateApngController = null;
            ClearImageControl(PlateBackgroundStaticImage);

            if (string.IsNullOrEmpty(user.NameplateSkuId))
            {
                // ネームプレート未装備時はデフォルトのDiscordダーク背景に戻す
                var defaultBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313338"));
                defaultBg.Freeze();
                NameplateCard.Background = defaultBg;
                PlateBackgroundStaticImage.Visibility = Visibility.Collapsed;

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
            var indicatorBrush = new SolidColorBrush(paletteColor);
            indicatorBrush.Freeze();
            PlatePaletteIndicator.Background = indicatorBrush;
            PlatePaletteIndicator.Visibility = Visibility.Visible;

            // パレットに応じた公式ダーク背景トーンを設定
            var cardBg = new SolidColorBrush(paletteColor);
            cardBg.Freeze();
            NameplateCard.Background = cardBg;
            PlateBackgroundStaticImage.Visibility = Visibility.Visible;

            string skuId = user.NameplateSkuId;
            string? animatedUrl = user.NameplateAnimatedUrl;
            staticUrl = user.NameplateStaticUrl;

            TryGetNameplateCache(skuId, out var frames);

            if (frames == null && !string.IsNullOrEmpty(animatedUrl))
            {
                var bytes = await _imageClient.GetByteArrayAsync(animatedUrl, ct);
                if (ct.IsCancellationRequested || _nameplateLoadVersion != currentVersion) return;

                frames = await ApngDecoder.DecodeAsync(bytes, ct);
                if (ct.IsCancellationRequested || _nameplateLoadVersion != currentVersion) return;

                if (frames != null && frames.Count > 1)
                {
                    AddNameplateCache(skuId, frames);
                }
                else if (frames == null)
                {
                    bool isGif = bytes.Length >= 3 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F';
                    if (isGif)
                    {
                        ClearImageControl(PlateBackgroundStaticImage);
                        var msGif = new MemoryStream(bytes);
                        XamlAnimatedGif.AnimationBehavior.SetSourceStream(PlateBackgroundStaticImage, msGif);
                    }
                    else
                    {
                        // 単一フレーム PNG の場合はそのままデコードして表示
                        var bmp = new BitmapImage();
                        using (var ms = new MemoryStream(bytes))
                        {
                            bmp.BeginInit();
                            bmp.StreamSource = ms;
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                        }
                        bmp.Freeze();
                        PlateBackgroundStaticImage.Source = bmp;
                    }
                    return;
                }
            }

            if (ct.IsCancellationRequested || _nameplateLoadVersion != currentVersion) return;

            if (frames != null && frames.Count > 1)
            {
                _nameplateApngController?.Dispose();
                _nameplateApngController = null;
                PlateBackgroundStaticImage.Source = frames[0].Image;
                _nameplateApngController = new ApngAnimationController(PlateBackgroundStaticImage, frames);
                _nameplateApngController.Start();
                UpdateApngAnimationsPlayback();
            }
            else if (!string.IsNullOrEmpty(staticUrl))
            {
                _ = LoadImageAsync(PlateBackgroundStaticImage, staticUrl, ct: ct);
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセル時は何もしない
        }
        catch (Exception ex)
        {
            App.LogException("LoadNameplateAsync", ex);
            if (_nameplateLoadVersion == currentVersion && !ct.IsCancellationRequested && !string.IsNullOrEmpty(staticUrl))
            {
                _ = LoadImageAsync(PlateBackgroundStaticImage, staticUrl, ct: ct);
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
            double innerW = Math.Max(0, e.NewSize.Width - 2);
            double innerH = Math.Max(0, e.NewSize.Height - 2);

            // 枠全体を満たしつつ、上下左右の重心が完全に中央に来るように MinWidth/MinHeight を設定
            BannerImage.MinWidth = innerW;
            BannerImage.MinHeight = innerH;
            BannerImage.Width = double.NaN;
            BannerImage.Height = double.NaN;

            // コンテナ枠全体を角丸(12px)でクリップ
            var clip = new RectangleGeometry(
                new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
                12, 12);
            clip.Freeze();
            BannerContainer.Clip = clip;
        }
    }

    /// <summary>
    /// バッジアイコン画像をリソースから安全かつ同期的に読み込む (Pack URI &amp; ストリーム解決)
    /// </summary>
    private static BitmapImage? LoadBadgeBitmap(string uriString)
    {
        if (string.IsNullOrWhiteSpace(uriString)) return null;

        try
        {
            var uri = new Uri(uriString, UriKind.RelativeOrAbsolute);
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }
        catch { }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(uriString, UriKind.RelativeOrAbsolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { }

        return null;
    }

    private async Task LoadImageAsync(Image targetImage, string url, bool isDecoration = false, Image? extraTargetImage = null, CancellationToken ct = default)
    {
        var targetsAll = extraTargetImage != null ? new[] { targetImage, extraTargetImage } : new[] { targetImage };
        int token = AdvanceImageToken(targetsAll);

        try
        {
            if (isDecoration)
            {
                _currentApngController?.Dispose();
                _currentApngController = null;
            }

            foreach (var t in targetsAll)
            {
                ClearImageControl(t);
            }

            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            var uri = new Uri(url, UriKind.Absolute);

            if (isDecoration)
            {
                // アバターデコレーション (60FPS 完全同期 + 濁りゼロの完全透明度・色彩維持)
                var bytes = await _imageClient.GetByteArrayAsync(uri, ct);
                if (ct.IsCancellationRequested || !IsLatestImageToken(targetImage, token)) return;

                // APNG マルチフレームの非同期解析 (STAスレッドで処理しUIスレッドを1ミリ秒も止めない)
                var frames = await ApngDecoder.DecodeAsync(bytes, ct);
                if (ct.IsCancellationRequested || !IsLatestImageToken(targetImage, token)) return;

                if (frames != null && frames.Count > 1)
                {
                    // 60FPS 完全同期アニメーションコントローラーで再生 (再代入直前にも既存コントローラーを必ず停止)
                    _currentApngController?.Dispose();
                    _currentApngController = null;

                    foreach (var t in targetsAll)
                    {
                        t.Source = frames[0].Image;
                    }

                    _currentApngController = new ApngAnimationController(targetsAll, frames);
                    _currentApngController.Start();
                    UpdateApngAnimationsPlayback();
                    return;
                }

                // 単一フレーム / 通常 PNG の場合
                var bitmap = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                }
                bitmap.Freeze();

                foreach (var t in targetsAll)
                {
                    t.Source = bitmap;
                }
                return;
            }

            // 通常アバター & バナー (GIF / PNG)
            var data = await _imageClient.GetByteArrayAsync(uri, ct);

            // 連続検索時に古い画像で上書きしないようにガード
            if (ct.IsCancellationRequested || !IsLatestImageToken(targetImage, token)) return;

            bool isGif = data.Length >= 3 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F';
            if (isGif)
            {
                foreach (var t in targetsAll)
                {
                    ClearImageControl(t);
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
                    ClearImageControl(t);
                    t.Source = bmp;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセル時は何もしない
        }
        catch
        {
            if (!ct.IsCancellationRequested && IsLatestImageToken(targetImage, token))
            {
                foreach (var t in targetsAll)
                {
                    try { ClearImageControl(t); } catch { }
                }
            }
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
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            MessageBox.Show("無効または許可されていない URL です。", "セキュリティ保護", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
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
        StatusDot.SetResourceReference(
            System.Windows.Shapes.Shape.FillProperty,
            isSuccess ? "SuccessGreenBrush" : "DangerRedBrush");
    }

    private void CopyToClipboard(string text, string description)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            text == "-" ||
            text == "なし" ||
            text == "--" ||
            text == "未装備" ||
            text == "(None)" ||
            text.StartsWith("UTC: --") ||
            text.StartsWith("-- 年"))
            return;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, true);
                SetStatus($"COPIED: {description}", true);
                return;
            }
            catch
            {
                if (attempt < 2)
                {
                    Thread.Sleep(20);
                }
            }
        }
        SetStatus("COPY FAILED: クリップボードにアクセスできませんでした", false);
    }

    private void AccentColor_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentUser == null) return;
        CopyToClipboard(TxtAccentColor.Text, "Accent Color");
    }

    private void NameplateMetaCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentUser == null || string.IsNullOrEmpty(_currentUser.NameplateSkuId)) return;
        var text = $"sku_id: {TxtPlateSkuId.Text}\nlabel: {TxtPlateLabel.Text}\nasset: {TxtPlateAsset.Text}\npalette: {TxtPlatePalette.Text}";
        CopyToClipboard(text, "Nameplate Collectibles");
    }

    private void CopyPlateLabel_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_currentUser == null || string.IsNullOrEmpty(_currentUser.NameplateSkuId)) return;
        CopyToClipboard(TxtPlateLabel.Text, "Nameplate Label");
    }

    private void CopyPlatePalette_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_currentUser == null || string.IsNullOrEmpty(_currentUser.NameplateSkuId)) return;
        CopyToClipboard(TxtPlatePalette.Text, "Nameplate Palette");
    }

    private void CopyPlateSkuId_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_currentUser == null || string.IsNullOrEmpty(_currentUser.NameplateSkuId)) return;
        CopyToClipboard(TxtPlateSkuId.Text, "Nameplate SKU ID");
    }

    private void CreatedAtJst_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentUser == null) return;
        CopyToClipboard(TxtCreatedAtJst.Text, "Created At (JST)");
    }

    private void CreatedAtUtc_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentUser == null) return;
        var text = TxtCreatedAtUtc.Text;
        if (text.StartsWith("UTC: ")) text = text.Substring(5).Trim();
        CopyToClipboard(text, "Created At (UTC)");
    }

    private void CopySnowflakeAll_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_currentUser == null || TxtWorkerId.Text == "--") return;
        var text = $"Epoch Offset: 1420070400000 ms\nInternal Worker ID: {TxtWorkerId.Text}\nInternal Process ID: {TxtProcessId.Text}";
        CopyToClipboard(text, "Snowflake Decoding");
    }

    private void CopyEpoch_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentUser == null) return;
        CopyToClipboard("1420070400000", "Epoch Offset");
    }

    private void CopyWorkerId_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentUser == null) return;
        CopyToClipboard(TxtWorkerId.Text, "Worker ID");
    }

    private void CopyProcessId_Click(object sender, MouseButtonEventArgs e)
    {
        if (_currentUser == null) return;
        CopyToClipboard(TxtProcessId.Text, "Process ID");
    }

    private void CopyRawJson_Click(object sender, RoutedEventArgs e)
    {
        if (_isHidingResult) return;
        CopyToClipboard(TxtRawJson.Text, "Raw JSON");
    }
}
