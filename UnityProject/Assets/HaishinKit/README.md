# HaishinKit Unity Plugin

HaishinKit.swift を Unity から利用するためのネイティブプラグイン。

## セットアップ

### 1. HaishinKitManager をシーンに追加

空の GameObject を作成し、`HaishinKitManager` コンポーネントをアタッチします。
または、スクリプトから自動作成されます。

```csharp
// 自動作成される場合
if (HaishinKitManager.Instance == null)
{
    var go = new GameObject("HaishinKitManager");
    go.AddComponent<HaishinKitManager>();
}
```

### 2. テスト用スクリプト

`HaishinKitTestScene.cs` を空の GameObject にアタッチすると、
OnGUI でシンプルなテスト UI が表示されます。

## 使用方法

```csharp
using HaishinKit;

// 接続
HaishinKitManager.Instance.Connect("rtmp://your-server/app", "stream-key");

// 配信開始
HaishinKitManager.Instance.StartPublishing();

// 設定変更
HaishinKitManager.Instance.SetVideoBitrate(1500);  // kbps
HaishinKitManager.Instance.SetAudioBitrate(128);   // kbps
HaishinKitManager.Instance.SetFrameRate(30);

// カメラ操作
HaishinKitManager.Instance.SwitchCamera();
HaishinKitManager.Instance.SetZoom(2.0f);
HaishinKitManager.Instance.SetTorch(true);

// 配信停止
HaishinKitManager.Instance.StopPublishing();

// 切断
HaishinKitManager.Instance.Disconnect();
```

## イベント

```csharp
HaishinKitManager.Instance.OnConnected += () => Debug.Log("Connected");
HaishinKitManager.Instance.OnDisconnected += () => Debug.Log("Disconnected");
HaishinKitManager.Instance.OnPublishingStarted += () => Debug.Log("Publishing");
HaishinKitManager.Instance.OnPublishingStopped += () => Debug.Log("Stopped");
HaishinKitManager.Instance.OnError += (error) => Debug.LogError(error);
HaishinKitManager.Instance.OnStatusChanged += (status) => Debug.Log(status);
```

## iOS ビルド設定

### 自動設定される項目（PostProcessBuild）

- `NSCameraUsageDescription` - カメラ権限
- `NSMicrophoneUsageDescription` - マイク権限
- `ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES = YES`

### 手動確認が必要な項目

1. **Xcode > Target > General > Frameworks, Libraries, and Embedded Content**
   - `HaishinKitUnity.framework` が "Embed & Sign" になっていること

2. **Signing & Capabilities**
   - 有効な Provisioning Profile が設定されていること

## 対応プラットフォーム

- iOS 15.0+
- ※ macOS Editor では動作しません（iOS 実機でテストしてください）

## API 一覧

| メソッド | 説明 |
|---------|------|
| `Connect(url, streamName)` | RTMP サーバーに接続 |
| `Disconnect()` | 切断 |
| `StartPublishing()` | 配信開始 |
| `StopPublishing()` | 配信停止 |
| `SetVideoBitrate(kbps)` | ビデオビットレート設定 |
| `SetAudioBitrate(kbps)` | オーディオビットレート設定 |
| `SetFrameRate(fps)` | フレームレート設定 |
| `SwitchCamera()` | カメラ切り替え |
| `SetZoom(level)` | ズーム設定 (1.0-5.0) |
| `SetTorch(enabled)` | トーチ設定 |
| `GetVersion()` | バージョン取得 |

## トラブルシューティング

### "DllNotFoundException" エラー

iOS 実機でのみ動作します。Unity Editor では動作しません。

### 配信が開始されない

1. カメラ/マイク権限が許可されているか確認
2. RTMP URL とストリームキーが正しいか確認
3. `OnError` イベントでエラーメッセージを確認

### Framework が見つからない

Xcode で Framework の Embed 設定を確認してください。
