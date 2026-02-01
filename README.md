# HaishinKit.unity

Unity plugin for RTMP live streaming, powered by [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift).

Stream Unity's rendered content (RenderTexture) and game audio directly to YouTube Live, Twitch, and other RTMP servers.

## Features

- **Texture Streaming**: Stream Unity's RenderTexture output via RTMP
- **Game Audio Capture**: Capture and stream game audio from AudioListener
- **Camera Streaming**: Direct camera/microphone streaming (iOS)
- **Real-time Control**: Bitrate, frame rate, zoom, torch control

## Platform Support

| Platform | Status |
|----------|--------|
| iOS | ✅ Supported |
| macOS | ✅ Supported (Editor & Standalone) |
| Android | ✅ Supported |

## Requirements

- Unity 2021.3 or later
- iOS 15.0+ / macOS 12.0+
- Android 5.0+ (API 21+)
- Xcode 15.0+ (for building iOS/macOS native plugin)
- Android Studio (for building Android plugin)

## Installation

### Option 1: Copy Assets

1. Copy `UnityProject/Assets/HaishinKit` to your project's `Assets` folder
2. Copy `UnityProject/Assets/Plugins` to your project's `Assets` folder

### Option 2: Unity Package (Coming Soon)

## Quick Start

### Texture Streaming (Recommended)

```csharp
using HaishinKit;

public class MyStreaming : MonoBehaviour
{
    private HaishinKitManager _manager;
    private RenderTexture _renderTexture;

    void Start()
    {
        _manager = HaishinKitManager.Instance;

        // Create RenderTexture and assign to camera
        _renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.BGRA32);
        Camera.main.targetTexture = _renderTexture;

        // Setup events
        _manager.OnConnected += () => Debug.Log("Connected");
        _manager.OnPublishingStarted += () => Debug.Log("Streaming started");
    }

    public void StartStreaming()
    {
        _manager.Connect("rtmp://your-server/live", "stream-key");
    }

    public void OnConnectedToServer()
    {
        _manager.SetVideoBitrate(2000); // kbps
        _manager.SetAudioBitrate(128);  // kbps
        _manager.StartPublishingWithTexture(1280, 720);
    }

    void Update()
    {
        if (_isPublishing)
        {
            _manager.SendVideoFrame(_renderTexture.GetNativeTexturePtr());
        }
    }
}
```

### Audio Capture

Audio is automatically captured when `AudioStreamCapture` is attached to the same GameObject as `AudioListener`:

```csharp
// AudioStreamCapture is auto-added by TextureStreamingTest
// Or manually add it to your AudioListener's GameObject
var audioCapture = audioListenerObject.AddComponent<AudioStreamCapture>();
audioCapture.StartCapture(); // Call when publishing starts
audioCapture.StopCapture();  // Call when publishing stops
```

## Sample Scenes

- `TextureStreamingTest`: Complete example for streaming Unity rendering
- `HaishinKitTestScene`: Basic camera/microphone streaming test

## Building Native Plugin

The native plugin source is in `NativePlugin/HaishinKitUnity/`.

### macOS

```bash
cd NativePlugin/HaishinKitUnity
swift build -c release
# Output: .build/release/libHaishinKitUnity.dylib
```

### iOS

```bash
cd NativePlugin/HaishinKitUnity
xcodebuild -scheme HaishinKitUnity -configuration Release \
  -destination 'generic/platform=iOS' \
  -derivedDataPath build-ios \
  BUILD_LIBRARY_FOR_DISTRIBUTION=YES
# Output: build-ios/Build/Products/Release-iphoneos/PackageFrameworks/HaishinKitUnity.framework
```

## API Reference

### HaishinKitManager

| Method | Description |
|--------|-------------|
| `Connect(url, streamKey)` | Connect to RTMP server |
| `Disconnect()` | Disconnect from server |
| `StartPublishingWithTexture(width, height)` | Start texture streaming |
| `SendVideoFrame(texturePtr)` | Send video frame |
| `StopPublishing()` | Stop streaming |
| `SetVideoBitrate(kbps)` | Set video bitrate |
| `SetAudioBitrate(kbps)` | Set audio bitrate |
| `SetFrameRate(fps)` | Set frame rate |

### AudioStreamCapture

| Property/Method | Description |
|-----------------|-------------|
| `Volume` | Audio volume (0.0 - 2.0) |
| `StartCapture()` | Start audio capture |
| `StopCapture()` | Stop audio capture |
| `IsCapturing` | Check if capturing |

## License

BSD 3-Clause License. See [LICENSE](LICENSE) for details.

This project uses [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) (BSD 3-Clause License).

## Acknowledgments

- [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) - Core streaming library by [@shogo4405](https://github.com/shogo4405)
