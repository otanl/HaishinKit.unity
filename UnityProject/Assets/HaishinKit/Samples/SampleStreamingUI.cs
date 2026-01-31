using UnityEngine;
using UnityEngine.UI;
using HaishinKit;

namespace HaishinKit.Samples
{
    /// <summary>
    /// HaishinKit サンプル UI
    /// 基本的な RTMP 配信機能のデモ
    /// </summary>
    public class SampleStreamingUI : MonoBehaviour
    {
        [Header("Connection Settings")]
        [SerializeField] private InputField urlInput;
        [SerializeField] private InputField streamNameInput;

        [Header("Buttons")]
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button switchCameraButton;
        [SerializeField] private Button torchButton;

        [Header("Sliders")]
        [SerializeField] private Slider videoBitrateSlider;
        [SerializeField] private Slider audioBitrateSlider;
        [SerializeField] private Slider zoomSlider;

        [Header("Labels")]
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text videoBitrateLabel;
        [SerializeField] private Text audioBitrateLabel;
        [SerializeField] private Text zoomLabel;

        [Header("FPS Control")]
        [SerializeField] private Button fps15Button;
        [SerializeField] private Button fps30Button;
        [SerializeField] private Button fps60Button;

        private HaishinKitManager _manager;
        private bool _torchEnabled = false;

        private void Start()
        {
            _manager = HaishinKitManager.Instance;

            if (_manager == null)
            {
                Debug.LogError("HaishinKitManager not found. Please add it to the scene.");
                return;
            }

            // Subscribe to events
            _manager.OnStatusChanged += OnStatusChanged;
            _manager.OnConnected += OnConnected;
            _manager.OnDisconnected += OnDisconnected;
            _manager.OnPublishingStarted += OnPublishingStarted;
            _manager.OnPublishingStopped += OnPublishingStopped;
            _manager.OnError += OnError;

            // Setup buttons
            SetupButtons();

            // Setup sliders
            SetupSliders();

            // Initial state
            UpdateUIState(false, false);

            // Show version
            if (statusLabel != null)
            {
                statusLabel.text = $"Ready - {_manager.GetVersion()}";
            }
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.OnStatusChanged -= OnStatusChanged;
                _manager.OnConnected -= OnConnected;
                _manager.OnDisconnected -= OnDisconnected;
                _manager.OnPublishingStarted -= OnPublishingStarted;
                _manager.OnPublishingStopped -= OnPublishingStopped;
                _manager.OnError -= OnError;
            }
        }

        private void SetupButtons()
        {
            if (connectButton != null)
                connectButton.onClick.AddListener(OnConnectClicked);

            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectClicked);

            if (startButton != null)
                startButton.onClick.AddListener(OnStartClicked);

            if (stopButton != null)
                stopButton.onClick.AddListener(OnStopClicked);

            if (switchCameraButton != null)
                switchCameraButton.onClick.AddListener(OnSwitchCameraClicked);

            if (torchButton != null)
                torchButton.onClick.AddListener(OnTorchClicked);

            if (fps15Button != null)
                fps15Button.onClick.AddListener(() => OnFpsClicked(15));

            if (fps30Button != null)
                fps30Button.onClick.AddListener(() => OnFpsClicked(30));

            if (fps60Button != null)
                fps60Button.onClick.AddListener(() => OnFpsClicked(60));
        }

        private void SetupSliders()
        {
            if (videoBitrateSlider != null)
            {
                videoBitrateSlider.minValue = 100;
                videoBitrateSlider.maxValue = 5000;
                videoBitrateSlider.value = 1000;
                videoBitrateSlider.onValueChanged.AddListener(OnVideoBitrateChanged);
                UpdateVideoBitrateLabel();
            }

            if (audioBitrateSlider != null)
            {
                audioBitrateSlider.minValue = 32;
                audioBitrateSlider.maxValue = 256;
                audioBitrateSlider.value = 128;
                audioBitrateSlider.onValueChanged.AddListener(OnAudioBitrateChanged);
                UpdateAudioBitrateLabel();
            }

            if (zoomSlider != null)
            {
                zoomSlider.minValue = 1f;
                zoomSlider.maxValue = 5f;
                zoomSlider.value = 1f;
                zoomSlider.onValueChanged.AddListener(OnZoomChanged);
                UpdateZoomLabel();
            }
        }

        private void UpdateUIState(bool isConnected, bool isPublishing)
        {
            if (connectButton != null)
                connectButton.interactable = !isConnected;

            if (disconnectButton != null)
                disconnectButton.interactable = isConnected;

            if (startButton != null)
                startButton.interactable = isConnected && !isPublishing;

            if (stopButton != null)
                stopButton.interactable = isPublishing;

            if (switchCameraButton != null)
                switchCameraButton.interactable = isPublishing;

            if (torchButton != null)
                torchButton.interactable = isPublishing;

            if (zoomSlider != null)
                zoomSlider.interactable = isPublishing;
        }

        #region Button Handlers

        private void OnConnectClicked()
        {
            string url = urlInput?.text ?? "rtmp://localhost/live";
            string streamName = streamNameInput?.text ?? "stream";

            _manager.Connect(url, streamName);
        }

        private void OnDisconnectClicked()
        {
            _manager.Disconnect();
        }

        private void OnStartClicked()
        {
            _manager.StartPublishing();
        }

        private void OnStopClicked()
        {
            _manager.StopPublishing();
        }

        private void OnSwitchCameraClicked()
        {
            _manager.SwitchCamera();
        }

        private void OnTorchClicked()
        {
            _torchEnabled = !_torchEnabled;
            _manager.SetTorch(_torchEnabled);
        }

        private void OnFpsClicked(int fps)
        {
            _manager.SetFrameRate(fps);
            Debug.Log($"[HaishinKit] FPS set to {fps}");
        }

        #endregion

        #region Slider Handlers

        private void OnVideoBitrateChanged(float value)
        {
            _manager.SetVideoBitrate((int)value);
            UpdateVideoBitrateLabel();
        }

        private void OnAudioBitrateChanged(float value)
        {
            _manager.SetAudioBitrate((int)value);
            UpdateAudioBitrateLabel();
        }

        private void OnZoomChanged(float value)
        {
            _manager.SetZoom(value);
            UpdateZoomLabel();
        }

        private void UpdateVideoBitrateLabel()
        {
            if (videoBitrateLabel != null && videoBitrateSlider != null)
            {
                videoBitrateLabel.text = $"Video: {(int)videoBitrateSlider.value} kbps";
            }
        }

        private void UpdateAudioBitrateLabel()
        {
            if (audioBitrateLabel != null && audioBitrateSlider != null)
            {
                audioBitrateLabel.text = $"Audio: {(int)audioBitrateSlider.value} kbps";
            }
        }

        private void UpdateZoomLabel()
        {
            if (zoomLabel != null && zoomSlider != null)
            {
                zoomLabel.text = $"Zoom: {zoomSlider.value:F1}x";
            }
        }

        #endregion

        #region Event Handlers

        private void OnStatusChanged(string status)
        {
            if (statusLabel != null)
            {
                statusLabel.text = status;
            }
        }

        private void OnConnected()
        {
            UpdateUIState(true, false);
        }

        private void OnDisconnected()
        {
            UpdateUIState(false, false);
        }

        private void OnPublishingStarted()
        {
            UpdateUIState(true, true);
        }

        private void OnPublishingStopped()
        {
            UpdateUIState(true, false);
        }

        private void OnError(string error)
        {
            Debug.LogError($"[HaishinKit] Error: {error}");
        }

        #endregion
    }
}
