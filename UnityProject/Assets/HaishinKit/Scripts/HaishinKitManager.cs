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

#if UNITY_ANDROID && !UNITY_EDITOR
        public bool IsInitialized => _androidBridge != null;
#else
        public bool IsInitialized => _nativeInstance != IntPtr.Zero;
#endif
        public string CurrentStatus { get; private set; } = "";

        #endregion

        #region Private Fields

        private IntPtr _nativeInstance = IntPtr.Zero;
        private static StatusCallbackDelegate _statusCallbackDelegate;
        private static GCHandle _callbackHandle;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass _androidBridge;
        private Texture2D _readbackTexture;
        private byte[] _pixelBuffer;
#endif

        #endregion

        #region Platform-specific DLL Import (iOS/macOS)

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
#if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroid();
#elif UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            InitializeApple();
#else
            Debug.LogWarning("[HaishinKit] This plugin only supports iOS, macOS, and Android");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void InitializeAndroid()
        {
            try
            {
                Debug.Log("[HaishinKit] Starting Android initialization...");

                // Get current activity
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    Debug.Log("[HaishinKit] Got UnityPlayer class");
                    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        Debug.Log("[HaishinKit] Got currentActivity");

                        // Initialize UnityBridge
                        _androidBridge = new AndroidJavaClass("com.haishinkit.unity.UnityBridge");
                        Debug.Log("[HaishinKit] Created UnityBridge class");

                        _androidBridge.CallStatic("initialize", activity);
                        Debug.Log("[HaishinKit] Called initialize");

                        // Set callback target (this GameObject's name)
                        _androidBridge.CallStatic("setCallback", gameObject.name, "OnNativeStatusCallback");
                        Debug.Log($"[HaishinKit] Set callback to {gameObject.name}");

                        // Test: get version
                        string version = _androidBridge.CallStatic<string>("getVersion");
                        Debug.Log($"[HaishinKit] Version: {version}");
                    }
                }

                Debug.Log("[HaishinKit] Android initialization successful");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HaishinKit] Android initialization failed: {e.Message}\n{e.StackTrace}");
                _androidBridge = null;
            }
        }
#endif

#if UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private void InitializeApple()
        {
            try
            {
                _nativeInstance = HaishinKit_CreateInstance();

                if (_nativeInstance == IntPtr.Zero)
                {
                    Debug.LogError("[HaishinKit] Failed to create native instance");
                    return;
                }

                // Setup callback
                _statusCallbackDelegate = OnNativeStatusCallbackStatic;
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
        }
#endif

        private void Cleanup()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            CleanupAndroid();
#else
            CleanupApple();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void CleanupAndroid()
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
                Destroy(_readbackTexture);
                _readbackTexture = null;
            }
            _pixelBuffer = null;
        }
#endif

        private void CleanupApple()
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

        // Static callback for iOS/macOS (P/Invoke)
        [MonoPInvokeCallback(typeof(StatusCallbackDelegate))]
        private static void OnNativeStatusCallbackStatic(string status)
        {
            Instance?.HandleStatusChange(status);
        }

        // Instance callback for Android (UnitySendMessage)
        public void OnNativeStatusCallback(string status)
        {
            HandleStatusChange(status);
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
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_androidBridge == null) return "Not Initialized";
            return _androidBridge.CallStatic<string>("getVersion");
#elif UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
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
            Debug.Log($"[HaishinKit] Connect called: url={url}, streamName={streamName}, IsInitialized={IsInitialized}");

            if (!IsInitialized)
            {
                Debug.LogError("[HaishinKit] Not initialized");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Debug.Log("[HaishinKit] Calling Android connect...");
                _androidBridge.CallStatic("connect", url, streamName);
                Debug.Log("[HaishinKit] Android connect called successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HaishinKit] Android connect failed: {e.Message}\n{e.StackTrace}");
            }
#else
            HaishinKit_Connect(_nativeInstance, url, streamName);
#endif
        }

        /// <summary>
        /// サーバーから切断
        /// </summary>
        public void Disconnect()
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("disconnect");
#else
            HaishinKit_Disconnect(_nativeInstance);
#endif
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

#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("[HaishinKit] StartPublishing is not supported on Android. Use StartPublishingWithTexture instead.");
#else
            HaishinKit_StartPublishing(_nativeInstance);
#endif
        }

        /// <summary>
        /// 配信を停止
        /// </summary>
        public void StopPublishing()
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("stopPublishing");
#else
            HaishinKit_StopPublishing(_nativeInstance);
