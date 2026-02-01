# HaishinKit Unity Plugin

> **Note**: This is an unofficial community project, not affiliated with the original HaishinKit authors.

Unity plugin for RTMP live streaming, powered by [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) and [HaishinKit.kt](https://github.com/shogo4405/HaishinKit.kt).

## Installation

### Unity Package Manager (Git URL)

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.otanl.haishinkit": "https://github.com/otanl/HaishinKit.unity.git?path=UnityProject/Assets/HaishinKit#v0.1.0"
  }
}
```

Or via Unity Editor:
1. Window > Package Manager
2. "+" > Add package from git URL
3. Enter: `https://github.com/otanl/HaishinKit.unity.git?path=UnityProject/Assets/HaishinKit`

## Quick Start

### Setup

```csharp
using HaishinKit;

// HaishinKitManager は自動的にシングルトンとして初期化されます
var manager = HaishinKitManager.Instance;

// イベント登録
manager.OnConnected += () => Debug.Log("Connected");
manager.OnPublishingStarted += () => Debug.Log("Streaming started");
manager.OnError += (error) => Debug.LogError(error);
```

### Texture Streaming (RenderTexture)

```csharp
// RenderTexture を作成してカメラに割り当て
var renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.BGRA32);
Camera.main.targetTexture = renderTexture;

// 接続
manager.Connect("rtmp://your-server/live", "stream-key");

// 接続完了後に配信開始
manager.SetVideoBitrate(2000);  // kbps
manager.SetAudioBitrate(128);   // kbps
manager.StartPublishingWithTexture(1280, 720);

// Update() でフレーム送信
void Update()
{
    if (isPublishing)
    {
        manager.SendVideoFrame(renderTexture.GetNativeTexturePtr());
    }
}
```

### Camera/Microphone Streaming (iOS)

```csharp
// 接続
manager.Connect("rtmp://your-server/live", "stream-key");

// 配信開始
manager.StartPublishing();

// カメラ操作
manager.SwitchCamera();
manager.SetZoom(2.0f);
manager.SetTorch(true);

// 配信停止
manager.StopPublishing();
manager.Disconnect();
```

## Platform Support

| Platform | Status | Notes |
|----------|--------|-------|
| iOS | ✅ | Metal texture streaming |
| macOS | ✅ | Editor & Standalone |
| Android | ✅ | Bitmap / OpenGL texture |

## Requirements

- Unity 2021.3+
- iOS 15.0+ / macOS 12.0+
- Android 5.0+ (API 21+)

## API Reference

| Method | Description |
|--------|-------------|
| `Connect(url, streamKey)` | Connect to RTMP server |
| `Disconnect()` | Disconnect from server |
| `StartPublishing()` | Start camera/mic streaming |
| `StartPublishingWithTexture(w, h)` | Start texture streaming |
| `SendVideoFrame(texturePtr)` | Send video frame |
| `StopPublishing()` | Stop streaming |
| `SetVideoBitrate(kbps)` | Set video bitrate |
| `SetAudioBitrate(kbps)` | Set audio bitrate |
| `SetFrameRate(fps)` | Set frame rate |

## Troubleshooting

### DllNotFoundException

Make sure native plugins are properly imported:
- iOS: `Runtime/Plugins/iOS/HaishinKitUnity/HaishinKitUnity.framework`
- macOS: `Runtime/Plugins/libHaishinKitUnity.dylib`
- Android: `Runtime/Plugins/Android/*.aar`

### Framework not found (iOS)

In Xcode, check Framework embed settings:
Target > General > Frameworks, Libraries, and Embedded Content > "Embed & Sign"

## License

BSD 3-Clause License. See [LICENSE](https://github.com/otanl/HaishinKit.unity/blob/main/LICENSE).

## Credits

- [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) by [@shogo4405](https://github.com/shogo4405)
- [HaishinKit.kt](https://github.com/shogo4405/HaishinKit.kt) by [@shogo4405](https://github.com/shogo4405)
