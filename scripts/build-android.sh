#!/bin/bash
set -e

# HaishinKit.unity Android ビルドスクリプト
# 使用法: ./scripts/build-android.sh [path/to/HaishinKit.kt]
#
# 前提条件:
#   - HaishinKit.kt リポジトリがクローン済み (unity-support ブランチ)
#   - Android SDK がインストール済み
#   - ANDROID_HOME 環境変数が設定済み

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
UNITY_PLUGINS="$PROJECT_ROOT/UnityProject/Assets/Plugins/Android"

# デフォルトのHaishinKit.ktパス
HAISHINKIT_KT_PATH="${1:-$HOME/Desktop/work/xcode/HaishinKit.kt}"

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

# 環境チェック
check_environment() {
    if [ ! -d "$HAISHINKIT_KT_PATH" ]; then
        log_error "HaishinKit.kt not found at: $HAISHINKIT_KT_PATH"
        echo ""
        echo "Usage: $0 [path/to/HaishinKit.kt]"
        echo ""
        echo "HaishinKit.kt repository must be cloned with unity-support branch:"
        echo "  git clone -b unity-support https://github.com/otanl/HaishinKit.kt.git"
        exit 1
    fi

    if [ -z "$ANDROID_HOME" ]; then
        log_warn "ANDROID_HOME is not set. Trying common paths..."
        if [ -d "$HOME/Library/Android/sdk" ]; then
            export ANDROID_HOME="$HOME/Library/Android/sdk"
        elif [ -d "/usr/local/share/android-sdk" ]; then
            export ANDROID_HOME="/usr/local/share/android-sdk"
        else
            log_error "Could not find Android SDK. Please set ANDROID_HOME."
            exit 1
        fi
    fi

    log_info "Using HaishinKit.kt at: $HAISHINKIT_KT_PATH"
    log_info "Using Android SDK at: $ANDROID_HOME"
}

# ブランチチェック
check_branch() {
    cd "$HAISHINKIT_KT_PATH"
    CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
    if [ "$CURRENT_BRANCH" != "unity-support" ]; then
        log_warn "Current branch is '$CURRENT_BRANCH', not 'unity-support'"
        read -p "Switch to unity-support branch? [y/N] " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            git checkout unity-support
        else
            log_warn "Continuing with branch: $CURRENT_BRANCH"
        fi
    fi
}

# AAR ビルド
build_aars() {
    cd "$HAISHINKIT_KT_PATH"

    log_info "Building AAR files..."

    # Gradleラッパーに実行権限を付与
    chmod +x gradlew

    # クリーンビルド
    ./gradlew clean

    # 各モジュールをビルド
    log_info "Building unity module..."
    ./gradlew :unity:assembleRelease

    log_info "Building haishinkit module..."
    ./gradlew :haishinkit:assembleRelease

    log_info "Building rtmp module..."
    ./gradlew :rtmp:assembleRelease
}

# AAR コピー
copy_aars() {
    log_info "Copying AAR files to Unity project..."

    mkdir -p "$UNITY_PLUGINS"

    # unity モジュール
    UNITY_AAR="$HAISHINKIT_KT_PATH/unity/build/outputs/aar/unity-release.aar"
    if [ -f "$UNITY_AAR" ]; then
        cp "$UNITY_AAR" "$UNITY_PLUGINS/HaishinKitUnity.aar"
        log_info "Copied HaishinKitUnity.aar"
    else
        log_error "unity-release.aar not found"
        exit 1
    fi

    # haishinkit モジュール
    HAISHINKIT_AAR="$HAISHINKIT_KT_PATH/haishinkit/build/outputs/aar/haishinkit-release.aar"
    if [ -f "$HAISHINKIT_AAR" ]; then
        cp "$HAISHINKIT_AAR" "$UNITY_PLUGINS/haishinkit.aar"
        log_info "Copied haishinkit.aar"
    else
        log_error "haishinkit-release.aar not found"
        exit 1
    fi

    # rtmp モジュール
    RTMP_AAR="$HAISHINKIT_KT_PATH/rtmp/build/outputs/aar/rtmp-release.aar"
    if [ -f "$RTMP_AAR" ]; then
        cp "$RTMP_AAR" "$UNITY_PLUGINS/rtmp.aar"
        log_info "Copied rtmp.aar"
    else
        log_error "rtmp-release.aar not found"
        exit 1
    fi
}

# メイン処理
main() {
    log_info "Starting Android build..."

    check_environment
    check_branch
    build_aars
    copy_aars

    log_info "Android build completed successfully!"
    echo ""
    echo "AAR files copied to: $UNITY_PLUGINS"
    ls -la "$UNITY_PLUGINS"/*.aar
}

main
