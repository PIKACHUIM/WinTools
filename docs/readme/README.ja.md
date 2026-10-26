<p align="center">
  <a href="../../README.md">简体中文</a> · <a href="README.zh-TW.md">繁體中文</a> · <a href="README.en.md">English</a> · <b>日本語</b> · <a href="README.ko.md">한국어</a> · <a href="README.de.md">Deutsch</a><br/>
  <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <a href="README.pt.md">Português</a> · <a href="README.it.md">Italiano</a> · <a href="README.ru.md">Русский</a> · <a href="README.ar.md">العربية</a>
</p>

<p align="center">
  <img src="../public/logo.png" width="88" height="88" alt="WinTools" />
</p>

<h1 align="center">WinTools</h1>

<p align="center">
  <a href="https://github.com/PIKACHUIM/WinTools/releases"><img src="https://img.shields.io/badge/version-0.1.0-orange" alt="バージョン"></a>
  <a href="https://www.microsoft.com/windows"><img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4" alt="プラットフォーム"></a>
  <a href="https://dotnet.microsoft.com"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-AGPL--3.0-blue" alt="ライセンス"></a>
  <a href="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml"><img src="https://github.com/PIKACHUIM/WinTools/actions/workflows/build.yml/badge.svg" alt="ビルド"></a>
  <a href="https://wtools.opkg.cn/"><img src="https://img.shields.io/badge/docs-online-41B883" alt="ドキュメント"></a>
</p>

## 概要

WinTools はよく使うシステム保守を 1 つのウィンドウにまとめます。キャッシュと更新の残骸の削除、不要なテレメトリの停止、問題のある更新の削除、スタートアップの確認、ランタイムの追加インストール、古いドライバーのエクスポート、再インストール前のイメージ作成。

WPF デスクトップ アプリで、すべてローカルで動作します。サービスも常駐プロセスも起動項目もネットワーク通信もありません。ウィンドウを閉じれば終了します。

10 個のモジュール: **クリーンアップ、最適化、機能管理、更新管理、起動管理、コンポーネント管理、ランタイム、アプリ管理、ドライバー管理、バックアップ**.

## 動作環境

- Windows 10 1809 (17763) 以降、または Windows 11、x64
- フレームワーク依存版は [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) が必要
- 管理者権限が必要（マニフェストで宣言済み）。DISM、ドライバー、サービス、イメージ操作が依存
- 一部機能は Windows 標準コンポーネント（DISM、Windows Update Agent、`pnputil`、WIMGAPI）を呼びます。縮小版では削除されている場合があります

## ダウンロード

