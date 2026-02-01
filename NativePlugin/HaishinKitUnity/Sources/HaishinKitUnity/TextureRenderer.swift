import Foundation
import AVFoundation
import CoreVideo
import Metal
import HaishinKit

/// テクスチャレンダラー - Metalテクスチャからビデオフレームへの変換を担当
public class TextureRenderer {

    // MARK: - Private Properties

    private weak var streamProvider: StreamProvider?
    private var pixelBufferPool: CVPixelBufferPool?
    private var videoWidth: Int = 0
    private var videoHeight: Int = 0
    private var frameCount: Int64 = 0
    private var startTime: CMTime?

    // MARK: - Public Properties

    public private(set) var isInitialized: Bool = false

    // MARK: - Initialization

    public init(streamProvider: StreamProvider) {
        self.streamProvider = streamProvider
    }

    // MARK: - Public Methods

    /// レンダラーを初期化
    public func initialize(width: Int, height: Int) {
        videoWidth = width
        videoHeight = height
        frameCount = 0
        startTime = nil

        createPixelBufferPool(width: width, height: height)
        isInitialized = true
    }

    /// リソースを解放
    public func release() {
        isInitialized = false
        pixelBufferPool = nil
        frameCount = 0
        startTime = nil
    }

    /// UnityのMetalテクスチャからビデオフレームを送信
    /// - Returns: 送信成功時はtrue
    public func renderTexture(texturePtr: UnsafeRawPointer) -> Bool {
        guard isInitialized, let stream = streamProvider?.currentStream, let pool = pixelBufferPool else {
            return false
        }

        let textureObject = Unmanaged<AnyObject>.fromOpaque(texturePtr).takeUnretainedValue()
        guard let texture = textureObject as? MTLTexture else {
            return false
        }

        var pixelBuffer: CVPixelBuffer?
        let status = CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, pool, &pixelBuffer)
        guard status == kCVReturnSuccess, let buffer = pixelBuffer else {
            return false
        }

        // MetalテクスチャからPixelBufferにコピー（上下反転）
        CVPixelBufferLockBaseAddress(buffer, [])
        defer { CVPixelBufferUnlockBaseAddress(buffer, []) }

        guard let baseAddress = CVPixelBufferGetBaseAddress(buffer) else {
            return false
        }

        let bytesPerRow = CVPixelBufferGetBytesPerRow(buffer)
        let height = texture.height

        for y in 0..<height {
            let srcRow = height - 1 - y
            let region = MTLRegionMake2D(0, srcRow, texture.width, 1)
            let destPtr = baseAddress.advanced(by: y * bytesPerRow)
            texture.getBytes(destPtr, bytesPerRow: bytesPerRow, from: region, mipmapLevel: 0)
        }

        // タイムスタンプ計算
        let now = CMClockGetTime(CMClockGetHostTimeClock())
        if startTime == nil {
            startTime = CMTimeSubtract(now, CMTime(value: 1, timescale: 30))
        }
        let pts = CMTimeSubtract(now, startTime!)
        let duration = CMTime(value: 1, timescale: 30)

        frameCount += 1

        guard let sampleBuffer = createUncompressedSampleBuffer(from: buffer, pts: pts, duration: duration) else {
            return false
        }

        Task {
            await stream.append(sampleBuffer)
        }

        return true
    }

    // MARK: - Private Methods

    private func createPixelBufferPool(width: Int, height: Int) {
        let poolAttributes: [String: Any] = [
            kCVPixelBufferPoolMinimumBufferCountKey as String: 3
        ]

        let pixelBufferAttributes: [String: Any] = [
            kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA,
            kCVPixelBufferWidthKey as String: width,
            kCVPixelBufferHeightKey as String: height,
            kCVPixelBufferIOSurfacePropertiesKey as String: [:],
            kCVPixelBufferMetalCompatibilityKey as String: true
        ]

        var pool: CVPixelBufferPool?
        let status = CVPixelBufferPoolCreate(
            kCFAllocatorDefault,
            poolAttributes as CFDictionary,
            pixelBufferAttributes as CFDictionary,
            &pool
        )

        if status == kCVReturnSuccess {
            pixelBufferPool = pool
        }
    }

    private func createUncompressedSampleBuffer(from pixelBuffer: CVPixelBuffer, pts: CMTime, duration: CMTime) -> CMSampleBuffer? {
        var formatDescription: CMFormatDescription?
        let formatStatus = CMVideoFormatDescriptionCreateForImageBuffer(
            allocator: kCFAllocatorDefault,
            imageBuffer: pixelBuffer,
            formatDescriptionOut: &formatDescription
        )
        guard formatStatus == noErr, let format = formatDescription else { return nil }

        var timingInfo = CMSampleTimingInfo(
            duration: duration,
            presentationTimeStamp: pts,
            decodeTimeStamp: .invalid
        )

        var sampleBuffer: CMSampleBuffer?
        let createStatus = CMSampleBufferCreateReadyWithImageBuffer(
            allocator: kCFAllocatorDefault,
            imageBuffer: pixelBuffer,
            formatDescription: format,
            sampleTiming: &timingInfo,
            sampleBufferOut: &sampleBuffer
        )

        return createStatus == noErr ? sampleBuffer : nil
    }
}
