# HaishinKit.unity アーキテクチャ

## リポジトリ構成

```
┌─────────────────────────────────────────────────────────────────┐
│  shogo4405/HaishinKit.swift    shogo4405/HaishinKit.kt          │
│         (upstream)                    (upstream)                │
└──────────┬─────────────────────────────────┬────────────────────┘
           │ fork                            │ fork
           ▼                                 ▼
┌─────────────────────────┐    ┌─────────────────────────────────┐
│ otanl/HaishinKit.swift  │    │ otanl/HaishinKit.kt             │
│ (unity-support branch)  │    │ (unity-support branch)          │
│                         │    │                                 │
│ 変更内容:               │    │ 追加モジュール: unity/          │
│ - タイムスタンプ修正    │    │ - UnityBridge.kt                │
│ - 圧縮済み動画対応      │    │ - HaishinKitUnityWrapper.kt     │
│                         │    │ - BitmapRenderer.kt             │
│                         │    │ - NativeTextureRenderer.kt      │
└───────────┬─────────────┘    └──────────────┬──────────────────┘
            │ SPM依存                          │ AAR取得
            ▼                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                    otanl/HaishinKit.unity                       │
│                                                                 │
│  NativePlugin/HaishinKitUnity/     ← iOS/macOS Swift Package    │
│  UnityProject/Assets/                                           │
│    ├── Plugins/iOS/                ← Framework (自動ビルド)     │
│    ├── Plugins/Android/            ← AAR (HaishinKit.ktから)    │
│    └── HaishinKit/Scripts/         ← C# 統一API                 │
└─────────────────────────────────────────────────────────────────┘
```

## CI/CD 自動化

### HaishinKit.kt

```bash
git tag unity-v1.0.0 && git push origin unity-v1.0.0
```
→ GitHub Actions が AAR をビルドしてリリース作成

### HaishinKit.unity

```bash
git tag v1.0.0 && git push origin v1.0.0
```
→ GitHub Actions が:
  - iOS Framework ビルド (macOS runner)
  - macOS dylib ビルド
  - HaishinKit.kt から AAR 取得
  - 統合リリース作成

## ブリッジ実装

### iOS/macOS (Swift → C#)

```
C# (P/Invoke)  →  @_cdecl関数  →  HaishinKitWrapper  →  HaishinKit.swift
```

### Android (Kotlin → C#)

```
C# (AndroidJavaObject)  →  UnityBridge.kt  →  HaishinKitUnityWrapper  →  HaishinKit.kt
```

## リリースURL

| リポジトリ | リリース |
|-----------|---------|
| HaishinKit.kt | https://github.com/otanl/HaishinKit.kt/releases |
| HaishinKit.unity | https://github.com/otanl/HaishinKit.unity/releases |

## upstream追従

```bash
# HaishinKit.swift
cd HaishinKit.swift
git fetch upstream && git rebase upstream/main
git push --force-with-lease origin unity-support

# HaishinKit.kt
cd HaishinKit.kt
git fetch upstream && git rebase upstream/main
git push --force-with-lease origin unity-support
```
