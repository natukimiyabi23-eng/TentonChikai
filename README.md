# 天遁地改

移動送信時のY座標へ指定値を加算する、Dalamud API 15向けの独立プラグインです。

## ダウンロード

[GitHub Releases](https://github.com/natukimiyabi23-eng/TentonChikai/releases/latest) から `TentonChikai-v1.0.0.zip` をダウンロードしてください。

## 導入方法

1. ZIPを任意のフォルダーへ展開します。
2. Dalamudの開発用プラグインに、展開した `YMoveStandalone.dll` を登録します。
3. `/ymove` で設定画面を開きます。
4. Y補正値を設定してから「機能を有効にする」をオンにします。

初期状態は無効、Y補正値は `0` です。

## 安全制御

「ディープダンジョン安全制御」がオンの場合、次の状態では座標を変更しません。

- ディープダンジョン外
- 安全地帯
- 10階ごとの区切り階
- 対象コンテンツのB99終端
- 現在階層を取得できない場合

## 注意

- ゲーム更新でシグネチャが変わった場合、プラグインはフックを作らず利用不可になります。
- I-Ching-GL側の同じ機能と同時に有効化しないでください。
- 移動送信データを書き換えるため、サーバー補正や切断が起こる可能性があります。

## ビルド

`.NET 10 SDK` とDalamudの開発環境が必要です。

```powershell
dotnet build .\YMoveStandalone.csproj --configuration Release
```

配布用ZIPは `bin\Release\YMoveStandalone\latest.zip` に生成されます。
