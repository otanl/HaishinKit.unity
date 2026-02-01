using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace HaishinKit
{
    /// <summary>
    /// AudioListener から音声をキャプチャして配信に送信するコンポーネント
    /// AudioListener と同じ GameObject にアタッチしてください
    /// </summary>
    public class AudioStreamCapture : MonoBehaviour
    {
        #region Inspector Settings

        [Header("Settings")]
        [Tooltip("音量調整 (0.0 - 2.0)")]
        [Range(0f, 2f)]
        [SerializeField] private float _volume = 1.0f;

        [Header("Debug")]
        [Tooltip("詳細なデバッグログを出力")]
        [SerializeField] private bool _enableDebugLog = false;

        #endregion

        #region Public Properties

        public float Volume
        {
            get => _volume;
            set => _volume = Mathf.Clamp(value, 0f, 2f);
        }

        public bool IsCapturing => _isCapturing;
        public int CapturedFrames => _capturedFrames;
        public int SentFrames => _sentFrames;
        public int BufferOverruns => _bufferOverrunCount;

        #endregion

        #region Private Fields

        // オーディオバッファ（スレッド間で安全に受け渡し）
        private readonly struct AudioBuffer
        {
            public readonly float[] Samples;
            public readonly int Length;
            public readonly int Channels;

            public AudioBuffer(float[] samples, int length, int channels)
            {
                Samples = samples;
                Length = length;
                Channels = channels;
            }
        }

        // スレッドセーフなキュー
        private readonly ConcurrentQueue<AudioBuffer> _audioQueue = new ConcurrentQueue<AudioBuffer>();

        // 状態管理
        private int _sampleRate;
        private volatile bool _isCapturing;
        private volatile bool _pendingStart;
        private volatile bool _pendingStop;

        // バッファプール（GC削減）
        private const int BufferPoolSize = 32;
        private const int MaxBufferSize = 4096;
        private float[][] _bufferPool;
        private volatile int _poolWriteIndex;
        private volatile int _poolReadIndex;

        // 統計カウンター
        private int _capturedFrames;
        private int _sentFrames;
        private int _filterCallCount;
        private int _bufferOverrunCount;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // メインスレッドでサンプルレートを取得
            _sampleRate = AudioSettings.outputSampleRate;

            // バッファプールを初期化
            _bufferPool = new float[BufferPoolSize][];
            for (int i = 0; i < BufferPoolSize; i++)
            {
                _bufferPool[i] = new float[MaxBufferSize];
            }

            // AudioListener の存在確認
            var listener = GetComponent<AudioListener>();
            if (listener == null)
            {
                Debug.LogWarning("[AudioStreamCapture] AudioListener not found on this GameObject! OnAudioFilterRead will not be called.");
            }
        }

        private void OnDisable()
        {
            StopCaptureInternal();
        }

        private void Update()
        {
            ProcessPendingCommands();
            ProcessAudioQueue();
        }

        /// <summary>
        /// Unity のオーディオスレッドから呼ばれるコールバック
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            _filterCallCount++;

            if (!_isCapturing || _bufferPool == null) return;
            if (data.Length > MaxBufferSize) return;

            // リングバッファから書き込み位置を取得
            int writeIndex = _poolWriteIndex;
            int nextWriteIndex = (writeIndex + 1) % BufferPoolSize;

            // バッファオーバーラン検出
            if (nextWriteIndex == _poolReadIndex)
            {
                _bufferOverrunCount++;
                return;
            }

            float[] buffer = _bufferPool[writeIndex];
            if (buffer == null) return;

            // データをコピー（音量調整込み）
            CopyAudioData(data, buffer);

            // キューに追加
            _audioQueue.Enqueue(new AudioBuffer(buffer, data.Length, channels));

            _poolWriteIndex = nextWriteIndex;
            _capturedFrames++;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 音声キャプチャを開始（配信開始時に呼ぶ）
        /// </summary>
        public void StartCapture()
        {
            _pendingStart = true;
        }

        /// <summary>
        /// 音声キャプチャを停止（配信停止時に呼ぶ）
        /// </summary>
        public void StopCapture()
        {
            _pendingStop = true;
        }

        #endregion

        #region Private Methods

        private void ProcessPendingCommands()
        {
            if (_pendingStop)
            {
                _pendingStop = false;
                _pendingStart = false;
                StopCaptureInternal();
            }
            else if (_pendingStart)
            {
                _pendingStart = false;
                StartCaptureInternal();
            }
        }

        private void StartCaptureInternal()
        {
            if (HaishinKitManager.Instance == null || !HaishinKitManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[AudioStreamCapture] HaishinKitManager not ready");
                return;
            }

            HaishinKitManager.Instance.SetUseExternalAudio(true);
            _isCapturing = true;

            // キューをクリア
            while (_audioQueue.TryDequeue(out _)) { }

            // カウンターとインデックスをリセット
            _capturedFrames = 0;
            _sentFrames = 0;
            _filterCallCount = 0;
            _bufferOverrunCount = 0;
            _poolWriteIndex = 0;
            _poolReadIndex = 0;

            if (_enableDebugLog)
            {
                Debug.Log($"[AudioStreamCapture] Capture started (SampleRate: {_sampleRate}Hz)");
            }
        }

        private void StopCaptureInternal()
        {
            _isCapturing = false;

            // キューをクリア
            while (_audioQueue.TryDequeue(out _)) { }

            if (HaishinKitManager.Instance != null)
            {
                HaishinKitManager.Instance.SetUseExternalAudio(false);
            }

            if (_enableDebugLog)
            {
                Debug.Log($"[AudioStreamCapture] Capture stopped (Sent: {_sentFrames}, Overruns: {_bufferOverrunCount})");
            }
        }

        private void ProcessAudioQueue()
        {
            if (!_isCapturing) return;
            if (HaishinKitManager.Instance == null) return;

            // デバッグログ（有効時のみ、60フレームごと）
            if (_enableDebugLog && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[AudioStreamCapture] Stats: captured={_capturedFrames}, sent={_sentFrames}, overruns={_bufferOverrunCount}, queue={_audioQueue.Count}");
            }

            // キューに溜まっているバッファを全て処理
            while (_audioQueue.TryDequeue(out AudioBuffer buffer))
            {
                HaishinKitManager.Instance.SendAudioFrame(buffer.Samples, buffer.Length, buffer.Channels, _sampleRate);
                _sentFrames++;

                // リードインデックスを更新
                _poolReadIndex = (_poolReadIndex + 1) % BufferPoolSize;
            }
        }

        private void CopyAudioData(float[] source, float[] dest)
        {
            if (Math.Abs(_volume - 1.0f) < 0.001f)
            {
                Array.Copy(source, dest, source.Length);
            }
            else
            {
                for (int i = 0; i < source.Length; i++)
                {
                    dest[i] = source[i] * _volume;
                }
            }
        }

        #endregion
    }
}