#endif
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

#if UNITY_ANDROID && !UNITY_EDITOR
            // Prepare readback texture for Android
            if (_readbackTexture == null || _readbackTexture.width != width || _readbackTexture.height != height)
            {
                if (_readbackTexture != null) Destroy(_readbackTexture);
                _readbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _pixelBuffer = new byte[width * height * 4];
            }
            _androidBridge.CallStatic("startPublishingWithTexture", width, height);
#else
            HaishinKit_StartPublishingWithTexture(_nativeInstance, width, height);
#endif
        }

        /// <summary>
        /// ビデオフレームを送信
        /// </summary>
        /// <param name="texturePtr">Metal テクスチャのネイティブポインタ (iOS/macOS)</param>
        public void SendVideoFrame(IntPtr texturePtr)
        {
            if (!IsInitialized || texturePtr == IntPtr.Zero) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("[HaishinKit] Use SendVideoFrame(RenderTexture) on Android");
#else
            HaishinKit_SendVideoFrame(_nativeInstance, texturePtr);
#endif
        }

        /// <summary>
        /// ビデオフレームを送信 (RenderTexture版 - Android用)
        /// </summary>
        /// <param name="renderTexture">送信するRenderTexture</param>
        public void SendVideoFrame(RenderTexture renderTexture)
        {
            if (!IsInitialized || renderTexture == null) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            // Read pixels from RenderTexture
            RenderTexture.active = renderTexture;
            _readbackTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
            RenderTexture.active = null;

            // Get raw texture data and send to native
            var rawData = _readbackTexture.GetRawTextureData();
            _androidBridge.CallStatic("sendVideoFrame", rawData, renderTexture.width, renderTexture.height);
#else
            // On iOS/macOS, use the native texture pointer
            SendVideoFrame(renderTexture.GetNativeTexturePtr());
#endif
        }

        #endregion

        #region Public API - Settings

        /// <summary>
        /// ビデオビットレートを設定 (kbps)
        /// </summary>
        public void SetVideoBitrate(int kbps)
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("setVideoBitrate", kbps);
#else
            HaishinKit_SetVideoBitrate(_nativeInstance, kbps);
#endif
        }

        /// <summary>
        /// オーディオビットレートを設定 (kbps)
        /// </summary>
        public void SetAudioBitrate(int kbps)
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("setAudioBitrate", kbps);
#else
            HaishinKit_SetAudioBitrate(_nativeInstance, kbps);
#endif
        }

        /// <summary>
        /// フレームレートを設定
        /// </summary>
        public void SetFrameRate(int fps)
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("setFrameRate", fps);
#else
            HaishinKit_SetFrameRate(_nativeInstance, fps);
#endif
        }

        #endregion

        #region Public API - Camera Control

        /// <summary>
        /// カメラを切り替え (前面/背面)
        /// </summary>
        public void SwitchCamera()
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("[HaishinKit] SwitchCamera is not supported on Android in texture mode");
#else
            HaishinKit_SwitchCamera(_nativeInstance);
#endif
        }

        /// <summary>
        /// ズームレベルを設定
        /// </summary>
        /// <param name="level">ズーム倍率 (1.0 - 5.0)</param>
        public void SetZoom(float level)
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("[HaishinKit] SetZoom is not supported on Android in texture mode");
#else
            HaishinKit_SetZoom(_nativeInstance, level);
#endif
        }

        /// <summary>
        /// トーチ（ライト）を設定
        /// </summary>
        public void SetTorch(bool enabled)
        {
            if (!IsInitialized) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("[HaishinKit] SetTorch is not supported on Android in texture mode");
#else
            HaishinKit_SetTorch(_nativeInstance, enabled);
#endif
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

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("setUseExternalAudio", enabled);
#else
            HaishinKit_SetUseExternalAudio(_nativeInstance, enabled);
#endif
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

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("sendAudioFrame", samples, sampleCount, channels, sampleRate);
#else
            HaishinKit_SendAudioFrame(_nativeInstance, samples, sampleCount, channels, sampleRate);
#endif
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

#if UNITY_ANDROID && !UNITY_EDITOR
            _androidBridge.CallStatic("sendAudioFrame", samples, sampleCount, channels, sampleRate);
#else
            HaishinKit_SendAudioFrame(_nativeInstance, samples, sampleCount, channels, sampleRate);
#endif
        }

        #endregion
    }
}
