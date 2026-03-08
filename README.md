# HaishinKit.unity

> **Note**: This is an unofficial community project. It is not affiliated with or endorsed by the original HaishinKit authors. This project provides Unity bindings for HaishinKit libraries.

Unity plugin for RTMP live streaming, powered by [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) and [HaishinKit.kt](https://github.com/shogo4405/HaishinKit.kt).

Stream Unity's rendered content (RenderTexture) and game audio directly to YouTube Live, Twitch, and other RTMP servers.

## Features

- **Texture Streaming**: Stream Unity's RenderTexture output via RTMP
- **Game Audio Capture**: Capture and stream game audio from AudioListener
- **Camera Streaming**: Direct camera/microphone streaming (iOS)
- **Real-time Control**: Bitrate, frame rate, zoom, torch control
- **Cross-Platform**: Unified C# API for iOS, macOS, and Android
- **State Management**: Typed `StreamingStatus` enum with state machine
- **Diagnostics**: Real-time streaming statistics and audio capture metrics

## Platform Support

| Platform | Status | Notes |
|----------|--------|-------|
| iOS | Supported | Metal texture streaming |
| macOS | Supported | Editor & Standalone |
| Android | Supported | ReadPixels / AsyncGPU / NativeTexture |

### Platform Capabilities

| Feature | iOS | macOS | Android |
|---------|-----|-------|---------|
| Texture Streaming | Metal | Metal | ReadPixels / AsyncGPU / NativeTexture |
| Camera Streaming | Yes | Yes | No (texture mode only) |
| Game Audio | Yes | Yes | Yes |
| SwitchCamera | Yes | Yes | N/A |
| Zoom / Torch | Yes | N/A | N/A |
| Native Texture (zero-copy) | Yes (Metal) | Yes (Metal) | OpenGL ES only |

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
      |                            |
      | unity-support              | unity-support
      | branch                     | branch (unity/ module)
      v                            v
+---------------------------------------------+
|           HaishinKit.unity                  |
|  +-- NativePlugin/      (Swift source)      |
|  +-- UnityProject/      (C# + binaries)     |
+---------------------------------------------+
```

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed documentation.

## Installation

### Option 1: Unity Package Manager (Recommended)

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
2. Click "+" > Add package from git URL
3. Enter: `https://github.com/otanl/HaishinKit.unity.git?path=UnityProject/Assets/HaishinKit`

### Option 2: Manual Installation

1. Download the latest release from [Releases](https://github.com/otanl/HaishinKit.unity/releases)
2. Copy `UnityProject/Assets/HaishinKit` to your project's `Assets` folder

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
        // HaishinKitManager is auto-created via RuntimeInitializeOnLoadMethod
        _manager = HaishinKitManager.Instance;

        // Create RenderTexture and assign to camera
        _renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.BGRA32);
        Camera.main.targetTexture = _renderTexture;

        // Setup events (enum-based, recommended)
        _manager.OnStreamingStatusChanged += (status) =>
        {
            Debug.Log($"Status: {status}");
            if (status == StreamingStatus.Connected)
            {
                _manager.SetVideoBitrate(2000);
                _manager.StartPublishingWithTexture(1280, 720);
            }
        };

        // Or use individual events
        _manager.OnError += (error) => Debug.LogError(error);
    }

    public void StartStreaming()
    {
        _manager.Connect("rtmp://your-server/live", "stream-key");
    }

    void Update()
    {
        if (_manager.Status == StreamingStatus.Publishing)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _manager.SendVideoFrame(_renderTexture);
#else
            _manager.SendVideoFrame(_renderTexture.GetNativeTexturePtr());
#endif
        }
    }
}
```

### Audio Capture

Audio is automatically captured when `AudioStreamCapture` is attached to the same GameObject as `AudioListener`:

```csharp
var audioCapture = audioListenerObject.AddComponent<AudioStreamCapture>();
audioCapture.StartCapture(); // Call when publishing starts
audioCapture.StopCapture();  // Call when publishing stops

