using System;
using System.Runtime.InteropServices;
using UnityEngine;
using AOT;

namespace HaishinKit
{
    /// <summary>
    /// HaishinKit Unity Plugin Manager
    /// RTMP/SRT ライブストリーミング機能を提供
    /// </summary>
    public class HaishinKitManager : MonoBehaviour
    {
        #region Singleton

        public static HaishinKitManager Instance { get; private set; }

        #endregion

        #region Events

        public event Action<string> OnStatusChanged;
        public event Action<string> OnError;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action OnPublishingStarted;
        public event Action OnPublishingStopped;

        #endregion

        #region Public Properties

        public bool IsInitialized => _nativeInstance != IntPtr.Zero;
        public string CurrentStatus { get; private set; } = "";

        #endregion

        #region Private Fields

        private IntPtr _nativeInstance = IntPtr.Zero;
        private static StatusCallbackDelegate _statusCallbackDelegate;
        private static GCHandle _callbackHandle;

        #endregion

        #region Platform-specific DLL Import

#if UNITY_IOS && !UNITY_EDITOR
        private const string DllName = "__Internal";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string DllName = "HaishinKitUnity";
#else
        private const string DllName = "HaishinKitUnity";
#endif

        // Version
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr HaishinKit_GetVersion();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_FreeString(IntPtr ptr);

        // Instance Management
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr HaishinKit_CreateInstance();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_Cleanup(IntPtr ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_DestroyInstance(IntPtr ptr);

        // Connection
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_Connect(IntPtr ptr, string url, string streamName);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_Disconnect(IntPtr ptr);

        // Publishing
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_StartPublishing(IntPtr ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_StopPublishing(IntPtr ptr);

        // Settings
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SetVideoBitrate(IntPtr ptr, int bitrate);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SetAudioBitrate(IntPtr ptr, int bitrate);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SetFrameRate(IntPtr ptr, int fps);

        // Camera
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SwitchCamera(IntPtr ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SetZoom(IntPtr ptr, float level);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SetTorch(IntPtr ptr, bool enabled);

        // Callback
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StatusCallbackDelegate(string status);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SetStatusCallback(IntPtr ptr, StatusCallbackDelegate callback);

        // Texture Streaming
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_StartPublishingWithTexture(IntPtr ptr, int width, int height);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SendVideoFrame(IntPtr ptr, IntPtr texturePtr);

        // External Audio
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SetUseExternalAudio(IntPtr ptr, bool enabled);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void HaishinKit_SendAudioFrame(IntPtr ptr, float[] samples, int sampleCount, int channels, int sampleRate);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void OnDestroy()
        {
            Cleanup();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
#if UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            try
            {
                _nativeInstance = HaishinKit_CreateInstance();

                if (_nativeInstance == IntPtr.Zero)
                {
                    Debug.LogError("[HaishinKit] Failed to create native instance");
                    return;
                }

                // Setup callback
                _statusCallbackDelegate = OnNativeStatusCallback;
                _callbackHandle = GCHandle.Alloc(_statusCallbackDelegate);
                HaishinKit_SetStatusCallback(_nativeInstance, _statusCallbackDelegate);
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError($"[HaishinKit] Native library not found: {e.Message}");
            }
            catch (EntryPointNotFoundException e)
            {
                Debug.LogError($"[HaishinKit] Native function not found: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HaishinKit] Initialization failed: {e.GetType().Name} - {e.Message}");
            }
#else
            Debug.LogWarning("[HaishinKit] This plugin only supports iOS and macOS");
#endif
        }

        private void Cleanup()
        {
            if (_nativeInstance != IntPtr.Zero)
            {
                HaishinKit_Cleanup(_nativeInstance);

                if (_callbackHandle.IsAllocated)
                {
                    _callbackHandle.Free();
                }

                HaishinKit_DestroyInstance(_nativeInstance);
                _nativeInstance = IntPtr.Zero;
            }
            else if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }
        }

        #endregion

        #region Callback Handler

        [MonoPInvokeCallback(typeof(StatusCallbackDelegate))]
        private static void OnNativeStatusCallback(string status)
        {
            Instance?.HandleStatusChange(status);
        }

        private void HandleStatusChange(string status)
        {
            CurrentStatus = status;
            OnStatusChanged?.Invoke(status);

            if (status.StartsWith("error:"))
            {
                var errorMessage = status.Substring(6);
                OnError?.Invoke(errorMessage);
                Debug.LogError($"[HaishinKit] Error: {errorMessage}");
            }
            else
            {
                switch (status)
                {
                    case "connected":
                        OnConnected?.Invoke();
                        break;
                    case "disconnected":
                        OnDisconnected?.Invoke();
                        break;
                    case "publishing":
                        OnPublishingStarted?.Invoke();
                        break;
                    case "stopped":
                        OnPublishingStopped?.Invoke();
                        break;
                }
            }
        }

        #endregion

        #region Public API - Connection

        /// <summary>
        /// プラグインのバージョンを取得
        /// </summary>
        public string GetVersion()
        {
#if UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            IntPtr ptr = HaishinKit_GetVersion();
            if (ptr == IntPtr.Zero) return "Unknown";

            string version = Marshal.PtrToStringAnsi(ptr);
            HaishinKit_FreeString(ptr);
            return version;
#else
            return "Unsupported Platform";
#endif
        }

        /// <summary>
        /// RTMP サーバーに接続
        /// </summary>
        /// <param name="url">RTMP URL (例: rtmp://live.example.com/app)</param>
        /// <param name="streamName">ストリーム名/キー</param>
        public void Connect(string url, string streamName)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[HaishinKit] Not initialized");
                return;
            }

            HaishinKit_Connect(_nativeInstance, url, streamName);
        }

