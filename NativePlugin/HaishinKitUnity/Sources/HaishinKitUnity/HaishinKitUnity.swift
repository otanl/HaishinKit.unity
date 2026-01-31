import Foundation
import AVFoundation
import HaishinKit
import CoreVideo
import Metal
import VideoToolbox

// MARK: - HaishinKitWrapper Class

/// Unity向けHaishinKitラッパークラス
/// RTMP/SRTストリーミング機能を提供
public class HaishinKitWrapper {

    // MARK: - Private Properties

    private var rtmpConnection: RTMPConnection
    private var rtmpStream: RTMPStream?
    private var mixer: MediaMixer

    private var statusCallback: StatusCallback?
    private var currentURL: String = ""
    private var currentStreamName: String = ""
    private var currentPosition: AVCaptureDevice.Position = .back

    // テクスチャ配信用
    private var isTextureMode: Bool = false
    private var pixelBufferPool: CVPixelBufferPool?
    private var videoWidth: Int = 0
    private var videoHeight: Int = 0
    private var frameCount: Int64 = 0
    private var startTime: CMTime?

    // 無音オーディオ生成用
    private var audioFormat: AVAudioFormat?
    private var lastAudioSendTime: CFAbsoluteTime = 0
    private var silentAudioCallCount: Int64 = 0

    // 外部オーディオ用
    private var useExternalAudio: Bool = false
    private var externalAudioFormat: AVAudioFormat?
    private var externalAudioSampleCount: Int64 = 0
    private var lastExternalAudioTime: CFAbsoluteTime = 0
    private var externalAudioStartHostTime: UInt64 = 0
    private var hostTimeInfo: mach_timebase_info_data_t = mach_timebase_info_data_t()

    // MARK: - Initialization

    public init() {
        rtmpConnection = RTMPConnection()
        mixer = MediaMixer()

        #if os(iOS)
        setupAudioSession()
        #endif
    }

    #if os(iOS)
    private func setupAudioSession() {
        let session = AVAudioSession.sharedInstance()
        do {
            try session.setCategory(.playAndRecord, mode: .default, options: [.defaultToSpeaker, .allowBluetooth])
            try session.setActive(true)
        } catch {
            NSLog("[HaishinKit] Audio session setup failed: \(error)")
        }
    }
    #endif

    deinit {
        statusCallback = nil
    }

    /// 明示的なクリーンアップ（C# の OnDestroy から呼ばれる）
    public func cleanup() {
        statusCallback = nil
        isTextureMode = false
        pixelBufferPool = nil
        Task {
            try? await mixer.attachVideo(nil, track: 0)
            try? await mixer.attachAudio(nil)
            try? await rtmpConnection.close()
            rtmpStream = nil
        }
    }

    // MARK: - Connection

    public func connect(url: String, streamName: String) {
        currentURL = url
        currentStreamName = streamName

        Task {
            do {
                rtmpStream = RTMPStream(connection: rtmpConnection)

                guard let stream = rtmpStream else {
                    notifyStatus("error:stream creation failed")
                    return
                }

                await mixer.addOutput(stream)
                _ = try await rtmpConnection.connect(url)
                notifyStatus("connected")
            } catch {
                notifyStatus("error:\(error.localizedDescription)")
            }
        }
    }

    public func disconnect() {
        Task {
            try? await rtmpConnection.close()
            rtmpStream = nil
            notifyStatus("disconnected")
        }
    }

    // MARK: - Publishing (カメラ/マイクモード)