| 版 | サイズ | 説明 | リンク |
| --- | --- | --- | --- |
| ポータブル | 約 70 MB | ランタイム同梱、解凍して実行 | [WinTools-*-portable-win-x64.exe](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| フレームワーク依存 | 約 22 MB | .NET 10 Desktop Runtime が必要 | [WinTools-*-framework-win-x64.zip](https://github.com/PIKACHUIM/WinTools/releases/latest) |
| Beta | 同上 | `main` への push ごとに再ビルド | [releases/tag/beta](https://github.com/PIKACHUIM/WinTools/releases/tag/beta) |

正式版は Certum のクラウド署名とタイムスタンプ付き。`Get-AuthenticodeSignature` で検証できます。各リリースに `SHA256SUMS.txt` を添付しています。

## 機能

### システム概要

システムのバージョンとビルド番号、CPU とメモリの使用率、各ディスクの空き容量、最近のアクティビティ。データはすべてローカルで読み取ります。

<details>
<summary>スクリーンショット</summary>

![システム概要](../public/screenshots/dashboard.png)

</details>

### クリーンアップ

ディスク単位でスキャンし、システムの一時フォルダー、更新キャッシュ、ログとダンプ、ブラウザーのキャッシュ、開発ツールのキャッシュ（npm、pip、NuGet、Gradle、Docker）、150 以上のアプリのキャッシュを対象にします。

結果は分類ごとにまとまり、サイズと安全レベルを表示します。使用中のファイルはスキップして記録し、処理全体を止めません。ルールは `src/WinTools.Infrastructure/Rules/` の JSON で、パスの追加にコード変更は不要です。

<details>
<summary>スクリーンショット</summary>

![クリーンアップ](../public/screenshots/cleanup.png)

</details>

### 最適化

プライバシーとテレメトリ、パフォーマンス、エクスプローラー、システム動作、ネットワーク、更新、ゲーム。

各項目は検出・適用・元に戻すに対応し、変更前に現在の状態を確認できます。推奨項目は影響が明確なものだけです。

<details>
<summary>スクリーンショット</summary>

![最適化](../public/screenshots/optimize.png)

</details>

### 機能管理

- **システム機能**: DISM API でオプション機能を一覧表示し、用途別にグループ化。説明と現在の状態を表示し、まとめて適用します。
- **エディション チャネル**: 現在の SKU、バージョン、ライセンス状態を読み取ります。切り替えは標準の `changepk` と汎用キーを使い、ライセンス認証は行いません。

<details>
<summary>スクリーンショット</summary>

![システム機能](../public/screenshots/features-systemfeatures.png)

</details>

### 更新管理

- **インストール済み更新**: 一覧表示と個別の削除。
- **利用可能な更新**: 手動で Windows Update を検索し、分類、KB、サイズ、日付を表示。一括インストールと結果の要約。
- **機能フラグ**: `FeatureManagement\Overrides` を ID 単位で管理。状態、優先度、バリアントに対応し、インポート、エクスポート、リセットが可能。

<details>
<summary>スクリーンショット</summary>

![利用可能な更新](../public/screenshots/updates-available.png)

</details>

### 起動管理

1 ページに集約。実際のプログラム アイコンを表示し、有効状態はスイッチで操作します。サービスは行内でスタートアップの種類（自動、自動（遅延）、手動、無効）を変更できます。

<details>
<summary>スクリーンショット</summary>

![起動管理](../public/screenshots/startup-services.png)

</details>

### コンポーネント管理

- **セキュリティ センター**: 14 項目。リアルタイム保護、クラウド保護、行動監視、PUA、ネットワーク保護、コントロールされたフォルダー アクセス、メモリ整合性、UAC、SmartScreen、3 つのファイアウォール プロファイル、除外項目。
- **システム コンポーネント**: 組み込みアプリ（ターミナル、電卓、メモ帳、ペイント、フォトなど）のインストール、修復、削除。WSL ディストリビューション。ローカル パッケージからの Android サブシステム。ストアの修復。

<details>
<summary>スクリーンショット</summary>

![セキュリティ センター](../public/screenshots/components-securitycenter.png)

</details>

### ランタイム

インストール済みの .NET と Visual C++ ランタイムを一覧表示します。利用可能な版は必要時に取得します。.NET は公式 `releases-index.json` を読み SHA512 を検証、VC++ は `aka.ms` のリンクを使い Authenticode を検証します。どちらもサイレント インストールでき、アンインストールも可能です。

<details>
<summary>スクリーンショット</summary>

![ランタイム](../public/screenshots/runtimes-dotnet.png)

</details>

### アプリ管理

アイコン、バージョン、サイズ、インストール日、最近の使用を表示します。アンインストールは各ソフトのアンインストーラーを呼び、パッケージは配置 API を使用します。

<details>
<summary>スクリーンショット</summary>

![アプリ管理](../public/screenshots/apps-programs.png)

</details>

### ドライバー管理

DriverStore 全体を管理します。任意の数のパッケージを選んでバックアップ、削除、またはフォルダーからインストールできます。一括操作は項目ごとの進捗と結果の要約を表示します。

<details>
<summary>スクリーンショット</summary>

![ドライバー管理](../public/screenshots/drivers-systemdrivers.png)

</details>

### バックアップ

ソース ボリュームはドロップダウンで選択します。イメージ形式（wim / esd / swm）に応じて拡張子と圧縮方式が変わり、分割サイズは swm のみ有効です。作成前に既定の除外項目を表示します。適用時はイメージの検証と `bcdboot` によるブート修復が可能です。

<details>
<summary>スクリーンショット</summary>

![バックアップ](../public/screenshots/backup-capture.png)

</details>

## ビルド

.NET 10 SDK が必要です。

```powershell
# デバッグ ビルド
dotnet build src/WinTools.App/WinTools.App.csproj

# Release ビルド
dotnet build src/WinTools.App/WinTools.App.csproj -c Release

# 2 種類のパッケージを artifacts/publish/{portable,framework} に生成
powershell -ExecutionPolicy Bypass -File tools/publish.ps1 -Profile both
```

アーキテクチャ、モジュール実装、ルール形式、ローカライズ、デバッグの詳細は **[wtools.opkg.cn](https://wtools.opkg.cn/)** または [docs/guide/development.md](../guide/development.md) を参照してください。

## 参考プロジェクト

| プロジェクト | 参考にした点 |
| --- | --- |
| [winutil](https://github.com/ChrisTitusTech/winutil) | 最適化項目のグループ化、選択してから適用する流れ |
| [Optimizer](https://github.com/hellzerg/optimizer) | 最適化の分類、リスク表記、スタートアップとサービスの構成 |
| [ZyperWinOptimize](https://github.com/ZyperRap/ZyperWinOptimize) | 中国語環境での最適化リストと情報密度 |
| [Dism-Multi-language](https://github.com/Chuyu-Team/Dism-Multi-language) | DISM の使い方と多言語リソースの構成 |
| [Geek Uninstaller](https://geekuninstaller.com/) | インストール済みプログラム一覧の項目選定 |
| [ViVeTool](https://github.com/thebookisclosed/ViVe) | 機能フラグのデータ構造と三状態の扱い |

クリーンアップ ルールは個人でまとめたキャッシュ パスの一覧が出発点で、その後追加を重ねています。現在は `tools/rules/*.txt` で管理し、`tools/Generate-CleanupRules.ps1` で生成しています。

## 免責事項

- 本ツールはレジストリ、サービスのスタートアップの種類、システム機能、ドライバーを変更します。使用前に復元ポイントを作成するか重要なデータをバックアップしてください。
- 本ツールの使用により生じたデータの損失やシステムの破損について、作者は責任を負いません。
- ソフトウェアのライセンスやライセンス認証の回避、その他現地の法令に違反する目的での使用は禁止します。

## ライセンス

[AGPL-3.0](../../LICENSE) © WinTools

### サードパーティ コンポーネント

| コンポーネント | ライセンス | 用途 |
| --- | --- | --- |
| [Maple Mono NF CN](https://github.com/subframe7536/maple-font) | SIL OFL 1.1 | UI フォント（TTF で埋め込み） |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4.2 | MIT | MVVM ソース ジェネレーター |
| [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) 10.0.13 | MIT | 依存関係の注入 |
| [VitePress](https://vitepress.dev/) | MIT | ドキュメント サイトのみ |
