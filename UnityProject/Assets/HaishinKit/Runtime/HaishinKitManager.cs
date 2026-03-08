using System;
using UnityEngine;

namespace HaishinKit
{
    /// <summary>
    /// HaishinKit Unity Plugin Manager
    /// RTMP ライブストリーミング機能を提供
    /// </summary>
    public class HaishinKitManager : MonoBehaviour
    {
        #region Singleton

        public static HaishinKitManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;
            var go = new GameObject("[HaishinKitManager]");
            go.AddComponent<HaishinKitManager>();
        }

        #endregion

        #region Events

        /// <summary>
        /// ステータス変更イベント（enum 版）
        /// </summary>
        public event Action<StreamingStatus> OnStreamingStatusChanged;

        /// <summary>
        /// ステータス変更イベント（文字列版、後方互換）
        /// </summary>
        public event Action<string> OnStatusChanged;

        public event Action<string> OnError;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action OnPublishingStarted;
        public event Action OnPublishingStopped;

        #endregion

        #region Public Properties

        /// <summary>
        /// 現在の配信状態（enum）
        /// </summary>
        public StreamingStatus Status => _stateMachine.CurrentStatus;

        /// <summary>
        /// 最後のエラーメッセージ
        /// </summary>
        public string ErrorMessage => _stateMachine.LastErrorMessage;

        /// <summary>
        /// 現在のステータス文字列（後方互換）
        /// </summary>
        public string CurrentStatus { get; private set; } = "";

        /// <summary>
        /// バックエンドが初期化済みかどうか
        /// </summary>
        public bool IsInitialized => _backend?.IsInitialized ?? false;

        /// <summary>
        /// 配信統計情報
        /// </summary>
        public StreamingStats Stats { get; } = new StreamingStats();

        #endregion

        #region Private Fields

        private IStreamingBackend _backend;
        private StreamingStateMachine _stateMachine;
        private bool _isPublishing;

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

