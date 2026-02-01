# HaishinKit.unity アーキテクチャ

## 概要

HaishinKit.unityは、HaishinKit（Swift/Kotlin）をUnityから利用するためのプラグインです。

## リポジトリ構成

```
┌─────────────────────────────────────────────────────────────────┐
│                    HaishinKit Ecosystem                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────┐    ┌─────────────────────┐            │
│  │ HaishinKit.swift    │    │ HaishinKit.kt       │            │
│  │ (upstream)          │    │ (upstream)          │            │
│  │ shogo4405/...       │    │ shogo4405/...       │            │
│  └──────────┬──────────┘    └──────────┬──────────┘            │
│             │ fork                      │ fork                  │
│             ▼                           ▼                       │
│  ┌─────────────────────┐    ┌─────────────────────┐            │
│  │ otanl/HaishinKit.   │    │ otanl/HaishinKit.kt │            │
│  │ swift               │    │                     │            │
│  │ (unity-support)     │    │ (unity-support)     │            │
│  │                     │    │                     │            │
│  │ - タイムスタンプ修正 │    │ - unity/ モジュール │            │
│  │ - 圧縮済み動画対応   │    │   - UnityBridge.kt  │            │
│  │                     │    │   - Wrapper.kt      │            │
│  │                     │    │   - Renderers       │            │
│  │                     │    │   - Native C++      │            │
│  └──────────┬──────────┘    └──────────┬──────────┘            │
│             │                           │                       │
│             │    ┌──────────────────────┘                       │
│             │    │                                              │
│             ▼    ▼                                              │
│  ┌─────────────────────────────────────────────────┐           │
│  │              HaishinKit.unity                    │           │
│  │              (otanl/HaishinKit.unity)            │           │
│  │                                                  │           │
│  │  NativePlugin/HaishinKitUnity/                   │           │
│  │    └── Swift Package (iOS/macOS C Interface)    │           │
│  │                                                  │           │
│  │  UnityProject/Assets/                            │           │
│  │    ├── Plugins/iOS/      (Framework)             │           │
│  │    ├── Plugins/Android/  (AAR from HaishinKit.kt)│           │
│  │    └── HaishinKit/       (C# Scripts)            │           │
│  └─────────────────────────────────────────────────┘           │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## 各リポジトリの役割

### 1. HaishinKit.swift (unity-support ブランチ)
**URL**: https://github.com/otanl/HaishinKit.swift/tree/unity-support

**変更内容**:
- RTMPStream: 圧縮済みビデオの経過時間ベースタイムスタンプ
- RTMPStream: publish時のタイムスタンプリセット
- AudioCodec: エラーログ追加
- OutgoingStream: コメント修正

**注意**: このブランチはHaishinKitのコア機能を修正。
iOS/macOS用のC InterfaceはHaishinKit.unity側に配置。

### 2. HaishinKit.kt (unity-support ブランチ)
**URL**: https://github.com/otanl/HaishinKit.kt/tree/unity-support

**追加モジュール**: `unity/`
```
unity/
├── build.gradle.kts
└── src/main/
    ├── java/com/haishinkit/unity/
    │   ├── UnityBridge.kt         # JNIブリッジ
    │   ├── HaishinKitUnityWrapper.kt
    │   ├── AudioEngine.kt
    │   ├── BitmapRenderer.kt      # Bitmap方式
    │   ├── NativeTextureRenderer.kt # OpenGL方式
    │   ├── NativeTexturePlugin.kt
    │   └── VideoRenderer.kt
    └── cpp/
        ├── CMakeLists.txt
        ├── NativeTexturePlugin.cpp # Zero-copy
        └── IUnityGraphics.h
```

**ビルド成果物**: `unity/build/outputs/aar/unity-release.aar`

### 3. HaishinKit.unity (このリポジトリ)
**URL**: https://github.com/otanl/HaishinKit.unity

**構成**:
```
HaishinKit.unity/
├── NativePlugin/
│   └── HaishinKitUnity/           # Swift Package
│       ├── Package.swift          # HaishinKit.swift依存
│       └── Sources/
│           ├── CInterface.swift   # @_cdecl C関数
│           ├── HaishinKitWrapper.swift
│           ├── AudioEngine.swift
│           └── TextureRenderer.swift
│
└── UnityProject/Assets/
    ├── Plugins/
    │   ├── iOS/HaishinKitUnity/   # iOS Framework
    │   │   └── HaishinKitUnity.framework
    │   ├── Android/               # Android AAR
    │   │   ├── HaishinKitUnity.aar  # unity module
    │   │   ├── haishinkit.aar       # core library
    │   │   └── rtmp.aar             # rtmp module
    │   └── libHaishinKitUnity.dylib # macOS
    │
    └── HaishinKit/
        ├── Scripts/
        │   ├── HaishinKitManager.cs # 統一C# API
        │   └── AudioStreamCapture.cs
        ├── Samples/
        └── Editor/
            └── HaishinKitPostProcessor.cs
```

## ビルドフロー

### iOS/macOS

```bash
# 1. HaishinKit.unity リポジトリで
cd NativePlugin/HaishinKitUnity