        /// <summary>
        /// サーバーから切断
        /// </summary>
        public void Disconnect()
        {
            if (!IsInitialized) return;
            HaishinKit_Disconnect(_nativeInstance);
        }

        #endregion

        #region Public API - Publishing

        /// <summary>
        /// 配信を開始（カメラ/マイクモード）
        /// </summary>
        public void StartPublishing()
        {
            if (!IsInitialized)
            {
                Debug.LogError("[HaishinKit] Not initialized");
                return;
            }

            HaishinKit_StartPublishing(_nativeInstance);
        }

        /// <summary>
        /// 配信を停止
        /// </summary>
        public void StopPublishing()
        {
            if (!IsInitialized) return;
            HaishinKit_StopPublishing(_nativeInstance);
        }

        /// <summary>
        /// テクスチャモードで配信を開始
        /// </summary>
        /// <param name="width">テクスチャの幅</param>
        /// <param name="height">テクスチャの高さ</param>
        public void StartPublishingWithTexture(int width, int height)
        {
            if (!IsInitialized)
            {
                Debug.LogError("[HaishinKit] Not initialized");
                return;
            }

            HaishinKit_StartPublishingWithTexture(_nativeInstance, width, height);
        }

        /// <summary>
        /// ビデオフレームを送信
        /// </summary>
        /// <param name="texturePtr">Metal テクスチャのネイティブポインタ</param>
        public void SendVideoFrame(IntPtr texturePtr)
        {
            if (!IsInitialized || texturePtr == IntPtr.Zero) return;
            HaishinKit_SendVideoFrame(_nativeInstance, texturePtr);
        }

        #endregion

        #region Public API - Settings

        /// <summary>
        /// ビデオビットレートを設定 (kbps)
        /// </summary>
        public void SetVideoBitrate(int kbps)
        {
            if (!IsInitialized) return;
            HaishinKit_SetVideoBitrate(_nativeInstance, kbps);
        }

        /// <summary>
        /// オーディオビットレートを設定 (kbps)
        /// </summary>
        public void SetAudioBitrate(int kbps)
        {
            if (!IsInitialized) return;
            HaishinKit_SetAudioBitrate(_nativeInstance, kbps);
        }

        /// <summary>
        /// フレームレートを設定
        /// </summary>
        public void SetFrameRate(int fps)
        {
            if (!IsInitialized) return;
            HaishinKit_SetFrameRate(_nativeInstance, fps);
        }

        #endregion

        #region Public API - Camera Control

        /// <summary>
        /// カメラを切り替え (前面/背面)
        /// </summary>
        public void SwitchCamera()
        {
            if (!IsInitialized) return;
            HaishinKit_SwitchCamera(_nativeInstance);
        }

        /// <summary>
        /// ズームレベルを設定
        /// </summary>
        /// <param name="level">ズーム倍率 (1.0 - 5.0)</param>
        public void SetZoom(float level)
        {
            if (!IsInitialized) return;
            HaishinKit_SetZoom(_nativeInstance, level);
        }

        /// <summary>
        /// トーチ（ライト）を設定
        /// </summary>
        public void SetTorch(bool enabled)
        {
            if (!IsInitialized) return;
            HaishinKit_SetTorch(_nativeInstance, enabled);
        }

        #endregion

        #region Public API - External Audio

        /// <summary>
        /// 外部オーディオの使用を設定
        /// </summary>
        /// <param name="enabled">外部オーディオを使用するかどうか</param>
        public void SetUseExternalAudio(bool enabled)
        {
            if (!IsInitialized) return;
            HaishinKit_SetUseExternalAudio(_nativeInstance, enabled);
        }

        /// <summary>
        /// オーディオフレームを送信
        /// </summary>
        /// <param name="samples">インターリーブされたFloat32 PCMサンプル</param>
        /// <param name="channels">チャンネル数</param>
        /// <param name="sampleRate">サンプルレート</param>
        public void SendAudioFrame(float[] samples, int channels, int sampleRate)
        {
            if (!IsInitialized || samples == null || samples.Length == 0) return;
            int sampleCount = samples.Length / channels;
            HaishinKit_SendAudioFrame(_nativeInstance, samples, sampleCount, channels, sampleRate);
        }

        /// <summary>
        /// オーディオフレームを送信（バッファサイズ指定版）
        /// </summary>
        /// <param name="samples">インターリーブされたFloat32 PCMサンプル</param>
        /// <param name="length">実際のデータ長</param>
        /// <param name="channels">チャンネル数</param>
        /// <param name="sampleRate">サンプルレート</param>
        public void SendAudioFrame(float[] samples, int length, int channels, int sampleRate)
        {
            if (!IsInitialized || samples == null || length == 0) return;
            int sampleCount = length / channels;
            HaishinKit_SendAudioFrame(_nativeInstance, samples, sampleCount, channels, sampleRate);
        }

        #endregion
    }
}
