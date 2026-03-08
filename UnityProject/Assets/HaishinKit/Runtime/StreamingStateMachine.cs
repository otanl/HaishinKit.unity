using UnityEngine;

namespace HaishinKit
{
    /// <summary>
    /// 配信状態の遷移を管理するステートマシン
    /// ネイティブ側から受信する文字列ステータスを StreamingStatus enum に変換する
    /// </summary>
    internal class StreamingStateMachine
    {
        public StreamingStatus CurrentStatus { get; private set; } = StreamingStatus.Disconnected;
        public string LastErrorMessage { get; private set; } = "";

        /// <summary>
        /// C# 側からの明示的な状態遷移（Connect, StopPublishing 呼び出し時）
        /// </summary>
        public void TransitionTo(StreamingStatus newStatus)
        {
            if (!IsValidTransition(CurrentStatus, newStatus))
            {
                Debug.LogWarning($"[HaishinKit] Invalid state transition: {CurrentStatus} -> {newStatus}");
            }

            CurrentStatus = newStatus;

            if (newStatus != StreamingStatus.Error)
            {
                LastErrorMessage = "";
            }
        }

        /// <summary>
        /// ネイティブ側からの文字列ステータスを処理して enum に変換する
        /// </summary>
        public StreamingStatus ProcessNativeStatus(string status)
        {
            StreamingStatus newStatus;

            if (status.StartsWith("error:"))
            {
                newStatus = StreamingStatus.Error;
                LastErrorMessage = status.Substring(6);
            }
            else
            {
                switch (status)
                {
                    case "connected":
                        newStatus = StreamingStatus.Connected;
                        LastErrorMessage = "";
                        break;
                    case "disconnected":
                        newStatus = StreamingStatus.Disconnected;
                        LastErrorMessage = "";
                        break;
                    case "publishing":
                        newStatus = StreamingStatus.Publishing;
                        LastErrorMessage = "";
                        break;
                    case "stopped":
                        newStatus = StreamingStatus.Disconnected;
                        LastErrorMessage = "";
                        break;
                    default:
                        Debug.LogWarning($"[HaishinKit] Unknown native status: {status}");
                        return CurrentStatus;
                }
            }

            if (!IsValidTransition(CurrentStatus, newStatus))
            {
                Debug.LogWarning($"[HaishinKit] Unexpected state transition: {CurrentStatus} -> {newStatus} (native: \"{status}\")");
            }

            CurrentStatus = newStatus;
            return newStatus;
        }

        /// <summary>
        /// 状態をリセットする
        /// </summary>
        public void Reset()
        {
            CurrentStatus = StreamingStatus.Disconnected;
            LastErrorMessage = "";
        }

        private static bool IsValidTransition(StreamingStatus from, StreamingStatus to)
        {
            if (from == to) return true;

            switch (from)
            {
                case StreamingStatus.Disconnected:
                    return to == StreamingStatus.Connecting;
                case StreamingStatus.Connecting:
                    return to == StreamingStatus.Connected
                        || to == StreamingStatus.Error
                        || to == StreamingStatus.Disconnected;
                case StreamingStatus.Connected:
                    return to == StreamingStatus.Publishing
                        || to == StreamingStatus.Disconnected
                        || to == StreamingStatus.Error;
                case StreamingStatus.Publishing:
                    return to == StreamingStatus.Stopping
                        || to == StreamingStatus.Disconnected
                        || to == StreamingStatus.Error;
                case StreamingStatus.Stopping:
                    return to == StreamingStatus.Disconnected
                        || to == StreamingStatus.Connected
                        || to == StreamingStatus.Error;
                case StreamingStatus.Error:
                    return to == StreamingStatus.Disconnected
                        || to == StreamingStatus.Connecting;
                default:
                    return false;
            }
        }
    }
}