// Monitor audio stats
var stats = audioCapture.GetStats();
Debug.Log($"Sent: {stats.SentFrames}, Overruns: {stats.BufferOverruns}");
```

## Sample Scenes

Import samples via Package Manager > HaishinKit > Samples:

- **Texture Streaming**: Complete example for streaming Unity rendering with stats display
- **Camera Streaming**: Camera/microphone streaming with UI controls (iOS/macOS)
- **Audio Only**: Minimal audio-only streaming example

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

| Property/Method | Description |
|-----------------|-------------|
| `Instance` | Singleton instance (auto-created) |
| `Status` | Current streaming status (`StreamingStatus` enum) |
| `Stats` | Real-time streaming statistics |
| `IsInitialized` | Whether the native backend is ready |
| `Connect(url, streamKey)` | Connect to RTMP server |
| `Disconnect()` | Disconnect from server |
| `StartPublishingWithTexture(width, height)` | Start texture streaming |
| `SendVideoFrame(texturePtr)` | Send video frame (iOS/macOS Metal pointer) |
| `SendVideoFrame(renderTexture)` | Send video frame (cross-platform) |
| `StopPublishing()` | Stop streaming |
| `SetVideoBitrate(kbps)` | Set video bitrate |
| `SetAudioBitrate(kbps)` | Set audio bitrate |
| `SetFrameRate(fps)` | Set frame rate |

#### Events

| Event | Description |
|-------|-------------|
| `OnStreamingStatusChanged` | `Action<StreamingStatus>` - typed status changes |
| `OnStatusChanged` | `Action<string>` - raw status string (backward compatible) |
| `OnConnected` | Fired when connected to server |
| `OnDisconnected` | Fired when disconnected |
| `OnPublishingStarted` | Fired when publishing starts |
| `OnPublishingStopped` | Fired when publishing stops |
| `OnError` | `Action<string>` - error message |

#### StreamingStatus Enum

| Value | Description |
|-------|-------------|
| `Disconnected` | Not connected |
| `Connecting` | Connection in progress |
| `Connected` | Connected, not publishing |
| `Publishing` | Actively streaming |
| `Stopping` | Stop in progress |
| `Error` | Error occurred |

### AudioStreamCapture

| Property/Method | Description |
|-----------------|-------------|
| `Volume` | Audio volume (0.0 - 2.0) |
| `StartCapture()` | Start audio capture |
| `StopCapture()` | Stop audio capture |
| `IsCapturing` | Check if capturing |
| `GetStats()` | Get `AudioCaptureStats` (captured, sent, overruns, dropped, queue depth) |

#### Inspector Settings

| Setting | Description |
|---------|-------------|
| `Max Buffer Size Override` | 0 = auto from AudioSettings.dspBufferSize |
| `Max Sends Per Frame` | Limit audio sends per frame (default: 8, 0 = unlimited) |
| `Drop Policy` | `None` (process all) or `DropOldest` (low latency) |
| `Max Queue Size` | Max buffers when using DropOldest policy |

### Android-specific

| Method | Description |
|--------|-------------|
| `SetAndroidReadbackMode(mode)` | Set readback mode: ReadPixels, AsyncGPUReadback, NativeTexture, NativePlugin |
| `SetTargetSendFps(fps)` | Throttle video frame sending (0 = every frame) |

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

This project would not be possible without the excellent work of [@shogo4405](https://github.com/shogo4405):

- [HaishinKit.swift](https://github.com/shogo4405/HaishinKit.swift) - The core iOS/macOS RTMP/SRT streaming library
- [HaishinKit.kt](https://github.com/shogo4405/HaishinKit.kt) - The Android RTMP streaming library

> Note: The upstream HaishinKit libraries support RTMP and SRT protocols. This Unity plugin currently supports RTMP only.

All streaming functionality is provided by these libraries. This project only provides Unity bindings and does not modify the core streaming logic.
