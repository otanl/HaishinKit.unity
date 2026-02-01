# HaishinKit.unity Android対応計画

## 概要

HaishinKit.kt を使用して Android プラットフォームへの対応を実装する。

## 現状分析

### iOS/macOS 実装の構造

```
Unity (C#)
    │
    │ P/Invoke (@_cdecl)
    ▼
Swift Native Plugin (HaishinKitUnity.swift)
    │
    │ AVAudioPCMBuffer / CMSampleBuffer
    ▼
HaishinKit.swift (fork: unity-support branch)
    │
    │ RTMP
    ▼
Streaming Server
```

### HaishinKit.kt の分析結果

| 項目 | 詳細 |
|------|------|
| ライセンス | BSD 3-Clause (互換性あり) |
| 最小SDK | Android 5.0+ (API 21+) |
| 言語 | Kotlin (98%) |
| 動画入力 | Surface-based (`VideoCodec.createInputSurface()`) |
| 音声入力 | `AudioCodec.append(ByteBuffer)` でPCMデータを直接注入可能 ✅ |
| 外部テクスチャ | 直接サポートなし ⚠️ |

### 課題と解決策

#### 課題1: 外部テクスチャ入力
HaishinKit.kt はカメラ入力を前提としており、Unity の RenderTexture を直接入力する API がない。

**解決策の選択肢:**

| 方式 | 概要 | パフォーマンス | 複雑さ |
|------|------|---------------|--------|
| A. Bitmap方式 | RenderTexture→ReadPixels→Bitmap→ImageScreenObject | 低 (CPU経由) | 低 |
| B. Surface方式 | Unity NDK→Surface rendering→VideoCodec | 高 | 高 |
| C. Fork方式 | HaishinKit.kt を fork して外部テクスチャAPI追加 | 高 | 中 |

**推奨: 方式C (Fork方式)** - iOS と同様のアプローチで一貫性があり、パフォーマンスも良い。

#### 課題2: Unity-Android ブリッジ
- iOS: `@_cdecl` による C Interface + P/Invoke
- Android: JNI または `AndroidJavaObject`

**推奨: AndroidJavaObject を使用**
- C++/NDK 不要でシンプル
- Unity の標準機能で十分

---

## 実装計画

### Phase 1: 基盤構築 (Fork & AAR作成)

#### 1.1 HaishinKit.kt のフォーク
```bash
# フォーク先: github.com/otanl/HaishinKit.kt
# ブランチ: unity-support
```

追加するAPI:
```kotlin
// VideoCodec に追加
fun appendVideoFrame(bitmap: Bitmap, timestampNs: Long)

// または Surface取得API
fun getInputSurface(): Surface?
```

#### 1.2 Android Library (AAR) の作成

```
NativePlugin/
├── HaishinKitUnity/          # iOS/macOS (既存)
└── HaishinKitUnityAndroid/   # Android (新規)
    ├── build.gradle.kts
    ├── src/main/
    │   ├── AndroidManifest.xml
    │   └── java/com/haishinkit/unity/
    │       ├── HaishinKitUnityWrapper.kt
    │       └── UnityBridge.kt
    └── libs/
        └── (HaishinKit.kt dependencies)
```

#### 1.3 Unity側の修正

```csharp
// HaishinKitManager.cs に追加
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _androidInstance;

    private void InitializeAndroid()
    {
        _androidInstance = new AndroidJavaObject(
            "com.haishinkit.unity.HaishinKitUnityWrapper",
            GetCurrentActivity()
        );
    }
#endif
```

### Phase 2: 動画ストリーミング

#### 2.1 テクスチャ転送方式の実装

**オプション A: Bitmap 経由 (シンプル・低パフォーマンス)**

```csharp
// Unity C#
void SendVideoFrame(RenderTexture rt)
{
    RenderTexture.active = rt;
    _texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
    RenderTexture.active = null;

    byte[] pixels = _texture2D.GetRawTextureData();
    _androidInstance.Call("sendVideoFrame", pixels, rt.width, rt.height);
}
```

```kotlin
// Kotlin
fun sendVideoFrame(pixels: ByteArray, width: Int, height: Int) {
    val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
    bitmap.copyPixelsFromBuffer(ByteBuffer.wrap(pixels))
    imageScreenObject.bitmap = bitmap
}
```

**オプション B: NDK + Surface (高パフォーマンス)**

```cpp
// C++ NDK
extern "C" JNIEXPORT void JNICALL
Java_com_haishinkit_unity_UnityBridge_sendTextureToSurface(
    JNIEnv* env, jobject thiz, jint textureId, jint width, jint height)
{
    // OpenGL ES で Surface に描画
}
```

#### 2.2 タイムスタンプ管理
iOS と同様の連続タイムスタンプ計算を実装:

