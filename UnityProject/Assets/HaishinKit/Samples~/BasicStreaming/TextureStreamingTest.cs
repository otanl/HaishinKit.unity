using System;
using UnityEngine;
using HaishinKit;

namespace HaishinKit.Samples
{
    /// <summary>
    /// Unity のレンダリング結果をテクスチャとして配信するサンプル
    /// </summary>
    public class TextureStreamingTest : MonoBehaviour
    {
        #region Inspector Settings

        [Header("RTMP Settings")]
        [SerializeField] private string rtmpUrl = "rtmp://localhost/live";
        [SerializeField] private string streamKey = "test";

        [Header("Video Settings")]
        [SerializeField] private int videoWidth = 1280;
        [SerializeField] private int videoHeight = 720;
        [SerializeField] private int videoBitrate = 2000; // kbps
        [SerializeField] private int audioBitrate = 128;  // kbps

        [Header("Source Camera")]
        [SerializeField] private Camera sourceCamera;

        [Header("Audio")]
        [Tooltip("AudioStreamCapture (自動検出されます)")]
        [SerializeField] private AudioStreamCapture audioCapture;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLog = false;

        #endregion

        #region Private Fields

        private HaishinKitManager _manager;
        private RenderTexture _renderTexture;
        private bool _isConnected;
        private bool _isPublishing;
        private int _sentFrames;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            InitializeManager();
            SetupEventHandlers();
            CreateRenderTexture();
            SetupCamera();
            SetupAudioCapture();
        }

        private void Update()
        {
            if (!_isPublishing || _renderTexture == null) return;

            SendVideoFrame();
        }

        private void OnDestroy()
        {
            CleanupRenderTexture();
        }

        private void OnGUI()
        {
            DrawUI();
        }

        #endregion

        #region Initialization

        private void InitializeManager()
        {
            if (HaishinKitManager.Instance == null)
            {
                var go = new GameObject("HaishinKitManager");
                go.AddComponent<HaishinKitManager>();
            }

            _manager = HaishinKitManager.Instance;
        }

        private void SetupEventHandlers()
        {
            _manager.OnConnected += OnConnected;
            _manager.OnDisconnected += OnDisconnected;
            _manager.OnPublishingStarted += OnPublishingStarted;
            _manager.OnPublishingStopped += OnPublishingStopped;
            _manager.OnError += OnError;
        }

