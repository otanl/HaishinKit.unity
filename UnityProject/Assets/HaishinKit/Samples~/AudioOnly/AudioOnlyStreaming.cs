using UnityEngine;
using HaishinKit;

namespace HaishinKit.Samples
{
    /// <summary>
    /// ゲーム音声のみを配信するミニマルサンプル
    /// AudioListener がアタッチされた GameObject に配置してください
    /// </summary>
    public class AudioOnlyStreaming : MonoBehaviour
    {
        [Header("RTMP Settings")]
        [SerializeField] private string rtmpUrl = "rtmp://localhost/live";
        [SerializeField] private string streamKey = "test";

        [Header("Audio Settings")]
        [SerializeField] private int audioBitrate = 128; // kbps

        private HaishinKitManager _manager;
        private AudioStreamCapture _audioCapture;
        private bool _isConnected;
        private bool _isPublishing;

        private void Start()
        {
            _manager = HaishinKitManager.Instance;

            // AudioStreamCapture を自動セットアップ
            _audioCapture = GetComponent<AudioStreamCapture>();
            if (_audioCapture == null)
            {
                _audioCapture = gameObject.AddComponent<AudioStreamCapture>();
            }

            _manager.OnConnected += () => _isConnected = true;
            _manager.OnDisconnected += () => { _isConnected = false; _isPublishing = false; };
            _manager.OnPublishingStarted += () =>
            {
                _isPublishing = true;
                _audioCapture.StartCapture();
            };
            _manager.OnPublishingStopped += () =>
            {
                _isPublishing = false;
                _audioCapture.StopCapture();
            };
            _manager.OnError += (error) => Debug.LogError($"[AudioOnly] Error: {error}");
        }

        private void OnGUI()
        {
            float scale = Screen.dpi > 0 ? Screen.dpi / 160f : 2f;
            scale = Mathf.Clamp(scale, 1f, 4f);

            GUI.skin.label.fontSize = (int)(16 * scale);
            GUI.skin.button.fontSize = (int)(18 * scale);
            GUI.skin.textField.fontSize = (int)(16 * scale);

            float h = 60 * scale;
            float margin = 20 * scale;
            float w = Screen.width - margin * 2;
            float y = Screen.safeArea.y + margin;

            GUI.Label(new Rect(margin, y, w, h), $"Audio Only - {_manager.Status}");
            y += h;

            // Stats
            if (_isPublishing)
            {
                var audioStats = _audioCapture.GetStats();
                GUI.Label(new Rect(margin, y, w, h * 0.5f),
                    $"Sent: {audioStats.SentFrames} | Overruns: {audioStats.BufferOverruns} | Queue: {audioStats.QueueDepth}");
                y += h * 0.6f;
            }

            // URL / Key
            GUI.Label(new Rect(margin, y, 80 * scale, h * 0.5f), "URL:");
            rtmpUrl = GUI.TextField(new Rect(margin + 80 * scale, y, w - 80 * scale, h * 0.6f), rtmpUrl);
            y += h * 0.7f;

            GUI.Label(new Rect(margin, y, 80 * scale, h * 0.5f), "Key:");
            streamKey = GUI.TextField(new Rect(margin + 80 * scale, y, w - 80 * scale, h * 0.6f), streamKey);
            y += h * 0.8f;

            // Connect
            if (!_isConnected)
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Connect"))
                {
                    _manager.Connect(rtmpUrl, streamKey);
                }
            }
            else
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Disconnect"))
                {
                    _manager.Disconnect();
                }
            }
            y += h + margin;

            // Publish
            GUI.enabled = _isConnected;
            if (!_isPublishing)
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Start Audio Streaming"))
                {
                    _manager.SetAudioBitrate(audioBitrate);
                    // テクスチャモードで最小解像度（音声のみ実質使用）
                    _manager.StartPublishingWithTexture(160, 120);
                }
            }
            else
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Stop Audio Streaming"))
                {
                    _manager.StopPublishing();
                }
            }
            GUI.enabled = true;
        }
    }
}