    public func startPublishing() {
        guard let stream = rtmpStream else {
            notifyStatus("error:stream not initialized")
            return
        }

        Task {
            do {
                if let device = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: currentPosition) {
                    try await mixer.attachVideo(device, track: 0)
                }

                if let device = AVCaptureDevice.default(for: .audio) {
                    try await mixer.attachAudio(device)
                }

                _ = try await stream.publish(currentStreamName)
                notifyStatus("publishing")
            } catch {
                notifyStatus("error:\(error.localizedDescription)")
            }
        }
    }

    public func stopPublishing() {
        guard let stream = rtmpStream else { return }

        Task {
            do {
                _ = try await stream.publish(nil)
                notifyStatus("stopped")
            } catch {
                notifyStatus("stopped")
            }
        }
    }

    // MARK: - Settings

    public func setVideoBitrate(_ bitrate: Int32) {
        guard let stream = rtmpStream else { return }

        Task {
            var settings = await stream.videoSettings
            settings.bitRate = Int(bitrate) * 1000
            await stream.setVideoSettings(settings)
        }
    }

    public func setAudioBitrate(_ bitrate: Int32) {
        guard let stream = rtmpStream else { return }

        Task {
            var settings = await stream.audioSettings
            settings.bitRate = Int(bitrate) * 1000
            await stream.setAudioSettings(settings)
        }
    }

    public func setFrameRate(_ fps: Int32) {
        Task {
            await mixer.setFrameRate(Float64(fps))
        }
    }

    // MARK: - Camera Control

    public func switchCamera() {
        Task {
            let newPosition: AVCaptureDevice.Position = currentPosition == .back ? .front : .back

            if let device = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: newPosition) {
                do {
                    try await mixer.attachVideo(device, track: 0) { videoUnit in
                        videoUnit.isVideoMirrored = newPosition == .front
                    }
                    currentPosition = newPosition
                } catch {
                    notifyStatus("error:camera switch failed")
                }
            }
        }
    }

    public func setZoom(_ level: Float) {
        #if os(iOS) || os(tvOS)
        Task {
            do {
                try await mixer.configuration(video: 0) { unit in
                    guard let device = unit.device else { return }
                    try device.lockForConfiguration()
                    device.ramp(toVideoZoomFactor: CGFloat(level), withRate: 5.0)
                    device.unlockForConfiguration()
                }
            } catch {
                notifyStatus("error:zoom failed")
            }
        }
        #endif
    }

    public func setTorch(_ enabled: Bool) {
        Task {
            await mixer.setTorchEnabled(enabled)
        }
    }

    // MARK: - Texture Streaming

    /// テクスチャモードで配信開始
    public func startPublishingWithTexture(width: Int32, height: Int32) {
        guard let stream = rtmpStream else {
            notifyStatus("error:stream not initialized")
            return
        }

        isTextureMode = true
        videoWidth = Int(width)
        videoHeight = Int(height)
        frameCount = 0
        startTime = nil
        lastAudioSendTime = 0
        silentAudioCallCount = 0

        createPixelBufferPool(width: Int(width), height: Int(height))

        Task {
            do {
                await stream.setVideoSettings(VideoCodecSettings(
                    videoSize: CGSize(width: Int(width), height: Int(height)),
                    bitRate: 2_000_000,
                    profileLevel: kVTProfileLevel_H264_Baseline_3_1 as String
                ))

                await stream.setAudioSettings(AudioCodecSettings(
                    bitRate: 128_000
                ))

                _ = try await stream.publish(currentStreamName)
                try await Task.sleep(nanoseconds: 300_000_000)
                notifyStatus("publishing")
            } catch {
                notifyStatus("error:\(error.localizedDescription)")
            }
        }
    }

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

    /// UnityのMetalテクスチャからビデオフレームを送信
    public func sendVideoFrame(texturePtr: UnsafeRawPointer) {
        guard isTextureMode, let stream = rtmpStream, let pool = pixelBufferPool else { return }

        let textureObject = Unmanaged<AnyObject>.fromOpaque(texturePtr).takeUnretainedValue()
        guard let texture = textureObject as? MTLTexture else { return }

        var pixelBuffer: CVPixelBuffer?
        let status = CVPixelBufferPoolCreatePixelBuffer(kCFAllocatorDefault, pool, &pixelBuffer)
        guard status == kCVReturnSuccess, let buffer = pixelBuffer else { return }

        // MetalテクスチャからPixelBufferにコピー（上下反転）
        CVPixelBufferLockBaseAddress(buffer, [])
        defer { CVPixelBufferUnlockBaseAddress(buffer, []) }

        guard let baseAddress = CVPixelBufferGetBaseAddress(buffer) else { return }

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

        if let sampleBuffer = createUncompressedSampleBuffer(from: buffer, pts: pts, duration: duration) {
            Task {
                await stream.append(sampleBuffer)
            }
        }

        sendSilentAudio()
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

    /// 無音オーディオを送信
    private func sendSilentAudio() {
        guard isTextureMode, let stream = rtmpStream else { return }

        // 外部オーディオ使用時は、受信がない場合のみ無音送信
        if useExternalAudio {
            let now = CFAbsoluteTimeGetCurrent()
            if now - lastExternalAudioTime < 0.5 {
                return
            }
        }

        // 約23ms間隔で送信
        let now = CFAbsoluteTimeGetCurrent()
        guard now - lastAudioSendTime >= 0.023 else { return }
        lastAudioSendTime = now

        if audioFormat == nil {
            audioFormat = AVAudioFormat(
                commonFormat: .pcmFormatFloat32,
                sampleRate: 44100,
                channels: 2,
                interleaved: false
            )
        }
        guard let format = audioFormat else { return }

        guard let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: 1024) else { return }
        buffer.frameLength = 1024

        if let channelData = buffer.floatChannelData {
            for channel in 0..<Int(format.channelCount) {
                memset(channelData[channel], 0, Int(buffer.frameLength) * MemoryLayout<Float>.size)
            }
        }

        let hostTime = mach_absolute_time()
        let when = AVAudioTime(hostTime: hostTime, sampleTime: silentAudioCallCount * 1024, atRate: 44100)
        silentAudioCallCount += 1

        Task {
            await stream.append(buffer, when: when)
        }
    }

    // MARK: - External Audio

    /// 外部オーディオの使用を設定
    public func setUseExternalAudio(_ enabled: Bool) {
        useExternalAudio = enabled
        if enabled {
            externalAudioSampleCount = 0
            externalAudioStartHostTime = 0
            externalAudioFormat = nil
        }
    }

    /// Unityからオーディオフレームを受信して送信
    public func sendAudioFrame(samples: UnsafePointer<Float>, sampleCount: Int32, channels: Int32, sampleRate: Int32) {
        guard isTextureMode, useExternalAudio, let stream = rtmpStream else { return }

        lastExternalAudioTime = CFAbsoluteTimeGetCurrent()

        let channelCount = Int(channels)
        let frameCount = Int(sampleCount)
        let rate = Double(sampleRate)

        // フォーマットを作成
        if externalAudioFormat == nil || externalAudioFormat?.sampleRate != rate || externalAudioFormat?.channelCount != UInt32(channelCount) {
            externalAudioFormat = AVAudioFormat(
                commonFormat: .pcmFormatFloat32,
                sampleRate: rate,
                channels: AVAudioChannelCount(channelCount),
                interleaved: false
            )
        }
        guard let format = externalAudioFormat else { return }

        guard let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: AVAudioFrameCount(frameCount)) else { return }
        buffer.frameLength = AVAudioFrameCount(frameCount)

        // インターリーブ → デインターリーブ変換
        if let channelData = buffer.floatChannelData {
            if channelCount == 2 {
                for i in 0..<frameCount {
                    channelData[0][i] = samples[i * 2]
                    channelData[1][i] = samples[i * 2 + 1]
                }
            } else {
                memcpy(channelData[0], samples, frameCount * MemoryLayout<Float>.size)
            }
        }

        // 連続タイムスタンプを計算
        if externalAudioSampleCount == 0 {
            externalAudioStartHostTime = mach_absolute_time()
            mach_timebase_info(&hostTimeInfo)
        }

        let elapsedSamples = externalAudioSampleCount
        let elapsedNanoseconds = UInt64(Double(elapsedSamples) / rate * 1_000_000_000)
        let elapsedHostTime = elapsedNanoseconds * UInt64(hostTimeInfo.denom) / UInt64(hostTimeInfo.numer)
        let calculatedHostTime = externalAudioStartHostTime + elapsedHostTime

        let when = AVAudioTime(hostTime: calculatedHostTime, sampleTime: externalAudioSampleCount, atRate: rate)
        externalAudioSampleCount += Int64(frameCount)

        Task {
            await stream.append(buffer, when: when)
        }
    }

    // MARK: - Callback

    public func setStatusCallback(_ callback: @escaping StatusCallback) {
        statusCallback = callback
    }

    private func notifyStatus(_ status: String) {
        guard let callback = statusCallback else { return }
        status.withCString { ptr in
            callback(ptr)
        }
    }
}

