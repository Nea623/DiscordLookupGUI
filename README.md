# ⚡ DISCORD LOOKUP GUI

<div align="center">

![Version](https://img.shields.io/badge/version-v2.4-4DF2D3?style=for-the-badge&logo=discord&logoColor=black)
![.NET 8](https://img.shields.io/badge/.NET-8.0_WPF-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows_x64-0078D4?style=for-the-badge&logo=windows&logoColor=white)
![Single File](https://img.shields.io/badge/Release-Single--File_exe-10B981?style=for-the-badge)

<br />

**近未来サイバーパンク調の洗練された Discord ユーザー情報検索（OSINT）デスクトップクライアント**

[ダウンロード (Releases)](https://github.com/Nea623/DiscordLookupGUI/releases/latest) • [機能紹介](#-主な機能) • [使い方](#-使い方) • [クレジット](#-クレジット)

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
- 🚀 **単一実行ファイル (Single-File) 自己完結型配布**
  - .NET 8 ランタイム同梱のため、面倒なインストール作業は一切不要
  - 配布された `DiscordLookupGUI.exe` 1つをクリックするだけで即座に起動可能

---

## 🚀 使い方

### 1. ダウンロード
[GitHub Releases (v2.4)](https://github.com/Nea623/DiscordLookupGUI/releases/latest) から **`DiscordLookupGUI.exe`** をダウンロードします。

> [!NOTE]
> **Windows SmartScreen の警告が表示された場合**  
> 初回起動時に「Windows によって PC が保護されました」という青いセキュリティ画面が表示される場合があります。本アプリは完全なオープンソースであり安全ですが、個人開発のためコード署名証明書を取得していないこと・公開直後で実績数が少ないことによる Windows の標準的な挙動です。  
> **起動方法**: 画面内の **「詳細情報」** をクリック ➔ 右下に表示される **「実行」** ボタンをクリックすることで安全に起動できます。

### 2. トークン設定不要！そのまま即座に検索可能
**共有 Bot トークンが内蔵されているため、面倒なトークン取得・設定作業は一切不要です。**
- **独自の Bot トークンを使いたい場合（任意）**:
  - 上部タブの `Settings` からご自身のトークンを入力して保存するか、環境変数 `DISCORD_BOT_TOKEN` を設定することで優先使用できます。空欄で保存またはリセットすればいつでも共有トークンに戻せます。

### 3. ユーザー検索
1. 上部の検索バーに調べたいユーザーの **Discord ユーザーID (Snowflake)** を入力
2. `Enter` キーを押すか、`LOOKUP USER` ボタンをクリック
3. アバター、バナー、クラン情報、アカウント作成日、アナリティクスが美しくフェードイン表示されます！
4. 検索バーを空欄にすると、スムーズにフェードアウトして初期画面に戻ります。

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