        private void CreateRenderTexture()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
            }

            _renderTexture = new RenderTexture(videoWidth, videoHeight, 24, RenderTextureFormat.BGRA32);
            _renderTexture.Create();

            if (enableDebugLog)
            {
                Debug.Log($"[TextureStreaming] RenderTexture created: {videoWidth}x{videoHeight}");
            }
        }

        private void SetupCamera()
        {
            if (sourceCamera == null)
            {
                sourceCamera = Camera.main;
            }

            if (sourceCamera != null)
            {
                sourceCamera.targetTexture = _renderTexture;
            }
            else
            {
                Debug.LogError("[TextureStreaming] No camera found!");
            }
        }

        private void SetupAudioCapture()
        {
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                Debug.LogWarning("[TextureStreaming] No AudioListener found in scene!");
                return;
            }

            // AudioListener の GameObject に AudioStreamCapture を追加/取得
            var listenerAudioCapture = listener.GetComponent<AudioStreamCapture>();
            if (listenerAudioCapture == null)
            {
                listenerAudioCapture = listener.gameObject.AddComponent<AudioStreamCapture>();
            }

            audioCapture = listenerAudioCapture;
        }

        private void CleanupRenderTexture()
        {
            if (_renderTexture == null) return;

            if (sourceCamera != null)
            {
                sourceCamera.targetTexture = null;
            }
            _renderTexture.Release();
            _renderTexture = null;
        }

        #endregion

        #region Event Handlers

        private void OnConnected()
        {
            _isConnected = true;
            if (enableDebugLog)
            {
                Debug.Log("[TextureStreaming] Connected");
            }
        }

        private void OnDisconnected()
        {
            _isConnected = false;
            _isPublishing = false;
            if (enableDebugLog)
            {
                Debug.Log("[TextureStreaming] Disconnected");
            }
        }

        private void OnPublishingStarted()
        {
            _isPublishing = true;
            _sentFrames = 0;
            audioCapture?.StartCapture();
            if (enableDebugLog)
            {
                Debug.Log("[TextureStreaming] Publishing started");
            }
        }

        private void OnPublishingStopped()
        {
            _isPublishing = false;
            audioCapture?.StopCapture();
            if (enableDebugLog)
            {
                Debug.Log($"[TextureStreaming] Publishing stopped (Sent: {_sentFrames} frames)");
            }
        }

        private void OnError(string error)
        {
            Debug.LogError($"[TextureStreaming] Error: {error}");
        }

        #endregion

        #region Video Streaming

        private void SendVideoFrame()
        {
            IntPtr texturePtr = _renderTexture.GetNativeTexturePtr();
            if (texturePtr == IntPtr.Zero)
            {
                if (enableDebugLog)
                {
                    Debug.LogWarning("[TextureStreaming] texturePtr is Zero!");
                }
                return;
            }

            _manager.SendVideoFrame(texturePtr);
            _sentFrames++;
        }

        #endregion

        #region UI

        private void DrawUI()
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

            // Status
            GUI.Label(new Rect(margin, y, w, h), $"Texture Streaming - {_manager?.CurrentStatus ?? "N/A"}");
            y += h;

            GUI.Label(new Rect(margin, y, w, h * 0.5f), $"Resolution: {videoWidth}x{videoHeight}");
            y += h * 0.6f;

            // URL
            GUI.Label(new Rect(margin, y, 80 * scale, h * 0.5f), "URL:");
            rtmpUrl = GUI.TextField(new Rect(margin + 80 * scale, y, w - 80 * scale, h * 0.6f), rtmpUrl);
            y += h * 0.7f;

            // Stream Key
            GUI.Label(new Rect(margin, y, 80 * scale, h * 0.5f), "Key:");
            streamKey = GUI.TextField(new Rect(margin + 80 * scale, y, w - 80 * scale, h * 0.6f), streamKey);
            y += h * 0.8f;

            // Connect / Disconnect
            DrawConnectionButton(ref y, margin, w, h);
            y += h + margin;

            // Publishing
            DrawPublishingButton(ref y, margin, w, h);
            y += h + margin;

            // Preview
            DrawPreview(y, margin, scale);
        }

        private void DrawConnectionButton(ref float y, float margin, float w, float h)
        {
            if (!_isConnected)
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Connect"))
                {
                    _manager?.Connect(rtmpUrl, streamKey);
                }
            }
            else
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Disconnect"))
                {
                    _manager?.Disconnect();
                }
            }
        }

        private void DrawPublishingButton(ref float y, float margin, float w, float h)
        {
            GUI.enabled = _isConnected;

            if (!_isPublishing)
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Start Publishing"))
                {
                    _manager?.SetVideoBitrate(videoBitrate);
                    _manager?.SetAudioBitrate(audioBitrate);
                    _manager?.StartPublishingWithTexture(videoWidth, videoHeight);
                }
            }
            else
            {
                if (GUI.Button(new Rect(margin, y, w, h), "Stop Publishing"))
                {
                    _manager?.StopPublishing();
                }
            }

            GUI.enabled = true;
        }

        private void DrawPreview(float y, float margin, float scale)
        {
            if (_renderTexture == null) return;

            float previewHeight = 200 * scale;
            float previewWidth = previewHeight * videoWidth / videoHeight;
            GUI.DrawTexture(new Rect(margin, y, previewWidth, previewHeight), _renderTexture, ScaleMode.ScaleToFit);
        }

        #endregion
    }
}
