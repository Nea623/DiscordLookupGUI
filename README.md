# ⚡ DISCORD LOOKUP GUI

<div align="center">

![Version](https://img.shields.io/badge/version-v2.2-4DF2D3?style=for-the-badge&logo=discord&logoColor=black)
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

- 🌌 **近未来・サイバーパンク UI (AETHER)**
  - 発光ネオンシアンアクセント、真円アバタークリップ、センター配置バナー、角丸ウィンドウ
  - 起動時の 1.5 秒ネオンロゴスプラッシュ演出 ＆ スムーズなフェードインアニメーション
- 🌗 **ダークテーマ & ライトテーマの動的切り替え**
  - **Dark Mode**: サイバーネイビー ＆ ネオンシアンの近未来ダークUI
  - **Light Mode**: 洗練されたフロストホワイト ＆ エレクトリックティールのクリーンUI
  - タイトルバー右上のボタン、または Settings 画面から横方向にスッと展開するスマートなメニュー
  - 選択したテーマはローカル（`theme_settings.json`）に自動保存され、次回起動時もシームレスに復元
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
  - 配布された `DiscordLookup.exe` 1つをクリックするだけで即座に起動可能

---

## 🚀 使い方

### 1. ダウンロード
[GitHub Releases (v2.2)](https://github.com/Nea623/DiscordLookupGUI/releases/latest) から **`DiscordLookup.exe`** をダウンロードします。

### 2. Discord Bot トークンの設定
ユーザー情報の取得には Discord Bot のトークンが必要です。以下のいずれかの方法で設定できます：
- **アプリ内から設定**:
  1. 上部タブの `Settings` をクリック
  2. `Discord Bot Token` 欄にトークンを入力し、`Save Bot Token` をクリック
- **環境変数から設定**:
  - システム環境変数 `DISCORD_BOT_TOKEN` にトークンを設定しておくと、起動時に自動で読み込まれます。

### 3. ユーザー検索
1. 上部の検索バーに調べたいユーザーの **Discord ユーザーID (Snowflake)** を入力
2. `Enter` キーを押すか、`LOOKUP USER` ボタンをクリック
3. アバター、バナー、クラン情報、アカウント作成日、アナリティクスが美しく表示されます！

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
