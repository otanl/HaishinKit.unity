using System;
using System.Runtime.InteropServices;
using UnityEngine;
using AOT;

namespace HaishinKit
{
    /// <summary>
    /// iOS / macOS 向け P/Invoke バックエンド
    /// </summary>
#if UNITY_IOS || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR
    internal class AppleStreamingBackend : IStreamingBackend
    {
        #region DLL Import

#if UNITY_IOS && !UNITY_EDITOR
        private const string DllName = "__Internal";
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

        #region Fields

        private IntPtr _nativeInstance = IntPtr.Zero;
        private static StatusCallbackDelegate _statusCallbackDelegate;
        private static GCHandle _callbackHandle;

        #endregion

        #region IStreamingBackend

        public bool IsInitialized => _nativeInstance != IntPtr.Zero;

        public void Initialize(GameObject callbackTarget)
        {
            try
            {
                _nativeInstance = HaishinKit_CreateInstance();

                if (_nativeInstance == IntPtr.Zero)
                {
                    Debug.LogError("[HaishinKit] Failed to create native instance");
                    return;
                }

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

        public void Cleanup()
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

        public string GetVersion()
        {
            IntPtr ptr = HaishinKit_GetVersion();
            if (ptr == IntPtr.Zero) return "Unknown";

            string version = Marshal.PtrToStringAnsi(ptr);
            HaishinKit_FreeString(ptr);
            return version;
        }

        public void Connect(string url, string streamName)
        {
            HaishinKit_Connect(_nativeInstance, url, streamName);
        }

        public void Disconnect()
        {
            HaishinKit_Disconnect(_nativeInstance);
        }

        public void StartPublishing()
        {
            HaishinKit_StartPublishing(_nativeInstance);
        }

        public void StopPublishing()
        {
            HaishinKit_StopPublishing(_nativeInstance);
        }

        public void StartPublishingWithTexture(int width, int height)
        {
            HaishinKit_StartPublishingWithTexture(_nativeInstance, width, height);
        }

        public void SendVideoFrame(IntPtr texturePtr)
        {
            HaishinKit_SendVideoFrame(_nativeInstance, texturePtr);
        }

        public void SendVideoFrame(RenderTexture renderTexture)
        {
            SendVideoFrame(renderTexture.GetNativeTexturePtr());
        }

        public void SetVideoBitrate(int kbps)
        {
            HaishinKit_SetVideoBitrate(_nativeInstance, kbps);
        }

        public void SetAudioBitrate(int kbps)
        {
            HaishinKit_SetAudioBitrate(_nativeInstance, kbps);
        }

        public void SetFrameRate(int fps)
        {
            HaishinKit_SetFrameRate(_nativeInstance, fps);
        }

        public void SwitchCamera()
        {
            HaishinKit_SwitchCamera(_nativeInstance);
        }

        public void SetZoom(float level)
        {
            HaishinKit_SetZoom(_nativeInstance, level);
        }

        public void SetTorch(bool enabled)
        {
            HaishinKit_SetTorch(_nativeInstance, enabled);
        }

        public void SetUseExternalAudio(bool enabled)
        {
            HaishinKit_SetUseExternalAudio(_nativeInstance, enabled);
        }

        public void SetAudioSampleRate(int sampleRate)
        {
            // Apple 側ではネイティブが自動設定するため不要
        }

        public void SendAudioFrame(float[] samples, int sampleCount, int channels, int sampleRate)
        {
            HaishinKit_SendAudioFrame(_nativeInstance, samples, sampleCount, channels, sampleRate);
        }

        #endregion

        #region Callback

        [MonoPInvokeCallback(typeof(StatusCallbackDelegate))]
        private static void OnNativeStatusCallbackStatic(string status)
        {
            HaishinKitManager.Instance?.HandleStatusChange(status);
        }

        #endregion
    }
#endif
}
