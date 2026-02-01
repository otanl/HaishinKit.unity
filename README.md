# HaishinKit.unity

Unity plugin for RTMP live streaming, powered by [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) and [HaishinKit.kt](https://github.com/shogo4405/HaishinKit.kt).

Stream Unity's rendered content (RenderTexture) and game audio directly to YouTube Live, Twitch, and other RTMP servers.

## Features

- **Texture Streaming**: Stream Unity's RenderTexture output via RTMP
- **Game Audio Capture**: Capture and stream game audio from AudioListener
- **Camera Streaming**: Direct camera/microphone streaming (iOS)
- **Real-time Control**: Bitrate, frame rate, zoom, torch control
- **Cross-Platform**: Unified C# API for iOS, macOS, and Android

## Platform Support

| Platform | Status | Notes |
|----------|--------|-------|
| iOS | ✅ Supported | Metal texture streaming |
| macOS | ✅ Supported | Editor & Standalone |
| Android | ✅ Supported | Bitmap / OpenGL texture |

## Requirements

- Unity 2021.3 or later
- iOS 15.0+ / macOS 12.0+
- Android 5.0+ (API 21+)
- Xcode 16.0+ / Swift 6 (for building iOS/macOS native plugin)
- Android Studio (for building Android plugin)

## Architecture

This project consists of three related repositories:

```
HaishinKit.swift (fork)     HaishinKit.kt (fork)
      │                            │
      │ unity-support              │ unity-support
      │ branch                     │ branch (unity/ module)
      ▼                            ▼
┌─────────────────────────────────────────────┐
│           HaishinKit.unity                  │
│  ├── NativePlugin/      (Swift source)      │
│  └── UnityProject/      (C# + binaries)     │
└─────────────────────────────────────────────┘
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed documentation.

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

## Building Native Plugins

### Using Build Scripts (Recommended)

```bash
# iOS/macOS
./scripts/build-ios.sh all    # Build both iOS and macOS
./scripts/build-ios.sh ios    # iOS only
./scripts/build-ios.sh macos  # macOS only

# Android (requires HaishinKit.kt clone)
./scripts/build-android.sh /path/to/HaishinKit.kt
```

### Manual Build

#### macOS

```bash
cd NativePlugin/HaishinKitUnity
swift package resolve
swift build -c release --arch arm64 --arch x86_64
# Output: .build/release/libHaishinKitUnity.dylib
```

#### iOS

```bash
cd NativePlugin/HaishinKitUnity
xcodebuild -scheme HaishinKitUnity -configuration Release \
  -sdk iphoneos \
  -destination 'generic/platform=iOS' \
  -derivedDataPath build-ios \
  BUILD_LIBRARY_FOR_DISTRIBUTION=YES
# Output: build-ios/Build/Products/Release-iphoneos/PackageFrameworks/HaishinKitUnity.framework
```

#### Android

```bash
# Clone HaishinKit.kt fork with unity-support branch
git clone -b unity-support https://github.com/otanl/HaishinKit.kt.git

cd HaishinKit.kt
./gradlew :unity:assembleRelease
./gradlew :haishinkit:assembleRelease
./gradlew :rtmp:assembleRelease

# Copy AARs to Unity project
cp unity/build/outputs/aar/unity-release.aar /path/to/HaishinKit.unity/UnityProject/Assets/Plugins/Android/HaishinKitUnity.aar
cp haishinkit/build/outputs/aar/haishinkit-release.aar /path/to/HaishinKit.unity/UnityProject/Assets/Plugins/Android/haishinkit.aar
cp rtmp/build/outputs/aar/rtmp-release.aar /path/to/HaishinKit.unity/UnityProject/Assets/Plugins/Android/rtmp.aar
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

## Related Repositories

| Repository | Branch | Description |
|------------|--------|-------------|
| [otanl/HaishinKit.swift](https://github.com/otanl/HaishinKit.swift) | unity-support | iOS/macOS core library fork |
| [otanl/HaishinKit.kt](https://github.com/otanl/HaishinKit.kt) | unity-support | Android core library fork with Unity module |

## License

BSD 3-Clause License. See [LICENSE](LICENSE) for details.

This project uses:
- [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) (BSD 3-Clause License)
- [HaishinKit.kt](https://github.com/shogo4405/HaishinKit.kt) (BSD 3-Clause License)

## Acknowledgments

- [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) - Core streaming library by [@shogo4405](https://github.com/shogo4405)
- [HaishinKit.kt](https://github.com/shogo4405/HaishinKit.kt) - Android streaming library by [@shogo4405](https://github.com/shogo4405)
