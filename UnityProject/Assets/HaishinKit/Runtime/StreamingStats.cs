using UnityEngine;

namespace HaishinKit
{
    /// <summary>
    /// 配信中の統計情報
    /// </summary>
    [System.Serializable]
    public class StreamingStats
    {
        [Header("Video")]
        public long VideoFramesSent;

        [Header("Audio")]
        public long AudioFramesSent;
        public int AudioQueueDepth;

        [Header("Errors")]
        public long DroppedFrames;

        [Header("Performance")]
        public float Uptime;
        public float CurrentFps;

        // FPS 計算用（非シリアライズ）
        [System.NonSerialized] private int _frameCountThisSecond;
        [System.NonSerialized] private float _fpsTimer;
        [System.NonSerialized] private float _publishStartTime;
        [System.NonSerialized] private bool _needsStartTime;

        /// <summary>
        /// 配信開始時にリセットする（非メインスレッドから呼ばれる可能性あり）
        /// </summary>
        public void Reset()
        {
            VideoFramesSent = 0;
            AudioFramesSent = 0;
            AudioQueueDepth = 0;
            DroppedFrames = 0;
            Uptime = 0f;
            CurrentFps = 0f;
            _frameCountThisSecond = 0;
            _fpsTimer = 0f;
            _needsStartTime = true;
        }

        /// <summary>
        /// 毎フレーム呼び出して Uptime と FPS を更新する
        /// </summary>
        public void Update()
        {
            if (_needsStartTime)
            {
                _publishStartTime = Time.unscaledTime;
                _needsStartTime = false;
            }
            Uptime = Time.unscaledTime - _publishStartTime;

            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 1f)
            {
                CurrentFps = _frameCountThisSecond / _fpsTimer;
                _frameCountThisSecond = 0;
                _fpsTimer = 0f;
            }
        }

        /// <summary>
        /// ビデオフレーム送信時に呼び出す
        /// </summary>
        public void RecordVideoFrameSent()
        {
            VideoFramesSent++;
            _frameCountThisSecond++;
        }
    }
}
