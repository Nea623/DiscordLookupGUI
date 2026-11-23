# ⚡ DISCORD LOOKUP GUI

<div align="center">

![Version](https://img.shields.io/badge/version-v2.5-4DF2D3?style=for-the-badge&logo=discord&logoColor=black)
![.NET 8](https://img.shields.io/badge/.NET-8.0_WPF-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows_x64-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![Single File](https://img.shields.io/badge/Release-Single--File_exe-10B981?style=for-the-badge)

<br />

**近未来サイバーパンク調の洗練された Discord ユーザー情報検索（OSINT）デスクトップクライアント**

[ダウンロード (Releases)](https://github.com/Nea623/DiscordLookupGUI/releases/latest) • [機能紹介](#-主な機能) • [使い方](#-使い方) • [v2.5 アップデート内容](#-v25-アップデート内容) • [クレジット](#-クレジット)

<br />

<img src="assets/screenshot.jpg" alt="Discord Lookup GUI Screenshot" width="850" style="border-radius: 12px; box-shadow: 0 8px 30px rgba(0,0,0,0.5);" />

</div>

---

## ✨ 主な機能

- 🌌 **近未来・サイバーパンク UI （CYBERDECK）**
  - 発光ネオンアクセント、真円アバタークリップ、センター配置バナー、角丸ウィンドウ
  - 起動時の 1.5 秒ネオンロゴスプラッシュ演出 ＆ スムーズなフェードインアニメーション
  - ユーザー検索完了時の 0.5 秒優雅なフェードイン表示 ＆ 入力欄クリア時の自動フェードアウト復帰

- 🎨 **多彩な 5 つのテーマをリアルタイム動的切り替え**
  - 🌙 **Dark Theme**: サイバーネイビー ＆ ネオンシアンの近未来王道ダークUI
  - ☀️ **Light Theme**: 洗練されたフロストホワイト ＆ エレクトリックティールのクリーンUI
  - 🌆 **Sunset Theme**: 情緒あふれる美しい夕暮れグラデーション（ローズ＆バイオレット）
  - 🌠 **Starry Night Theme**: 満天の星空・星座アート・吊り下げ星オーナメントがきらめく幻想的UI
  - 🎮 **Gaming RGB Theme**: 漆黒のピュアブラック ＆ 外枠・虫眼鏡の無限レインボーサイクル ＆ 鮮烈なネオングリーン
  - タイトルバー右上のボタン、または Settings 画面から横方向にスッと展開するスマートなメニュー
  - 選択したテーマはローカル（`theme_settings.json`）に自動保存され、次回起動時もシームレスに復元

- 🏷️ **Discord 公式メンバーリスト原寸 ネームプレート (248x42px)**
  - Discord 公式原寸 `248x42px` ＆ 角丸 8px クリップを忠実に再現
  - 自作高性能 APNG エンジンによる透過アルファアニメーション（サイバーネティックの紫スモーク等）の滑らかループ再生
  - Discord 公式フォント「**gg sans**」（Normal / Medium / SemiBold / Bold）をアプリ内完全埋め込み
  - ネームプレート詳細メタ情報（`sku_id`, `label`, `asset`, `palette`）の常時表示 ＆ ホバー時のリッチツールチップ

- 📋 **ワンクリック・クリップボードコピー機能**
  - **Accent Color**: カラーコード（`#xxxxxx`）のクリックコピー
  - **Nameplate Collectibles**: カード全体クリックによる一括コピー ＆ `label`, `palette`, `sku_id` のピンポイント個別コピー
  - **Snowflake Decoding**: 「Copy All」による全項目一括コピー ＆ `Epoch Offset`, `Worker ID`, `Process ID` の個別コピー
  - **Account Creation Date**: 作成日時（JST / UTC）の個別コピー
  - **Raw Data**: ヘッダーの「Copy JSON」ボタンによるフォーマット済み生 JSON 一括コピー

- 🔄 **シームレスな UI 連動 ＆ 視認性最適化**
  - 入力欄クリア時に Main 画面と Raw Data 画面が完全同期して美しくフェードアウトし、未検索状態（Empty View）へ復帰
  - 各テーマの背景に合わせて最も視認性の高い配色に自動連動する入力欄プレースホルダー説明文

- 🎞️ **Nitro アニメーション GIF の完全ループ再生**
  - Discord Nitro などの動くアバターおよび動くバナー GIF をそのままアプリ内で滑らかにループ再生 (`XamlAnimatedGif`)

- 🦄 **Unicode カラー絵文字 ＆ クランバッジ対応**
  - Discord のクランタグに含まれるカラー絵文字（DirectWrite）を完全レンダリング (`Emoji.Wpf`)
  - クランバッジ画像の直接描画

- 📊 **詳細なアカウントアナリティクス**
  - アカウント作成日時 (JST / UTC) の瞬時計算
  - アカウント経過時間（年数）および視覚的プログレスバー
  - Discord Snowflake のバイナリデコード（Epoch Offset / Worker ID / Process ID 解析）
  - 生の JSON データをインデント整形して閲覧できる `Raw Data` タブ

- 🔐 **安全なトークン保護 ＆ 徹底した省電力・安定性チューニング**
  - カスタム Bot トークンは Windows DPAPI で暗号化保存（旧平文からの自動移行・形式バリデーション対応）
  - ウィンドウ最小化時や別タブ滞在時は APNG・Gaming RGB アニメーションを自動一時停止し CPU/GPU 負荷を徹底削減

- 🚀 **単一実行ファイル (Single-File) 自己完結型配布**
  - .NET 8 ランタイム同梱のため、面倒なインストール作業は一切不要
  - 配布された `DiscordLookupGUI.exe` 1つをクリックするだけで即座に起動可能

---

## 🚀 使い方

### 1. ダウンロード
[GitHub Releases (v2.5)](https://github.com/Nea623/DiscordLookupGUI/releases/latest) から **`DiscordLookupGUI.exe`** をダウンロードします。

> [!NOTE]
> **Windows SmartScreen の警告が表示された場合**  
> 初回起動時に「Windows によって PC が保護されました」という青いセキュリティ画面が表示される場合があります。本アプリは完全なオープンソースであり安全ですが、個人開発のためコード署名証明書を取得していないこと・公開直後で実績数が少ないことによる Windows の標準的な挙動です。  
> **起動方法**: 画面内の **「詳細情報」** をクリック ➔ 右下に表示される **「実行」** ボタンをクリックすることで安全に起動できます。

### 2. トークン設定不要！そのまま即座に検索可能
**共有 Bot トークンが内蔵されているため、面倒なトークン取得・設定作業は一切不要です。**
- **独自の Bot トークンを使いたい場合（任意）**:
  - 上部タブの `Settings` からご自身のトークンを入力して保存するか、環境変数 `DISCORD_BOT_TOKEN` を設定することで優先使用できます。保存したカスタムトークンは **Windows DPAPI で暗号化保存され安全に管理されます**。空欄で保存またはリセットすればいつでも共有トークンに戻せます。

### 3. ユーザー検索
1. 上部の検索バーに調べたいユーザーの **Discord ユーザーID (Snowflake)** を入力
2. `Enter` キーを押すか、`LOOKUP USER` ボタンをクリック
3. アバター、バナー、クラン情報、アカウント作成日、アナリティクスが美しくフェードイン表示されます！
4. 検索バーを空欄にすると、スムーズにフェードアウトして初期画面に戻ります。

---

## ⚡ v2.5 アップデート内容

- 📋 **クリップボード自動検知（Auto-Lookup）＆ 設定記憶**:
  - DiscordやWebブラウザでID・メンション・URLをコピー（`Ctrl+C`）した瞬間、Win32 APIイベント駆動で即座に検知し自動検索！
  - `Settings` タブのサイバー風トグルスイッチでいつでも ON/OFF 可能（設定はローカルに自動保存）。自前コピー誤爆防止・重複ガード完備。
- 🕒 **検索履歴（Recent Lookups）クイックドロップダウン**:
  - 検索バー横の「🕒」ボタンから、直近最大20件の履歴を丸型アバターサムネイル・表示名・ID・相対時間付きで一覧表示。
  - ワンクリックで即座再検索＆全消去機能付き（プライバシー配慮）。
- 🔍 **メンション・URL・テキストからのID自動抽出**:
  - `<@ID>`, `<@!ID>`, `discord.com/users/{id}`, `ユーザー: {id}` など、あらゆるテキストからSnowflake IDを瞬時に自動抽出。
- 🟣 **公式Discordコミュニティ（⚡DLG Community）招待ボタン**:
  - `Settings` ➔ `About` に公式コミュニティサーバーへのワンクリック招待ボタンを新設。
- ⚡ **Snowflake Decoding の強化**:
  - `Creation Timestamp (ms)`（UNIX絶対ミリ秒）の解析表示を追加。
- 🎨 **バナー読み込み時のチラつき（フラッシュ）完全解消**:
  - バナー画像があるユーザーを表示する際、画像のダウンロード完了前にアクセントカラーが一瞬パッと光る現象を根治。

---

## 🔧 過去の徹底改修ログ (v2.4)

> **v2.4 リリース後、「潜在バグは1つ残らず叩き潰す！」の勢いで全コードを総点検＆分散監査し、修正に修正を重ねまくって極限まで磨き上げた改訂版です。** 🛠️🔥

- 📦 **完全単一実行ファイル化（DLL不要）**: ネイティブライブラリを `.exe` 内に完全内包（`IncludeNativeLibrariesForSelfExtract`）し、DLL なしで単体起動できる真の Single-File 動作を実現
- 🏷️ **表記同期 ＆ Discord API v10 完全準拠**: スプラッシュ画面・Settings 画面のバージョン表記を `v2.4` に統一し、`primary_guild`・公式 CDN `/guild-tag-badges/`・`Discriminator` デフォルトアバター算出・Snowflake/URL 防御を実装
- 🎨 **APNG エンジン＆描画バグ一掃**: `OVER` アルファ合成のオーバーフローによる色反転を根治し、チャンク境界検証・LOH 負荷削減・並列制限・64MB LRU キャッシュ・STA スレッド解放を整備
- ⚡ **省電力化 ＆ 非同期競合の完全解消**: 最小化時・別タブ滞在時の APNG／Gaming RGB 自動一時停止、高速検索連打や入力クリア時のキャンセルレース・ボタン固着・残像を根治
- ☀️ **全5テーマ視認性 ＆ レイアウト崩れ防止**: Light テーマの文字・バッジ視認性改善、全5テーマ間 51 リソースキー完全同期、長文ユーザー名・Windows テキスト拡大時でも崩れない Grid 省略レイアウトを導入
- 🔐 **セキュリティ ＆ 堅牢性の大幅強化**: カスタム Bot トークンの Windows DPAPI 暗号化保存（旧平文からの自動移行・アトミック書き込み・メモリゼロクリア・入力バリデーション・環境変数表示整合）およびスレッドセーフなエラーログ（`error.log` 512KB 1世代ローテーション＆暴走防止ハンドラー）を実装

---

## 🛠️ 技術スタック

| テクノロジー | 用途 |
| :--- | :--- |
| **C# / .NET 8.0** | コアランタイム |
| **WPF (Windows Presentation Foundation)** | デスクトップ GUI & XAML スタイリング |
| **Discord API v10** | ユーザーデータ取得 REST API |
| **XamlAnimatedGif** | アニメーション GIF デコーダー & レンダリング |
| **Emoji.Wpf** | DirectWrite カラー絵文字グリフ描画 |
| **Newtonsoft.Json** | JSON レスポンスのパース & 整形 |

---

## 💻 クレジット & 開発環境

- **Created by**: **Neaneko** ([@Nea623](https://github.com/Nea623))
- **Built with**:
  - **Microsoft Visual Studio 2022** (IDE & C# / .NET Tooling)
  - **Antigravity IDE** (Agentic AI Development Environment)

---

<div align="center">
  <sub>Developed with ⚡ for the Discord Community by Neaneko</sub>
</div>