            _stateMachine = new StreamingStateMachine();
            InitializeBackend();
        }

        private void Update()
        {
            if (_isPublishing)
            {
                Stats.Update();
            }
        }

        private void OnDestroy()
        {
            _backend?.Cleanup();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        private void InitializeBackend()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _backend = new AndroidStreamingBackend();
#elif UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            _backend = new AppleStreamingBackend();
#else
            Debug.LogWarning("[HaishinKit] This plugin only supports iOS, macOS, and Android");
            return;
#endif
            _backend.Initialize(gameObject);
        }

        #endregion

        #region Callback Handler

        /// <summary>
        /// Android 用コールバック（UnitySendMessage から呼ばれる）
        /// </summary>
        public void OnNativeStatusCallback(string status)
        {
            HandleStatusChange(status);
        }

        /// <summary>
        /// ネイティブ側からのステータス変更を処理する
        /// </summary>
        internal void HandleStatusChange(string status)
        {
            CurrentStatus = status;

            var newEnumStatus = _stateMachine.ProcessNativeStatus(status);

            // 後方互換イベント（文字列）
            OnStatusChanged?.Invoke(status);

            // 新イベント（enum）
            OnStreamingStatusChanged?.Invoke(newEnumStatus);

            // 個別イベント
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
                        _isPublishing = false;
                        OnDisconnected?.Invoke();
                        break;
                    case "publishing":
                        _isPublishing = true;
                        Stats.Reset();
                        OnPublishingStarted?.Invoke();
                        break;
                    case "stopped":
                        _isPublishing = false;
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
            if (!IsInitialized) return "Not Initialized";
            return _backend.GetVersion();
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

            _stateMachine.TransitionTo(StreamingStatus.Connecting);
            _backend.Connect(url, streamName);
        }

        /// <summary>
        /// サーバーから切断
        /// </summary>
        public void Disconnect()
        {
            if (!IsInitialized) return;
            _backend.Disconnect();
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

            _backend.StartPublishing();
        }

        /// <summary>
        /// 配信を停止
        /// </summary>
        public void StopPublishing()
        {
            if (!IsInitialized) return;

            _stateMachine.TransitionTo(StreamingStatus.Stopping);
            _backend.StopPublishing();
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

            // エンコーダ初期化前に Unity のサンプルレートを設定
            _backend.SetAudioSampleRate(AudioSettings.outputSampleRate);
            _backend.StartPublishingWithTexture(width, height);
        }

        /// <summary>
        /// ビデオフレームを送信
        /// </summary>
        /// <param name="texturePtr">Metal テクスチャのネイティブポインタ (iOS/macOS)</param>
        public void SendVideoFrame(IntPtr texturePtr)
        {
            if (!IsInitialized || texturePtr == IntPtr.Zero) return;

            _backend.SendVideoFrame(texturePtr);
            Stats.RecordVideoFrameSent();
        }

        /// <summary>
        /// ビデオフレームを送信 (RenderTexture版)
        /// </summary>
        /// <param name="renderTexture">送信するRenderTexture</param>
        public void SendVideoFrame(RenderTexture renderTexture)
        {
            if (!IsInitialized || renderTexture == null) return;

            _backend.SendVideoFrame(renderTexture);
            Stats.RecordVideoFrameSent();
        }

        #endregion

        #region Public API - Settings

        /// <summary>
        /// ビデオビットレートを設定 (kbps)
        /// </summary>
        public void SetVideoBitrate(int kbps)
        {
            if (!IsInitialized) return;
            _backend.SetVideoBitrate(kbps);
        }

        /// <summary>
        /// オーディオビットレートを設定 (kbps)
        /// </summary>
        public void SetAudioBitrate(int kbps)
        {
            if (!IsInitialized) return;
            _backend.SetAudioBitrate(kbps);
        }

        /// <summary>
        /// フレームレートを設定
        /// </summary>
        public void SetFrameRate(int fps)
        {
            if (!IsInitialized) return;
            _backend.SetFrameRate(fps);
        }

        #endregion

        #region Public API - Camera Control

        /// <summary>
        /// カメラを切り替え (前面/背面)
        /// </summary>
        public void SwitchCamera()
        {
            if (!IsInitialized) return;
            _backend.SwitchCamera();
        }

        /// <summary>
        /// ズームレベルを設定
        /// </summary>
        /// <param name="level">ズーム倍率 (1.0 - 5.0)</param>
        public void SetZoom(float level)
        {
            if (!IsInitialized) return;
            _backend.SetZoom(level);
        }

        /// <summary>
        /// トーチ（ライト）を設定
        /// </summary>
        public void SetTorch(bool enabled)
        {
            if (!IsInitialized) return;
            _backend.SetTorch(enabled);
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
            _backend.SetUseExternalAudio(enabled);
        }

        /// <summary>
        /// オーディオサンプルレートを設定
        /// </summary>
        /// <param name="sampleRate">サンプルレート (例: 48000)</param>
        public void SetAudioSampleRate(int sampleRate)
        {
            if (!IsInitialized) return;
            _backend.SetAudioSampleRate(sampleRate);
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

            _backend.SendAudioFrame(samples, sampleCount, channels, sampleRate);
            Stats.AudioFramesSent++;
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

            _backend.SendAudioFrame(samples, sampleCount, channels, sampleRate);
            Stats.AudioFramesSent++;
        }

        #endregion

        #region Public API - Android-specific

#if UNITY_ANDROID
        /// <summary>
        /// Android のビデオフレーム読み戻し方式を設定
        /// </summary>
        public void SetAndroidReadbackMode(AndroidReadbackMode mode)
        {
            if (_backend is AndroidStreamingBackend android)
            {
                android.SetReadbackMode(mode);
            }
        }

        /// <summary>
        /// ビデオフレーム送信の目標 FPS を設定（0 = 毎フレーム送信）
        /// </summary>
        public void SetTargetSendFps(int fps)
        {
            if (_backend is AndroidStreamingBackend android)
            {
                android.SetTargetSendFps(fps);
            }
        }
#endif

        #endregion
    }
}
