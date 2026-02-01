// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "HaishinKitUnity",
    platforms: [
        .iOS(.v15),
        .macOS(.v12)
    ],
    products: [
        .library(
            name: "HaishinKitUnity",
            type: .dynamic,
            targets: ["HaishinKitUnity"]
        ),
    ],
    dependencies: [
        // Unity対応フォーク (unity-support ブランチ)
        .package(url: "https://github.com/otanl/HaishinKit.swift.git", branch: "unity-support"),
        // Logboard (HaishinKitの依存関係、xcodebuild用に明示)
        .package(url: "https://github.com/shogo4405/Logboard.git", from: "2.5.0")
    ],
    targets: [
        .target(
            name: "HaishinKitUnity",
            dependencies: [
                .product(name: "HaishinKit", package: "HaishinKit.swift"),
                .product(name: "RTMPHaishinKit", package: "HaishinKit.swift")
            ]
        ),
        .testTarget(
            name: "HaishinKitUnityTests",
            dependencies: ["HaishinKitUnity"]
        ),
    ]
)