// MARK: - C Interface Types

public typealias StatusCallback = @convention(c) (UnsafePointer<CChar>) -> Void

// MARK: - C Interface Functions (Version)

@_cdecl("HaishinKit_GetVersion")
public func getVersion() -> UnsafeMutablePointer<CChar>? {
    let version = "HaishinKitUnity 1.0.0"
    return strdup(version)
}

@_cdecl("HaishinKit_FreeString")
public func freeString(ptr: UnsafeMutablePointer<CChar>?) {
    if let ptr = ptr {
        free(ptr)
    }
}

// MARK: - C Interface Functions (Instance Management)

@_cdecl("HaishinKit_CreateInstance")
public func createInstance() -> UnsafeMutableRawPointer {
    let instance = HaishinKitWrapper()
    return Unmanaged.passRetained(instance).toOpaque()
}

@_cdecl("HaishinKit_Cleanup")
public func cleanup(ptr: UnsafeMutableRawPointer) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.cleanup()
}

@_cdecl("HaishinKit_DestroyInstance")
public func destroyInstance(ptr: UnsafeMutableRawPointer) {
    _ = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeRetainedValue()
}

// MARK: - C Interface Functions (Connection)

@_cdecl("HaishinKit_Connect")
public func connect(ptr: UnsafeMutableRawPointer, url: UnsafePointer<CChar>, streamName: UnsafePointer<CChar>) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    let urlString = String(cString: url)
    let streamNameString = String(cString: streamName)
    instance.connect(url: urlString, streamName: streamNameString)
}

