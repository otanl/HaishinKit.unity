import Foundation

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
