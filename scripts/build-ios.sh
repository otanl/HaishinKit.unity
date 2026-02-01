#!/bin/bash
set -e

# HaishinKit.unity iOS/macOS ビルドスクリプト
# 使用法: ./scripts/build-ios.sh [ios|macos|all]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
NATIVE_PLUGIN="$PROJECT_ROOT/NativePlugin/HaishinKitUnity"
UNITY_PLUGINS="$PROJECT_ROOT/UnityProject/Assets/Plugins"

# 色付き出力
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

build_macos() {
    log_info "Building macOS dylib..."
    cd "$NATIVE_PLUGIN"

    # パッケージ解決
    log_info "Resolving Swift packages..."
    swift package resolve

    # Universal Binary (arm64 + x86_64)
    log_info "Building universal binary..."
    swift build -c release --arch arm64 --arch x86_64

    # コピー
    cp ".build/release/libHaishinKitUnity.dylib" "$UNITY_PLUGINS/"
    log_info "Copied libHaishinKitUnity.dylib to $UNITY_PLUGINS/"
}

build_ios() {
    log_info "Building iOS Framework..."
    cd "$NATIVE_PLUGIN"

    # パッケージ解決
    log_info "Resolving Swift packages..."
    swift package resolve

    # ビルドディレクトリをクリア
    rm -rf build-ios

    # iOS Framework ビルド
    log_info "Building iOS Framework..."
    xcodebuild -scheme HaishinKitUnity \
        -configuration Release \
        -sdk iphoneos \
        -destination "generic/platform=iOS" \
        BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
        SKIP_INSTALL=NO \
        -derivedDataPath build-ios \
        2>&1 | tail -20

    # Framework をコピー
    FRAMEWORK_PATH="build-ios/Build/Products/Release-iphoneos/PackageFrameworks/HaishinKitUnity.framework"
    if [ -d "$FRAMEWORK_PATH" ]; then
        mkdir -p "$UNITY_PLUGINS/iOS/HaishinKitUnity"
        rm -rf "$UNITY_PLUGINS/iOS/HaishinKitUnity/HaishinKitUnity.framework"
        cp -r "$FRAMEWORK_PATH" "$UNITY_PLUGINS/iOS/HaishinKitUnity/"
        log_info "Copied HaishinKitUnity.framework to $UNITY_PLUGINS/iOS/HaishinKitUnity/"
    else
        log_error "Framework not found at $FRAMEWORK_PATH"
        exit 1
    fi
}

show_usage() {
    echo "Usage: $0 [ios|macos|all]"
    echo ""
    echo "Options:"
    echo "  ios    - Build iOS Framework only"
    echo "  macos  - Build macOS dylib only"
    echo "  all    - Build both iOS and macOS (default)"
}

# メイン処理
case "${1:-all}" in
    ios)
        build_ios
        ;;
    macos)
        build_macos
        ;;
    all)
        build_macos
        build_ios
        ;;
    -h|--help)
        show_usage
        exit 0
        ;;
    *)
        log_error "Unknown option: $1"
        show_usage
        exit 1
        ;;
esac

log_info "Build completed successfully!"