@_cdecl("HaishinKit_Disconnect")
public func disconnect(ptr: UnsafeMutableRawPointer) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.disconnect()
}

// MARK: - C Interface Functions (Publishing)

@_cdecl("HaishinKit_StartPublishing")
public func startPublishing(ptr: UnsafeMutableRawPointer) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.startPublishing()
}

@_cdecl("HaishinKit_StopPublishing")
public func stopPublishing(ptr: UnsafeMutableRawPointer) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.stopPublishing()
}

// MARK: - C Interface Functions (Settings)

@_cdecl("HaishinKit_SetVideoBitrate")
public func setVideoBitrate(ptr: UnsafeMutableRawPointer, bitrate: Int32) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.setVideoBitrate(bitrate)
}

@_cdecl("HaishinKit_SetAudioBitrate")
public func setAudioBitrate(ptr: UnsafeMutableRawPointer, bitrate: Int32) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.setAudioBitrate(bitrate)
}

@_cdecl("HaishinKit_SetFrameRate")
public func setFrameRate(ptr: UnsafeMutableRawPointer, fps: Int32) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.setFrameRate(fps)
}

// MARK: - C Interface Functions (Camera)

@_cdecl("HaishinKit_SwitchCamera")
public func switchCamera(ptr: UnsafeMutableRawPointer) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.switchCamera()
}

@_cdecl("HaishinKit_SetZoom")
public func setZoom(ptr: UnsafeMutableRawPointer, level: Float) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.setZoom(level)
}

@_cdecl("HaishinKit_SetTorch")
public func setTorch(ptr: UnsafeMutableRawPointer, enabled: Bool) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.setTorch(enabled)
}

// MARK: - C Interface Functions (Callback)

@_cdecl("HaishinKit_SetStatusCallback")
public func setStatusCallback(ptr: UnsafeMutableRawPointer, callback: @escaping StatusCallback) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.setStatusCallback(callback)
}

// MARK: - C Interface Functions (Texture Streaming)

@_cdecl("HaishinKit_StartPublishingWithTexture")
public func startPublishingWithTexture(ptr: UnsafeMutableRawPointer, width: Int32, height: Int32) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.startPublishingWithTexture(width: width, height: height)
}

@_cdecl("HaishinKit_SendVideoFrame")
public func sendVideoFrame(ptr: UnsafeMutableRawPointer, texturePtr: UnsafeRawPointer) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.sendVideoFrame(texturePtr: texturePtr)
}

// MARK: - C Interface Functions (External Audio)

@_cdecl("HaishinKit_SetUseExternalAudio")
public func setUseExternalAudio(ptr: UnsafeMutableRawPointer, enabled: Bool) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.setUseExternalAudio(enabled)
}

@_cdecl("HaishinKit_SendAudioFrame")
public func sendAudioFrame(ptr: UnsafeMutableRawPointer, samples: UnsafePointer<Float>, sampleCount: Int32, channels: Int32, sampleRate: Int32) {
    let instance = Unmanaged<HaishinKitWrapper>.fromOpaque(ptr).takeUnretainedValue()
    instance.sendAudioFrame(samples: samples, sampleCount: sampleCount, channels: channels, sampleRate: sampleRate)
}
