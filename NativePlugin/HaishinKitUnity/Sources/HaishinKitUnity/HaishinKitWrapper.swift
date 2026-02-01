import Foundation
import AVFoundation
import HaishinKit
import VideoToolbox

// MARK: - HaishinKitWrapper Class

/// Unity向けHaishinKitラッパークラス
/// RTMP/SRTストリーミング機能を提供
public class HaishinKitWrapper: StreamProvider {

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

    // モジュラーコンポーネント
    private var audioEngine: AudioEngine!
    private var textureRenderer: TextureRenderer!

    // MARK: - StreamProvider Protocol

    public var currentStream: RTMPStream? {
        return rtmpStream
    }

    // MARK: - Initialization

    public init() {
        rtmpConnection = RTMPConnection()
        mixer = MediaMixer()

        // コンポーネント初期化（self参照のため後で設定）
        audioEngine = AudioEngine(streamProvider: self)
        textureRenderer = TextureRenderer(streamProvider: self)

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
        textureRenderer.release()
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
        audioEngine.reset()
        textureRenderer.initialize(width: Int(width), height: Int(height))

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

    /// UnityのMetalテクスチャからビデオフレームを送信
    public func sendVideoFrame(texturePtr: UnsafeRawPointer) {
        guard isTextureMode else { return }

        if textureRenderer.renderTexture(texturePtr: texturePtr) {
            audioEngine.sendSilentAudio()
        }
    }

    // MARK: - External Audio

    /// 外部オーディオの使用を設定
    public func setUseExternalAudio(_ enabled: Bool) {
        audioEngine.setUseExternalAudio(enabled)
    }

    /// Unityからオーディオフレームを受信して送信
    public func sendAudioFrame(samples: UnsafePointer<Float>, sampleCount: Int32, channels: Int32, sampleRate: Int32) {
        guard isTextureMode else { return }
        audioEngine.sendAudioFrame(samples: samples, sampleCount: sampleCount, channels: channels, sampleRate: sampleRate)
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
