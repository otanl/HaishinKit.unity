using UnityEditor;
using UnityEngine;

namespace HaishinKit.Editor
{
    [CustomEditor(typeof(AudioStreamCapture))]
    public class AudioStreamCaptureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var capture = (AudioStreamCapture)target;

            if (!Application.isPlaying) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Stats", EditorStyles.boldLabel);

            var stats = capture.GetStats();

            EditorGUILayout.LabelField("Sample Rate", $"{stats.SampleRate} Hz");
            EditorGUILayout.LabelField("Captured Frames", stats.CapturedFrames.ToString());
            EditorGUILayout.LabelField("Sent Frames", stats.SentFrames.ToString());
            EditorGUILayout.LabelField("Buffer Overruns", stats.BufferOverruns.ToString());
            EditorGUILayout.LabelField("Dropped Frames", stats.DroppedFrames.ToString());
            EditorGUILayout.LabelField("Queue Depth", stats.QueueDepth.ToString());
            EditorGUILayout.LabelField("Is Capturing", capture.IsCapturing.ToString());

            // 自動更新
            if (capture.IsCapturing)
            {
                Repaint();
            }
        }
    }
}