# 2. パッケージ解決（HaishinKit.swift unity-supportを取得）
swift package resolve

# 3. macOS dylib ビルド
swift build -c release --arch arm64 --arch x86_64
cp .build/release/libHaishinKitUnity.dylib ../../UnityProject/Assets/Plugins/

# 4. iOS Framework ビルド
xcodebuild -scheme HaishinKitUnity \
  -configuration Release \
  -sdk iphoneos \
  -destination "generic/platform=iOS" \
  BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
  -derivedDataPath build-ios

cp -r build-ios/Build/Products/Release-iphoneos/PackageFrameworks/HaishinKitUnity.framework \
  ../../UnityProject/Assets/Plugins/iOS/HaishinKitUnity/
```

### Android

```bash
# 1. HaishinKit.kt リポジトリで（unity-support ブランチ）
cd /path/to/HaishinKit.kt
git checkout unity-support

# 2. AAR ビルド
./gradlew :unity:assembleRelease
./gradlew :haishinkit:assembleRelease
./gradlew :rtmp:assembleRelease

# 3. AAR をコピー
cp unity/build/outputs/aar/unity-release.aar \
   /path/to/HaishinKit.unity/UnityProject/Assets/Plugins/Android/HaishinKitUnity.aar

cp haishinkit/build/outputs/aar/haishinkit-release.aar \
   /path/to/HaishinKit.unity/UnityProject/Assets/Plugins/Android/haishinkit.aar

cp rtmp/build/outputs/aar/rtmp-release.aar \
   /path/to/HaishinKit.unity/UnityProject/Assets/Plugins/Android/rtmp.aar
```

## 依存関係

### iOS/macOS
```
HaishinKitManager.cs (C#)
    ↓ P/Invoke
CInterface.swift (@_cdecl)
    ↓
HaishinKitWrapper.swift
    ↓
HaishinKit (via SPM, unity-support branch)
    ↓
RTMPHaishinKit / HaishinKit core
```

### Android
```
HaishinKitManager.cs (C#)
    ↓ AndroidJavaObject
UnityBridge.kt (JNI)
    ↓
HaishinKitUnityWrapper.kt
    ↓
HaishinKit.kt (via Gradle dependency)
    ↓
haishinkit / rtmp modules
```

## API対応表

| C# メソッド | iOS (@_cdecl) | Android (JNI) |
|------------|---------------|---------------|
| Connect | HaishinKit_Connect | UnityBridge.connect |
| Disconnect | HaishinKit_Disconnect | UnityBridge.disconnect |
| StartPublishingWithTexture | HaishinKit_StartPublishingWithTexture | UnityBridge.startPublishingWithTexture |
| StopPublishing | HaishinKit_StopPublishing | UnityBridge.stopPublishing |
| SendVideoFrame | HaishinKit_SendVideoFrame | UnityBridge.sendVideoFrame |
| SendAudioFrame | HaishinKit_SendAudioFrame | UnityBridge.sendAudioFrame |
| SetVideoBitrate | HaishinKit_SetVideoBitrate | UnityBridge.setVideoBitrate |
| SetAudioBitrate | HaishinKit_SetAudioBitrate | UnityBridge.setAudioBitrate |
| SetFrameRate | HaishinKit_SetFrameRate | UnityBridge.setFrameRate |

## メンテナンス手順

### upstream更新への追従

#### HaishinKit.swift
```bash
cd /path/to/HaishinKit.swift
git fetch upstream
git checkout unity-support
git rebase upstream/main
# コンフリクト解決後
git push --force-with-lease origin unity-support
```

#### HaishinKit.kt
```bash
cd /path/to/HaishinKit.kt
git fetch upstream
git checkout unity-support
git rebase upstream/main
# コンフリクト解決後
git push --force-with-lease origin unity-support
```

### 新機能追加時の作業場所

| 機能 | 作業リポジトリ |
|------|---------------|
| RTMPストリーム処理の修正 | HaishinKit.swift (unity-support) |
| iOS C Interface追加 | HaishinKit.unity (NativePlugin/) |
| Android JNI追加 | HaishinKit.kt (unity/モジュール) |
| Unity C# API追加 | HaishinKit.unity (Scripts/) |
| ビルド設定 | HaishinKit.unity |

## バージョン管理

各リポジトリのバージョンを同期させるため、以下を推奨:

1. **タグ命名規則**: `unity-v{major}.{minor}.{patch}`
2. **変更時は全リポジトリで同時リリース**
3. **Package.swiftとbuild.gradle.ktsで特定ブランチ/タグを指定**

## トラブルシューティング

### Swift Package解決エラー
```bash
# キャッシュクリア
rm -rf .build Package.resolved
swift package resolve
```

### Android AAR依存解決エラー
```bash
# Gradleキャッシュクリア
./gradlew clean
./gradlew :unity:dependencies
```

### タイムスタンプずれ
- HaishinKit.swift unity-supportブランチが最新か確認
- `compressedVideoStartTime`による経過時間ベース計算が有効か確認
