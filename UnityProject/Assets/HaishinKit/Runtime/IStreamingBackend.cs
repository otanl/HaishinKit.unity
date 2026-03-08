using System;
using UnityEngine;

namespace HaishinKit
{
    /// <summary>
    /// プラットフォーム固有の配信バックエンドインターフェース
    /// </summary>
    internal interface IStreamingBackend
    {
        bool IsInitialized { get; }

        void Initialize(GameObject callbackTarget);
        void Cleanup();
        string GetVersion();

        // Connection
        void Connect(string url, string streamName);
        void Disconnect();

        // Publishing
        void StartPublishing();
        void StopPublishing();
        void StartPublishingWithTexture(int width, int height);
        void SendVideoFrame(IntPtr texturePtr);
        void SendVideoFrame(RenderTexture renderTexture);

        // Settings
        void SetVideoBitrate(int kbps);
        void SetAudioBitrate(int kbps);
        void SetFrameRate(int fps);

        // Camera
        void SwitchCamera();
        void SetZoom(float level);
        void SetTorch(bool enabled);

        // Audio
        void SetUseExternalAudio(bool enabled);
        void SetAudioSampleRate(int sampleRate);
        void SendAudioFrame(float[] samples, int sampleCount, int channels, int sampleRate);
    }
}
