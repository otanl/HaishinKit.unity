import Foundation
import AVFoundation
import HaishinKit

/// オーディオエンジン - 無音オーディオと外部オーディオの処理を担当
public class AudioEngine {

    // MARK: - Private Properties

    private weak var streamProvider: StreamProvider?

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

    public init(streamProvider: StreamProvider) {
        self.streamProvider = streamProvider
    }

    // MARK: - Public Methods

    /// 状態をリセット
    public func reset() {
        lastAudioSendTime = 0
        silentAudioCallCount = 0
        externalAudioSampleCount = 0
        externalAudioStartHostTime = 0
        externalAudioFormat = nil
    }

    /// 外部オーディオの使用を設定
    public func setUseExternalAudio(_ enabled: Bool) {
        useExternalAudio = enabled
        if enabled {
            externalAudioSampleCount = 0
            externalAudioStartHostTime = 0
            externalAudioFormat = nil
        }
    }

    /// 外部オーディオが有効かどうか
    public var isExternalAudioEnabled: Bool {
        return useExternalAudio
    }

    /// 無音オーディオを送信
    public func sendSilentAudio() {
        guard let stream = streamProvider?.currentStream else { return }

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

    /// Unityからオーディオフレームを受信して送信
    public func sendAudioFrame(samples: UnsafePointer<Float>, sampleCount: Int32, channels: Int32, sampleRate: Int32) {
        guard useExternalAudio, let stream = streamProvider?.currentStream else { return }

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
}

// MARK: - StreamProvider Protocol

/// RTMPStreamを提供するプロトコル
public protocol StreamProvider: AnyObject {
    var currentStream: RTMPStream? { get }
}
