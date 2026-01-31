using UnityEngine;
using HaishinKit;

namespace HaishinKit.Samples
{
    /// <summary>
    /// HaishinKit 動作確認用シンプルテストスクリプト
    /// シーンの空の GameObject にアタッチして使用
    /// </summary>
    public class HaishinKitTestScene : MonoBehaviour
    {
        [Header("RTMP Settings")]
        [SerializeField] private string rtmpUrl = "rtmp://localhost/live";
        [SerializeField] private string streamKey = "test";

        [Header("Video Settings")]
        [SerializeField] private int videoBitrate = 1000; // kbps
        [SerializeField] private int audioBitrate = 128;  // kbps
        [SerializeField] private int frameRate = 30;

        private HaishinKitManager _manager;
        private bool _isConnected = false;
        private bool _isPublishing = false;

        private int _frameCount = 0;

        private void Awake()
        {
            Debug.Log("[Test] HaishinKitTestScene Awake() called");
        }

        private void Update()
        {
            _frameCount++;
            if (_frameCount == 1 || _frameCount == 60 || _frameCount == 120)
            {
                Debug.Log($"[Test] Update frame {_frameCount}, Screen: {Screen.width}x{Screen.height}");
            }
        }

        private void Start()
        {
            Debug.Log("[Test] HaishinKitTestScene Start() called");

            // HaishinKitManager がシーンにない場合は自動作成
            if (HaishinKitManager.Instance == null)
            {
                Debug.Log("[Test] Creating HaishinKitManager...");
                var go = new GameObject("HaishinKitManager");
                go.AddComponent<HaishinKitManager>();
                Debug.Log("[Test] HaishinKitManager created");
            }

            _manager = HaishinKitManager.Instance;
            Debug.Log($"[Test] HaishinKitManager.Instance = {(_manager != null ? "OK" : "NULL")}");

            // イベント登録
            _manager.OnConnected += () => {
                _isConnected = true;
                Debug.Log("[Test] Connected!");
            };

            _manager.OnDisconnected += () => {
                _isConnected = false;
                _isPublishing = false;
                Debug.Log("[Test] Disconnected!");
            };

            _manager.OnPublishingStarted += () => {
                _isPublishing = true;
                Debug.Log("[Test] Publishing started!");
            };

            _manager.OnPublishingStopped += () => {
                _isPublishing = false;
                Debug.Log("[Test] Publishing stopped!");
            };

            _manager.OnError += (error) => {
                Debug.LogError($"[Test] Error: {error}");
            };

            // Version 確認
            Debug.Log($"[Test] Version: {_manager.GetVersion()}");
        }

        private void OnGUI()
        {
            // 超シンプルなテスト - 固定サイズ
            GUI.skin.label.fontSize = 32;
            GUI.skin.button.fontSize = 32;

            float y = 100;
            float h = 80;
            float w = Screen.width - 40;

            GUI.Label(new Rect(20, y, w, h), $"HaishinKit Test - {_manager?.CurrentStatus ?? "N/A"}");
            y += h;

            if (!_isConnected)
            {
                if (GUI.Button(new Rect(20, y, w, h), "Connect"))
                {
                    _manager?.Connect(rtmpUrl, streamKey);
                }
            }
            else
            {
                if (GUI.Button(new Rect(20, y, w, h), "Disconnect"))
                {
                    _manager?.Disconnect();
                }
            }
            y += h + 20;

            GUI.enabled = _isConnected;
            if (!_isPublishing)
            {
                if (GUI.Button(new Rect(20, y, w, h), "Start Publishing"))
                {
                    _manager?.SetVideoBitrate(videoBitrate);
                    _manager?.SetAudioBitrate(audioBitrate);
                    _manager?.SetFrameRate(frameRate);
                    _manager?.StartPublishing();
                }
            }
            else
            {
                if (GUI.Button(new Rect(20, y, w, h), "Stop Publishing"))
                {
                    _manager?.StopPublishing();
                }
            }
            y += h + 20;

            GUI.enabled = _isPublishing;
            if (GUI.Button(new Rect(20, y, w, h), "Switch Camera"))
            {
                _manager?.SwitchCamera();
            }
            y += h + 20;

            GUI.enabled = true;
            GUI.Label(new Rect(20, y, w, h), $"Screen: {Screen.width}x{Screen.height}");
        }
    }
}