```kotlin
private var frameCount: Long = 0
private var startTimeNs: Long = 0

fun sendVideoFrame(bitmap: Bitmap) {
    if (frameCount == 0L) {
        startTimeNs = System.nanoTime()
    }

    val pts = System.nanoTime() - startTimeNs
    videoCodec.appendVideoFrame(bitmap, pts)
    frameCount++
}
```

### Phase 3: 音声ストリーミング

AudioCodec の `append(ByteBuffer)` が既に存在するため、iOS と同様のアプローチ:

```csharp
// Unity C#
public void SendAudioFrame(float[] samples, int channels, int sampleRate)
{
    byte[] bytes = new byte[samples.Length * 4];
    Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
    _androidInstance.Call("sendAudioFrame", bytes, samples.Length, channels, sampleRate);
}
```

```kotlin
// Kotlin
fun sendAudioFrame(samples: ByteArray, sampleCount: Int, channels: Int, sampleRate: Int) {
    val buffer = ByteBuffer.wrap(samples).order(ByteOrder.LITTLE_ENDIAN)
    audioCodec.append(buffer)
}
```

### Phase 4: 統合とテスト

#### 4.1 Unity C# 統合
`HaishinKitManager.cs` をプラットフォーム分岐で拡張:

```csharp
public void Connect(string url, string streamName)
{
#if UNITY_IOS && !UNITY_EDITOR
    HaishinKit_Connect(_nativeInstance, url, streamName);
#elif UNITY_ANDROID && !UNITY_EDITOR
    _androidInstance.Call("connect", url, streamName);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    HaishinKit_Connect(_nativeInstance, url, streamName);
#endif
}
```

#### 4.2 テスト項目
- [ ] RTMP接続/切断
- [ ] テクスチャ配信 (30fps, 1280x720)
- [ ] ゲーム音声配信
- [ ] ビットレート変更
- [ ] 長時間配信安定性 (1時間+)

---

## ディレクトリ構造 (最終形)

```
HaishinKit.unity/
├── NativePlugin/
│   ├── HaishinKitUnity/           # iOS/macOS
│   │   ├── Package.swift
│   │   └── Sources/
│   └── HaishinKitUnityAndroid/    # Android (新規)
│       ├── build.gradle.kts
│       ├── src/main/
│       │   ├── AndroidManifest.xml
│       │   └── kotlin/com/haishinkit/unity/
│       │       ├── HaishinKitUnityWrapper.kt
│       │       └── TextureRenderer.kt
│       └── libs/
├── UnityProject/Assets/
│   ├── HaishinKit/
│   │   ├── Scripts/
│   │   │   ├── HaishinKitManager.cs        # 全プラットフォーム対応
│   │   │   ├── AudioStreamCapture.cs
│   │   │   └── Platform/
│   │   │       ├── HaishinKitAndroid.cs    # Android固有 (新規)
│   │   │       └── HaishinKitiOS.cs        # iOS固有 (新規)
│   │   └── Samples/
│   └── Plugins/
│       ├── iOS/
│       │   └── HaishinKitUnity.framework
│       ├── macOS/
│       │   └── libHaishinKitUnity.dylib
│       └── Android/                         # 新規
│           └── haishinkit-unity.aar
└── README.md
```

---

## スケジュール概算

| Phase | 内容 | 見積もり |
|-------|------|----------|
| Phase 1 | 基盤構築 (Fork, AAR) | - |
| Phase 2 | 動画ストリーミング | - |
| Phase 3 | 音声ストリーミング | - |
| Phase 4 | 統合・テスト | - |

---

## リスクと対策

| リスク | 影響 | 対策 |
|--------|------|------|
| HaishinKit.kt の修正が大規模になる | 工数増加 | まず Bitmap 方式で動作確認、最適化は後回し |
| OpenGL ES テクスチャ共有の問題 | パフォーマンス低下 | Vulkan 対応も検討 |
| Android 端末の断片化 | 互換性問題 | 最小SDK 26+ (Android 8.0+) に引き上げ検討 |

---

## 次のステップ

1. **HaishinKit.kt のフォーク作成**
   - `github.com/otanl/HaishinKit.kt` に fork
   - `unity-support` ブランチ作成

2. **最小限の PoC 実装**
   - Bitmap 方式でテクスチャ配信を確認
   - 音声配信の動作確認

3. **パフォーマンス最適化**
   - NDK/Surface 方式の検討
   - プロファイリング

---

## 結論

Android 対応は**実現可能**です。

**推奨アプローチ:**
1. HaishinKit.kt を fork して外部入力 API を追加
2. まず Bitmap 方式で動作を確認
3. パフォーマンスが問題なら NDK/Surface 方式に移行

iOS 実装と同じパターンを踏襲することで、Unity C# 側の API は統一でき、ユーザーにとって使いやすいプラグインになります。
