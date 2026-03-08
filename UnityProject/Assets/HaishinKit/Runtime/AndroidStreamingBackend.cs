using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HaishinKit
{
    /// <summary>
    /// Android 向けビデオフレームの読み戻し方式
    /// </summary>
    public enum AndroidReadbackMode
    {
        /// <summary>ReadPixels による同期リードバック（従来方式）</summary>
        ReadPixels,

        /// <summary>AsyncGPUReadback による非同期リードバック</summary>
        AsyncGPUReadback,

        /// <summary>OpenGL ES テクスチャ ID を直接渡す（ゼロコピー）</summary>
        NativeTexture,

        /// <summary>C++ Native Plugin 経由（GL.IssuePluginEvent、ゼロコピー）</summary>
        NativePlugin
    }

#if UNITY_ANDROID
    /// <summary>
    /// Android 向け AndroidJavaObject バックエンド
    /// </summary>
    internal class AndroidStreamingBackend : IStreamingBackend
    {
        private AndroidJavaClass _androidBridge;
        private Texture2D _readbackTexture;

        private AndroidReadbackMode _readbackMode = AndroidReadbackMode.ReadPixels;
        private float _targetFrameInterval;
        private float _lastSendTime;

        public bool IsInitialized => _androidBridge != null;

        public void Initialize(GameObject callbackTarget)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        _androidBridge = new AndroidJavaClass("com.haishinkit.unity.UnityBridge");
                        _androidBridge.CallStatic("initialize", activity);
                        _androidBridge.CallStatic("setCallback", callbackTarget.name, "OnNativeStatusCallback");

                        string version = _androidBridge.CallStatic<string>("getVersion");
                        Debug.Log($"[HaishinKit] Android initialized (version: {version})");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HaishinKit] Android initialization failed: {e.Message}\n{e.StackTrace}");
                _androidBridge = null;
            }
        }

        public void Cleanup()
        {
            if (_androidBridge != null)
            {
                try
                {
                    _androidBridge.CallStatic("cleanup");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[HaishinKit] Android cleanup failed: {e.Message}");
                }
                _androidBridge = null;
            }

            if (_readbackTexture != null)
            {
                UnityEngine.Object.Destroy(_readbackTexture);
                _readbackTexture = null;
            }
        }

        public string GetVersion()
        {
            if (_androidBridge == null) return "Not Initialized";
            return _androidBridge.CallStatic<string>("getVersion");
        }

        public void Connect(string url, string streamName)
        {
            try
            {
                _androidBridge.CallStatic("connect", url, streamName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HaishinKit] Android connect failed: {e.Message}");
            }
        }

        public void Disconnect()
        {
            _androidBridge.CallStatic("disconnect");
        }

        public void StartPublishing()
        {
            Debug.LogWarning("[HaishinKit] StartPublishing is not supported on Android. Use StartPublishingWithTexture instead.");
        }

        public void StopPublishing()
        {
            _androidBridge.CallStatic("stopPublishing");
        }

        public void StartPublishingWithTexture(int width, int height)
        {
            if (_readbackTexture == null || _readbackTexture.width != width || _readbackTexture.height != height)
            {
                if (_readbackTexture != null) UnityEngine.Object.Destroy(_readbackTexture);
                _readbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }
            _androidBridge.CallStatic("startPublishingWithTexture", width, height);
        }

        public void SendVideoFrame(IntPtr texturePtr)
        {
            Debug.LogWarning("[HaishinKit] Use SendVideoFrame(RenderTexture) on Android");
        }

        public void SendVideoFrame(RenderTexture renderTexture)
        {
            if (_androidBridge == null || renderTexture == null) return;

            // フレームレートスロットリング
            if (_targetFrameInterval > 0f)
            {
                float now = Time.unscaledTime;
                if (now - _lastSendTime < _targetFrameInterval) return;
                _lastSendTime = now;
            }

            switch (_readbackMode)
            {
                case AndroidReadbackMode.ReadPixels:
                    SendVideoFrameReadPixels(renderTexture);
                    break;

                case AndroidReadbackMode.AsyncGPUReadback:
                    SendVideoFrameAsync(renderTexture);
                    break;

                case AndroidReadbackMode.NativeTexture:
                    int textureId = (int)renderTexture.GetNativeTexturePtr();
                    _androidBridge.CallStatic("sendVideoFrameNativeTexture", textureId, renderTexture.width, renderTexture.height);
                    break;

                case AndroidReadbackMode.NativePlugin:
                    int pluginTextureId = (int)renderTexture.GetNativeTexturePtr();
                    _androidBridge.CallStatic("sendVideoFrameNativeTexture", pluginTextureId, renderTexture.width, renderTexture.height);
                    break;
            }
        }

        public void SetVideoBitrate(int kbps)
        {
            _androidBridge.CallStatic("setVideoBitrate", kbps);
        }

        public void SetAudioBitrate(int kbps)
        {
            _androidBridge.CallStatic("setAudioBitrate", kbps);
        }

        public void SetFrameRate(int fps)
        {
            _androidBridge.CallStatic("setFrameRate", fps);
        }

        public void SwitchCamera()
        {
            Debug.LogWarning("[HaishinKit] SwitchCamera is not supported on Android in texture mode");
        }

        public void SetZoom(float level)
        {
            Debug.LogWarning("[HaishinKit] SetZoom is not supported on Android in texture mode");
        }

        public void SetTorch(bool enabled)
        {
            Debug.LogWarning("[HaishinKit] SetTorch is not supported on Android in texture mode");
        }

        public void SetUseExternalAudio(bool enabled)
        {
            _androidBridge.CallStatic("setUseExternalAudio", enabled);
        }

        public void SetAudioSampleRate(int sampleRate)
        {
            _androidBridge.CallStatic("setAudioSampleRate", sampleRate);
        }

        public void SendAudioFrame(float[] samples, int sampleCount, int channels, int sampleRate)
        {
            _androidBridge.CallStatic("sendAudioFrame", samples, sampleCount, channels, sampleRate);
        }

        #region Android-specific API

        /// <summary>
        /// ビデオフレームの読み戻し方式を設定
        /// </summary>
        public void SetReadbackMode(AndroidReadbackMode mode)
        {
            _readbackMode = mode;
        }

        /// <summary>
        /// ビデオフレーム送信の目標 FPS を設定（0 = 毎フレーム送信）
        /// </summary>
        public void SetTargetSendFps(int fps)
        {
            _targetFrameInterval = fps > 0 ? 1f / fps : 0f;
        }

        /// <summary>
        /// デバッグログの有効/無効を切り替え
        /// </summary>
        public void SetDebugEnabled(bool enabled)
        {
            try
            {
                _androidBridge?.CallStatic("setDebugEnabled", enabled);
            }
            catch (Exception)
            {
                // setDebugEnabled が未実装の古いバージョンでは無視
            }
        }

        #endregion

        #region Private Methods

        private void SendVideoFrameReadPixels(RenderTexture renderTexture)
        {
            RenderTexture.active = renderTexture;
            _readbackTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
            RenderTexture.active = null;

            var rawData = _readbackTexture.GetRawTextureData();
            _androidBridge.CallStatic("sendVideoFrame", rawData, renderTexture.width, renderTexture.height);
        }

        private void SendVideoFrameAsync(RenderTexture renderTexture)
        {
            AsyncGPUReadback.Request(renderTexture, 0, TextureFormat.RGBA32, (request) =>
            {
                if (request.hasError || _androidBridge == null) return;

                var data = request.GetData<byte>();
                _androidBridge.CallStatic("sendVideoFrame", data.ToArray(), renderTexture.width, renderTexture.height);
            });
        }

        #endregion
    }
#endif
}
